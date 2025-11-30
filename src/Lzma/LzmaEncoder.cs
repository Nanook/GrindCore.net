using System;
using System.Linq;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lzma;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Provides an encoder for LZMA-compressed data, supporting block-based compression.
    /// </summary>
    internal unsafe class LzmaEncoder : IDisposable
    {
        private IntPtr _encoder;
        private CBufferInStream _inStream;
        private byte[] _inBuffer;
        private GCHandle _inBufferPinned;
        private long _toFlush;

        /// <summary>
        /// Gets the LZMA properties used for encoding.
        /// </summary>
        public byte[] Properties { get; }

        /// <summary>
        /// Gets the block size used for compression.
        /// </summary>
        public int BlockSize { get; }

        /// <summary>
        /// Recommended input buffer size (native CStream input recommendation) used by the encoder.
        /// </summary>
        public int InputBufferSize { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LzmaEncoder"/> class with the specified compression parameters.
        /// Dictionary size, fast-bytes (word size) and other tuning are taken from <paramref name="dictOptions"/> when provided.
        /// </summary>
        /// <param name="dictOptions">Dictionary and tuning options. When a field is set it will be applied to the native props.</param>
        /// <param name="threadCount">Optional thread count override.</param>
        /// <param name="level">Compression level fallback (used when dictOptions does not provide an explicit level). Default: 5.</param>
        /// <exception cref="Exception">Thrown if the encoder context or buffer cannot be allocated or configured.</exception>
        public LzmaEncoder(CompressionDictionaryOptions? dictOptions = null, int? threadCount = null, int level = 5)
        {
            _toFlush = 0;

            CLzmaEncProps props = new CLzmaEncProps();

            SZ_Lzma_v25_01_EncProps_Init(ref props);

            // level: prefer value coming from dictOptions if somebody added it there; fallback to supplied level
            // (CompressionDictionaryOptions currently doesn't define a Level property in this snapshot,
            // so we use the provided level fallback).
            props.level = level;

            // Determine dictSize from dictOptions.DictionarySize when present, otherwise leave 0 so native defaults apply.
            uint dictSize = 0;
            if (dictOptions?.DictionarySize is long ds && ds != 0)
            {
                if (ds < 0 || ds > uint.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(dictOptions.DictionarySize), $"DictionarySize must be between 0 and {uint.MaxValue}.");
                dictSize = (uint)ds;
            }
            props.dictSize = dictSize;
            props.mc = 0;
            // Mark unspecified so native normalize() can fill defaults for values we don't set.
            props.lc = props.lp = props.pb = props.algo = props.fb = props.btMode = props.numHashBytes = props.numThreads = -1;

            // Fixed properties that we always want to set explicitly
            props.writeEndMark = 0; // default no end marker
            props.affinity = 0;
            props.reduceSize = ulong.MaxValue; // -1 means set to blocksize if blocksize not -1(solid)|0(auto) && blocksize<filesize

            // Apply dictionary options when provided (only override when set)
            if (dictOptions != null)
            {
                if (dictOptions.LiteralContextBits.HasValue)
                    props.lc = dictOptions.LiteralContextBits.Value;
                if (dictOptions.LiteralPositionBits.HasValue)
                    props.lp = dictOptions.LiteralPositionBits.Value;
                if (dictOptions.PositionBits.HasValue)
                    props.pb = dictOptions.PositionBits.Value;
                if (dictOptions.Algorithm.HasValue)
                    props.algo = dictOptions.Algorithm.Value;

                // fast-bytes (fb) / word size
                if (dictOptions.FastBytes.HasValue)
                    props.fb = dictOptions.FastBytes.Value;

                if (dictOptions.BinaryTreeMode.HasValue)
                    props.btMode = dictOptions.BinaryTreeMode.Value;
                if (dictOptions.HashBytes.HasValue)
                    props.numHashBytes = dictOptions.HashBytes.Value;

                if (dictOptions.MatchCycles.HasValue)
                    props.mc = (uint)dictOptions.MatchCycles.Value;
            }

            // Thread count: explicit override wins, otherwise force single-threaded for LZMA
            // LZMA native code is compiled for single-threading only (LZMA2 is different and supports multithreading)
            props.numThreads = 1; // Force single-threaded for all LZMA levels

            _encoder = SZ_Lzma_v25_01_Enc_Create();

            int res = SZ_Lzma_v25_01_Enc_SetProps(_encoder, ref props); // normalizes properties
            if (res != 0)
                throw new Exception($"Failed to set LZMA encoder config {res}");

            byte[] p = BufferPool.Rent(0x10);
            ulong sz = (ulong)p.Length;

            fixed (byte* inPtr = p)
                SZ_Lzma_v25_01_Enc_WriteProperties(_encoder, inPtr, &sz);
            this.Properties = p.Take((int)sz).ToArray();
            BufferPool.Return(p);

            uint bufferSize = 0;
            uint dSize = 0;

            res = SZ_Lzma_v25_01_Enc_LzmaCodeMultiCallPrepare(_encoder, &bufferSize, &dSize, 0);
            if (res != 0)
                throw new Exception($"Failed to prepare LZMA encoder {res}");

            // Use the normalized dictionary size returned by native code directly.
            // Do not convert units here — the native prepare call returns the value expected
            // by the native multi-call logic. If native returns a small value like 8192
            // that represents the intended threshold; converting it to KB caused an
            // incorrect 8 MiB threshold and prevented the encoder from making progress.
            this.BlockSize = (int)dSize;
            bufferSize += 0x8; // align/safety byte

            // Expose the native recommended input buffer size so callers/streams can align thresholds.
            this.InputBufferSize = (int)bufferSize;

            _inBuffer = BufferPool.Rent((int)bufferSize);
            _inBufferPinned = GCHandle.Alloc(_inBuffer, GCHandleType.Pinned);
            _inStream = new CBufferInStream() { buffer = _inBufferPinned.AddrOfPinnedObject(), size = bufferSize };
        }

        /// <summary>
        /// Encodes data from the input buffer into the output buffer using LZMA compression.
        /// </summary>
        /// <param name="inData">The input buffer containing data to compress.</param>
        /// <param name="outData">The output buffer to write compressed data to.</param>
        /// <param name="final">Indicates if this is the final block of data.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <returns>The total number of bytes written to the output buffer.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="inData"/> or <paramref name="outData"/> is not at the correct position.</exception>
        /// <exception cref="Exception">Thrown if compression fails.</exception>
        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            uint available = 0;
            int total = 0;
            bool finalfinal = false;

            int res = 0;
            ulong outSz = 0;
            int outTotal = 0;

            while (inData.AvailableRead != 0 || final)
            {
                cancel.ThrowIfCancellationRequested();

                if (_inStream.pos == _inStream.size)
                    _inStream.pos = 0; // wrap around

                int p = (int)((_inStream.pos + _inStream.remaining) % _inStream.size);

                int sz = (int)Math.Min((ulong)inData.AvailableRead, _inStream.size - _inStream.remaining);

                int endSz = (int)(_inStream.size - (ulong)p);
                inData.Read(_inBuffer, (int)p, (int)Math.Min(sz, endSz));

                // copy data at start of circular buffer
                if (sz > endSz)
                    inData.Read(_inBuffer, 0, (int)(sz - endSz));

                total += sz;
                _inStream.remaining += (ulong)sz;

                long remaining = (long)(_inStream.remaining + (ulong)_toFlush);

                if (!final && remaining < (long)this.BlockSize)
                    break;
                finalfinal = final && remaining == 0;

                fixed (byte* baseOutPtr = outData.Data)
                {
                    // Keep calling the native multi-call while we can make progress and the out buffer has space.
                    while (true)
                    {
                        byte* callPtr = baseOutPtr + outData.Size; // current write position
                        outSz = (ulong)(outData.AvailableWrite);
                        res = SZ_Lzma_v25_01_Enc_LzmaCodeMultiCall(_encoder, callPtr, &outSz, ref _inStream, final ? 0 : this.BlockSize, &available, finalfinal ? 1 : 0);

                        if (res != 0)
                        {
                            // Handle insufficient buffer error gracefully
                            if (res == -2147023537) // ERROR_INSUFFICIENT_BUFFER (0x8007054F)
                            {
                                outTotal += (int)outSz;
                                _toFlush = available;
                                outData.Write((int)outSz);
                                return outTotal;
                            }

                            throw new Exception($"Native LZMA call failed with error {res} (0x{res:X8})");
                        }

                        // Account for bytes produced and update state
                        outTotal += (int)outSz;
                        _toFlush = available;
                        outData.Write((int)outSz);

                        // If native produced no more output or the out buffer is full, stop looping.
                        if (outSz == 0 || outData.AvailableWrite == 0)
                            break;

                        // If not final and native still expects more input (remaining + toFlush < BlockSize), allow caller to feed more.
                        long remainingNow = (long)(_inStream.remaining + (ulong)_toFlush);
                        if (!final && remainingNow < (long)this.BlockSize)
                            break;
                    }
                }

                if (res != 0)
                    throw new Exception($"Encode Error {res}");

                if (final && (outSz != 0 || _toFlush == 0)) // the algo can return 0 bytes but still have data to flush
                    break;
            }

            return outTotal;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="LzmaEncoder"/>.
        /// </summary>
        public void Dispose()
        {
            if (_encoder != IntPtr.Zero)
            {
                SZ_Lzma_v25_01_Enc_Destroy(_encoder);
                _encoder = IntPtr.Zero;
            }
            if (_inBufferPinned.IsAllocated)
                _inBufferPinned.Free();
            BufferPool.Return(_inBuffer);
        }
    }
}


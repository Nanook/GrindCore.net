using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop.Lzma;
using static Nanook.GrindCore.Interop;
using Nanook.GrindCore;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Provides an encoder for LZMA2-compressed data, supporting block-based and pseudo-solid compression.
    /// </summary>
    internal unsafe class Lzma2Encoder : IDisposable
    {
        private const int LZMA2_BLOCK_SIZE = 1 << 21; // size of unpacked blocks - important for multicall solid encoding
        private IntPtr _encoder;
        private CBufferInStream _inStream;
        private byte[] _inBuffer;
        private GCHandle _inBufferPinned;
        private bool _solid;
        private long _blkTotal;
        private bool _blockComplete;
        private bool _needsInit = false;

        /// <summary>
        /// Gets the LZMA2 property byte used for encoding.
        /// </summary>
        public byte Properties { get; }

        /// <summary>
        /// Gets the block size used for compression.
        /// </summary>
        public long BlockSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lzma2Encoder"/> class with the specified compression parameters.
        /// Accepts optional <see cref="CompressionDictionaryOptions"/>; any LZMA tuning fields set on that object
        /// will be applied and unset fields will be left for the native normalizer to choose.
        /// </summary>
        /// <param name="level">The compression level to use (default is 5).</param>
        /// <param name="threads">The number of threads to use (default is 1).</param>
        /// <param name="blockSize">The block size to use for compression (-1 for solid mode, 0 for auto, default is -1).</param>
        /// <param name="dictOptions">Optional dictionary / tuning options (dict size, fb, lc/lp/pb, etc.).</param>
        /// <param name="minBufferSize">The minimum buffer size to use (default is 0).</param>
        /// <exception cref="Exception">Thrown if the encoder context or buffer cannot be allocated or configured.</exception>
        public Lzma2Encoder(int level = 5, int threads = 1, long blockSize = -1, CompressionDictionaryOptions? dictOptions = null, int minBufferSize = 0)
        {
            if (threads <= 0)
                threads = 1;

            _encoder = SZ_Lzma2_v25_01_Enc_Create();
            if (_encoder == IntPtr.Zero)
                throw new Exception("Failed to create LZMA2 encoder.");

            // encoder already has props, replace them. Blank lc, lp etc to ensure they're recalculated from the level
            CLzma2EncProps props = new CLzma2EncProps();

            // init
            props.lzmaProps.level = 5;
            props.lzmaProps.dictSize = props.lzmaProps.mc = 0;
            props.lzmaProps.reduceSize = ulong.MaxValue;
            props.lzmaProps.lc = props.lzmaProps.lp = props.lzmaProps.pb = props.lzmaProps.algo = props.lzmaProps.fb = props.lzmaProps.btMode = props.lzmaProps.numHashBytes = props.lzmaProps.numThreads = -1;
            props.lzmaProps.numHashBytes = 0;
            props.lzmaProps.writeEndMark = 0;
            props.lzmaProps.affinity = 0;
            props.blockSize = 0;
            props.numBlockThreads_Max = -1;
            props.numBlockThreads_Reduced = -1;
            props.numTotalThreads = -1;

            // config base
            props.lzmaProps.level = level;
            props.lzmaProps.numThreads = -1;
            props.numBlockThreads_Max = threads;
            props.numBlockThreads_Reduced = -1;
            props.numTotalThreads = threads;
            props.numThreadGroups = 0; // new for 25.01

            // Apply dictionary options (dict size & LZMA tuning) if provided
            if (dictOptions != null)
            {
                // Dictionary size - only set if explicitly provided, otherwise let native normalization choose
                if (dictOptions.DictionarySize.HasValue && dictOptions.DictionarySize.Value != 0)
                {
                    long ds = dictOptions.DictionarySize.Value;
                    if (ds < 0 || ds > uint.MaxValue)
                        throw new ArgumentOutOfRangeException(nameof(dictOptions.DictionarySize), $"DictionarySize must be between 0 and {uint.MaxValue} (fits in uint).");
                    props.lzmaProps.dictSize = (uint)ds;
                    // mc uses a uint; keep previous heuristic but it's not critical
                    props.lzmaProps.mc = (uint)Math.Min(ds, int.MaxValue);
                }
                // else leave props.lzmaProps.dictSize = 0 so native normalization chooses based on level

                // LZMA fine tuning: apply fields only when set, leave others -1 so native normalize() chooses defaults
                if (dictOptions.LiteralContextBits.HasValue) props.lzmaProps.lc = Clamp(dictOptions.LiteralContextBits.Value, 0, 8);
                if (dictOptions.LiteralPositionBits.HasValue) props.lzmaProps.lp = Clamp(dictOptions.LiteralPositionBits.Value, 0, 4);
                if (dictOptions.PositionBits.HasValue) props.lzmaProps.pb = Clamp(dictOptions.PositionBits.Value, 0, 4);
                if (dictOptions.Algorithm.HasValue) props.lzmaProps.algo = Clamp(dictOptions.Algorithm.Value, 0, 1);

                if (dictOptions.FastBytes.HasValue)
                    props.lzmaProps.fb = Clamp(dictOptions.FastBytes.Value, 5, 273);
                // else leave props.lzmaProps.fb = -1 so native normalize chooses default

                if (dictOptions.BinaryTreeMode.HasValue) props.lzmaProps.btMode = Clamp(dictOptions.BinaryTreeMode.Value, 0, 1);
                if (dictOptions.HashBytes.HasValue) props.lzmaProps.numHashBytes = Clamp(dictOptions.HashBytes.Value, 2, 4);
                if (dictOptions.MatchCycles.HasValue)
                {
                    // match cycles minimum 1, clamp to reasonable upper bound
                    int mc = Clamp(dictOptions.MatchCycles.Value, 1, 1 << 30);
                    props.lzmaProps.mc = (uint)mc;
                }
                if (dictOptions.WriteEndMarker.HasValue) props.lzmaProps.writeEndMark = dictOptions.WriteEndMarker.Value ? 1u : 0u;

                // Ensure lc + lp is within native acceptable limits. Some native versions reject combinations where lc+lp is too large.
                // If both lc and lp were explicitly set and their sum exceeds 4, reduce lc so lc+lp == 4 to avoid SZ_ERROR_PARAM.
                if (props.lzmaProps.lc >= 0 && props.lzmaProps.lp >= 0)
                {
                    int sum = (int)props.lzmaProps.lc + (int)props.lzmaProps.lp;
                    if (sum > 4)
                    {
                        int newLc = Math.Max(0, 4 - (int)props.lzmaProps.lp);
                        props.lzmaProps.lc = newLc;
                    }
                }
            }
            // else: no dictOptions — keep fb and dictSize unset (-1/0) so native defaults apply

            // Determine LZMA2 blockSize behavior (unchanged): props.blockSize set relative to threads & blockSize param
            if (threads == 1 || blockSize == -1)
                props.blockSize = ulong.MaxValue;
            else if (blockSize == 0 && minBufferSize > 0)
                props.blockSize = (ulong)minBufferSize / (ulong)threads;
            else
                props.blockSize = (ulong)blockSize / (ulong)threads;

            _solid = props.blockSize == ulong.MaxValue;
            this.BlockSize = _solid && blockSize == 0 ? -1 : blockSize;

            // Validate and dump props on failure to help identify invalid parameter (SZ_ERROR_PARAM)
            try
            {
                int res = SZ_Lzma2_v25_01_Enc_SetProps(_encoder, ref props);
                if (res != 0)
                    throw new Exception($"Failed to set LZMA2 encoder config {res}. Props: {PropsToString(ref props)}");
            }
            catch (Exception ex)
            {
                // If native call itself throws or returns error, include prop dump
                throw new Exception($"SZ_Lzma2_v25_01_Enc_SetProps failed. {ex.Message}\nProps: {PropsToString(ref props)}", ex);
            }

            this.Properties = SZ_Lzma2_v25_01_Enc_WriteProperties(_encoder);

            long bufferSize = (_solid || this.BlockSize > int.MaxValue ? 0x400000L : this.BlockSize) + 0x8;

            _inBuffer = BufferPool.Rent(bufferSize);
            _inBufferPinned = GCHandle.Alloc(_inBuffer, GCHandleType.Pinned);

            _inStream = new CBufferInStream() { buffer = _inBufferPinned.AddrOfPinnedObject(), size = (ulong)bufferSize };
            _blkTotal = 0;

            SZ_Lzma2_v25_01_Enc_EncodeMultiCallPrepare(_encoder);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        private static string PropsToString(ref CLzma2EncProps p)
        {
            // Build a concise representation of the important fields that can cause SZ_ERROR_PARAM
            return $"lzmaProps.level={p.lzmaProps.level}, dictSize={p.lzmaProps.dictSize}, lc={p.lzmaProps.lc}, lp={p.lzmaProps.lp}, pb={p.lzmaProps.pb}, algo={p.lzmaProps.algo}, fb={p.lzmaProps.fb}, btMode={p.lzmaProps.btMode}, numHashBytes={p.lzmaProps.numHashBytes}, mc={p.lzmaProps.mc}, writeEndMark={p.lzmaProps.writeEndMark}, blockSize={p.blockSize}, numBlockThreads_Max={p.numBlockThreads_Max}, numTotalThreads={p.numTotalThreads}";
        }

        /// <summary>
        /// Encodes data from the input buffer into the output buffer using LZMA2 compression.
        /// </summary>
        /// <param name="inData">The input buffer containing data to compress.</param>
        /// <param name="outData">The output buffer to write compressed data to.</param>
        /// <param name="final">Indicates if this is the final block of data.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <returns>The total number of bytes written to the output buffer.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="inData"/> or <paramref name="outData"/> is not at the correct position.</exception>
        /// <exception cref="Exception">Thrown if compression fails.</exception>
        public int EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            // Lazy preparation: only prepare if we need to AND have data to process
            if (_needsInit && (inData.AvailableRead > 0 || final))
            {
                SZ_Lzma2_v25_01_Enc_EncodeMultiCallPrepare(_encoder);
                _needsInit = false;
            }

            if (_solid)
                return encodeDataSolid(inData, outData, final, cancel);
            else
                return encodeDataMt(inData, outData, final, cancel);
        }

        private int encodeDataMt(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            ulong outSz = (ulong)outData.AvailableWrite;
            uint available = (uint)inData.AvailableRead;
            fixed (byte* outPtr = outData.Data)
            fixed (byte* inPtr = inData.Data)
            {
                *&outPtr += outData.Size;
                *&inPtr += inData.Pos;
                int res = SZ_Lzma2_v25_01_Enc_Encode2(_encoder, outPtr, &outSz, inPtr, (ulong)inData.AvailableRead, IntPtr.Zero);

                outSz--; //remove the null terminator from block-based compression

                // Handle insufficient buffer error gracefully like LzmaEncoder
                if (res == -2147023537) // ERROR_INSUFFICIENT_BUFFER (0x8007054F)
                {
                    // Return partial result - this is normal for higher compression levels
                    outData.Write((int)outSz);
                    inData.Read(Math.Min(inData.AvailableRead, (int)available));
                    return (int)outSz;
                }

                outData.Write((int)outSz);

                if (res != 0)
                    throw new Exception($"Encode Error {res}");
            }

            inData.Read(inData.AvailableRead);

            return (int)outSz;
        }

        private int encodeDataSolid(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            long inTotal = 0;
            int res = 0;
            ulong outSz = 0;
            int outTotal = 0;
            bool finalfinal = false;
            bool blkFinal = false;

            while (inData.AvailableRead != 0 || final)
            {
                cancel.ThrowIfCancellationRequested();

                if (_inStream.pos == _inStream.size)
                    _inStream.pos = 0;

                int p = (int)((_inStream.pos + _inStream.remaining) % _inStream.size);

                int sz = (int)Math.Min(inData.AvailableRead, (long)Math.Min(_inStream.size - _inStream.remaining, (ulong)this.BlockSize - (ulong)_blkTotal));

                int endSz = (int)(_inStream.size - (ulong)p);
                inData.Read(_inBuffer, (int)p, (int)Math.Min(sz, endSz));

                // copy inData at start of circular _outBuffer
                if (sz > endSz)
                    inData.Read(_inBuffer, 0, (int)(sz - endSz));

                inTotal += sz;
                _blkTotal += sz;
                _inStream.remaining += (ulong)sz;

                finalfinal = final && inData.AvailableRead == 0 && _inStream.remaining == 0;
                blkFinal = this.BlockSize == _blkTotal;

                if (!final && !blkFinal && _inStream.remaining < _inStream.size)
                    break;

                long inSz = (long)_inStream.remaining;

                fixed (byte* outPtr = outData.Data)
                {
                    do
                    {
                        outSz = (ulong)outData.AvailableWrite;
                        byte* outPtr2 = *&outPtr + outData.Size;
                        _blockComplete = finalfinal || blkFinal;
                        res = SZ_Lzma2_v25_01_Enc_EncodeMultiCall(_encoder, outPtr2, &outSz, ref _inStream, 0u);
                        
                        // Handle insufficient buffer error gracefully
                        if (res == -2147023537) // ERROR_INSUFFICIENT_BUFFER (0x8007054F)
                        {
                            // Return partial result and let caller handle it
                            outTotal += (int)outSz;
                            outData.Write((int)outSz);
                            return outTotal;
                        }
                        
                        outTotal += (int)outSz;
                        outData.Write((int)outSz);
                    } while (res == 0 && outSz != 0 && (finalfinal || blkFinal));

                    if (blkFinal && !finalfinal)
                    {
                        _blkTotal = 0;
                        _needsInit = true;
                    }
                }

                if (inSz == 0 && outSz == 0)
                    break;

                if (res != 0)
                    throw new Exception($"Encode Error {res}");

                if (final)
                    break;
            }

            return outTotal;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="Lzma2Encoder"/>.
        /// </summary>
        public void Dispose()
        {
            if (!_blockComplete && _solid) //ONLY finalise SOLID mode!!!
            {
                if (_needsInit)
                {
                    SZ_Lzma2_v25_01_Enc_EncodeMultiCallPrepare(_encoder);
                    _needsInit = false;
                }

                byte[] dummy = new byte[1]; // Ensure we have space for EOF marker
                ulong outSize = 1;
                fixed (byte* d = dummy)
                {
                    int res = SZ_Lzma2_v25_01_Enc_EncodeMultiCall(_encoder, d, &outSize, ref _inStream, 0u);
                    if (res == 0) // Only set to complete if finalization succeeded
                        _blockComplete = true;
                }
            }

            if (_encoder != IntPtr.Zero)
            {
                SZ_Lzma2_v25_01_Enc_Destroy(_encoder);
                _encoder = IntPtr.Zero;
            }
            if (_inBufferPinned.IsAllocated)
                _inBufferPinned.Free();
            BufferPool.Return(_inBuffer);
        }
    }
}
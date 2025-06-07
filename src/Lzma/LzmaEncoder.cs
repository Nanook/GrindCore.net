using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop.Lzma;
using static Nanook.GrindCore.Interop;
using System.Linq;

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
        /// Initializes a new instance of the <see cref="LzmaEncoder"/> class with the specified compression parameters.
        /// </summary>
        /// <param name="level">The compression level to use (default is 5).</param>
        /// <param name="dictSize">The dictionary size to use (default is 0).</param>
        /// <param name="wordSize">The word size to use (default is 0).</param>
        /// <exception cref="Exception">Thrown if the encoder context or buffer cannot be allocated or configured.</exception>
        public LzmaEncoder(int level = 5, uint dictSize = 0, int wordSize = 0)
        {
            CLzmaEncProps props = new CLzmaEncProps();

            SZ_Lzma_v24_07_EncProps_Init(ref props);

            props.level = level;
            props.dictSize = props.mc = dictSize;
            props.lc = props.lp = props.pb = props.algo = props.fb = props.btMode = props.numHashBytes = props.numThreads = -1;
            props.numHashBytes = 0;
            props.writeEndMark = 0; // BytesFullSize == 0 ? 0u : 1u;
            props.affinity = 0;
            props.numThreads = 1;

            props.fb = wordSize; // default is 32 in UI

            props.reduceSize = ulong.MaxValue; // -1 means set to blocksize if blocksize not -1(solid)|0(auto) && blocksize<filesize

            _encoder = SZ_Lzma_v24_07_Enc_Create();

            int res = SZ_Lzma_v24_07_Enc_SetProps(_encoder, ref props); // normalizes properties
            if (res != 0)
                throw new Exception($"Failed to set LZMA2 encoder config {res}");

            byte[] p = BufferPool.Rent(0x10);
            ulong sz = (ulong)p.Length;

            fixed (byte* inPtr = p)
                SZ_Lzma_v24_07_Enc_WriteProperties(_encoder, inPtr, &sz);
            this.Properties = p.Take((int)sz).ToArray();
            BufferPool.Return(p);

            uint bufferSize = 0;
            uint dSize = 0;

            res = SZ_Lzma_v24_07_Enc_LzmaCodeMultiCallPrepare(_encoder, &bufferSize, &dSize, 0);
            if (res != 0)
                throw new Exception($"Failed to set LZMA2 encoder config {res}");

            this.BlockSize = (int)bufferSize;
            bufferSize += 0x8; // only needs 1 extra byte to ensure the end is not reached. Just align to 8

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

            while (inData.AvailableRead != 0 || (final && !finalfinal))
            {
                cancel.ThrowIfCancellationRequested();

                if (_inStream.pos == _inStream.size)
                    _inStream.pos = 0; // wrap around

                int p = (int)((_inStream.pos + _inStream.remaining) % _inStream.size);

                int sz = (int)Math.Min((ulong)inData.AvailableRead, Math.Min(_inStream.size - _inStream.remaining, (ulong)this.BlockSize));

                int endSz = (int)(_inStream.size - (ulong)p);
                inData.Read(_inBuffer, (int)p, (int)Math.Min(sz, endSz));

                // copy data at start of circular buffer
                if (sz > endSz)
                    inData.Read(_inBuffer, 0, (int)(sz - endSz));

                total += sz;
                _inStream.remaining += (ulong)sz;

                if (!final && _inStream.remaining < (ulong)this.BlockSize)
                    break;
                finalfinal = final && inData.AvailableRead == 0 && _toFlush == 0;

                outSz = (ulong)(outData.AvailableWrite);

                fixed (byte* outPtr = outData.Data)
                {
                    *&outPtr += outData.Size; // writePos is Size
                    res = SZ_Lzma_v24_07_Enc_LzmaCodeMultiCall(_encoder, outPtr, &outSz, ref _inStream, this.BlockSize, &available, finalfinal ? 1 : 0);
                    outTotal += (int)outSz;
                }
                _toFlush = available;
                outData.Write((int)outSz);

                if (res != 0)
                    throw new Exception($"Encode Error {res}");
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
                SZ_Lzma_v24_07_Enc_Destroy(_encoder);
                _encoder = IntPtr.Zero;
            }
            if (_inBufferPinned.IsAllocated)
                _inBufferPinned.Free();
            BufferPool.Return(_inBuffer);
        }
    }
}


using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop.Lzma;
using static Nanook.GrindCore.Interop;

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
        /// </summary>
        /// <param name="level">The compression level to use (default is 5).</param>
        /// <param name="threads">The number of threads to use (default is 1).</param>
        /// <param name="blockSize">The block size to use for compression (-1 for solid mode, 0 for auto, default is -1).</param>
        /// <param name="dictSize">The dictionary size to use (default is 0).</param>
        /// <param name="wordSize">The word size to use (default is 0).</param>
        /// <param name="minBufferSize">The minimum buffer size to use (default is 0).</param>
        /// <exception cref="Exception">Thrown if the encoder context or buffer cannot be allocated or configured.</exception>
        public Lzma2Encoder(int level = 5, int threads = 1, long blockSize = -1, int dictSize = 0, int wordSize = 0, int minBufferSize = 0)
        {
            if (threads <= 0)
                threads = 1;

            _encoder = SZ_Lzma2_v25_01_Enc_Create();
            if (_encoder == IntPtr.Zero)
                throw new Exception("Failed to create LZMA2 encoder.");

            // encoder already has props, replace them. Blank lc, lp etc to ensure they're recalculated from the level
            CLzma2EncProps props = new CLzma2EncProps();

            //init
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

            //config
            props.lzmaProps.level = level;
            props.lzmaProps.numThreads = -1;
            props.numBlockThreads_Max = threads;
            props.numBlockThreads_Reduced = -1;
            props.numTotalThreads = threads;
            props.numThreadGroups = 0; //new for 25.01

            if (threads == 1 || blockSize == -1)
                props.blockSize = ulong.MaxValue;
            else if (blockSize == 0 && minBufferSize > 0)
                props.blockSize = (ulong)minBufferSize / (ulong)threads;
            else
                props.blockSize = (ulong)blockSize / (ulong)threads;

            _solid = props.blockSize == ulong.MaxValue;
            this.BlockSize = _solid && blockSize == 0 ? -1 : blockSize;

            // Use a fixed statement to pass the struct to the function
            int res = SZ_Lzma2_v25_01_Enc_SetProps(_encoder, ref props);

            if (res != 0)
                throw new Exception($"Failed to set LZMA2 encoder config {res}");

            this.Properties = SZ_Lzma2_v25_01_Enc_WriteProperties(_encoder);

            long bufferSize = (_solid || this.BlockSize > int.MaxValue ? 0x400000L : this.BlockSize) + 0x8;

            _inBuffer = BufferPool.Rent(bufferSize);
            _inBufferPinned = GCHandle.Alloc(_inBuffer, GCHandleType.Pinned);

            _inStream = new CBufferInStream() { buffer = _inBufferPinned.AddrOfPinnedObject(), size = (ulong)bufferSize };
            _blkTotal = 0;

            SZ_Lzma2_v25_01_Enc_EncodeMultiCallPrepare(_encoder);
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

            if (_solid)
                return encodeDataSolid(inData, outData, final, cancel);
            else
                return encodeDataMt(inData, outData, final, cancel);
        }

        /// <summary>
        /// Encodes data using multi-threaded block mode.
        /// </summary>
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

                outSz--; //remove the null
                outData.Write((int)outSz);

                if (res != 0)
                    throw new Exception($"Encode Error {res}");
            }

            inData.Read(inData.AvailableRead);

            return (int)outSz;
        }

        /// <summary>
        /// Encodes data using pseudo-solid mode.
        /// </summary>
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
                        res = SZ_Lzma2_v25_01_Enc_EncodeMultiCall(_encoder, outPtr2, &outSz, ref _inStream, 0u, _blockComplete ? 1u : 0u);
                        outTotal += (int)outSz;
                        outData.Write((int)outSz);
                    } while (res == 0 && outSz != 0 && (finalfinal || blkFinal));

                    if (blkFinal && !finalfinal)
                    {
                        SZ_Lzma2_v25_01_Enc_EncodeMultiCallPrepare(_encoder);
                        _blkTotal = 0;
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
                byte[] dummy = new byte[0];
                ulong zero = 0;
                fixed (byte* d = dummy)
                    SZ_Lzma2_v25_01_Enc_EncodeMultiCall(_encoder, d, &zero, ref _inStream, 0u, 1u);
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

using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.ZStd;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides an encoder for Zstandard (ZStd) compressed data, supporting streaming compression.
    /// </summary>
    internal unsafe class ZStdEncoder : IDisposable
    {
        private SZ_ZStd_v1_5_6_CompressionContext _context;
        private byte[] _outputBuffer;
        private GCHandle _outputPinned;
        private IntPtr _outputPtr;
        private int _compressionLevel;

        /// <summary>
        /// Gets the recommended input buffer size for ZStd compression.
        /// </summary>
        public int InputBufferSize { get; }

        /// <summary>
        /// Gets the recommended output buffer size for ZStd compression.
        /// </summary>
        public int OutputBufferSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdEncoder"/> class with the specified block size and compression level.
        /// </summary>
        /// <param name="blockSize">The block size to use for compression.</param>
        /// <param name="compressionLevel">The compression level to use (default is 3).</param>
        /// <exception cref="Exception">Thrown if the compression context cannot be created or configured.</exception>
        public ZStdEncoder(int blockSize, int compressionLevel = 3)
        {
            _compressionLevel = compressionLevel;

            // Get recommended buffer sizes

            _context = new SZ_ZStd_v1_5_6_CompressionContext();

            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
            {
                if (SZ_ZStd_v1_5_6_CreateCompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create Zstd compression context");

                int ret = SZ_ZStd_v1_5_6_SetCompressionLevel(ctxPtr, _compressionLevel);
                int ret2 = SZ_ZStd_v1_5_6_SetBlockSize(ctxPtr, (nuint)blockSize);
            }

            InputBufferSize = (int)SZ_ZStd_v1_5_6_CStreamInSize();
            OutputBufferSize = (int)SZ_ZStd_v1_5_6_CStreamOutSize();

            // Allocate and pin buffers
            _outputBuffer = BufferPool.Rent(OutputBufferSize);

            _outputPinned = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
            _outputPtr = _outputPinned.AddrOfPinnedObject();
        }

        /// <summary>
        /// Encodes data from the input buffer into the output buffer using ZStd compression.
        /// </summary>
        /// <param name="inData">The input buffer containing data to compress.</param>
        /// <param name="outData">The output buffer to write compressed data to.</param>
        /// <param name="final">Indicates if this is the final block of data.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <returns>The total number of bytes written to the output buffer.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="inData"/> or <paramref name="outData"/> is not at the correct position.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        /// <exception cref="Exception">Thrown if compression fails or more data needs to be flushed.</exception>
        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            int totalCompressed = 0;

            while (inData.AvailableRead > 0 || final)
            {
                cancel.ThrowIfCancellationRequested();

                int srcCapacity = Math.Min(inData.AvailableRead, InputBufferSize);
                long inSize;
                long outSize;

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
                {
                    *&inputPtr += inData.Pos;

                    UIntPtr toFlush = SZ_ZStd_v1_5_6_CompressStream(
                        ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize,
                        inputPtr, (UIntPtr)srcCapacity,
                        out inSize, out outSize);

                    if (toFlush != UIntPtr.Zero)
                        throw new Exception("TODO: ZStd compression has more to flush");

                    inData.Read((int)inSize);
                    outData.Write(_outputBuffer, 0, (int)outSize);
                    totalCompressed += (int)outSize;
                    final = false;
                }
            }

            return totalCompressed;
        }

        /// <summary>
        /// Flushes any remaining compressed data to the output buffer and finalizes the compression stream.
        /// </summary>
        /// <param name="outData">The output buffer to write flushed data to.</param>
        /// <returns>The total number of bytes flushed and finalized.</returns>
        /// <exception cref="Exception">Thrown if flushing or finalizing the compression stream fails.</exception>
        public long Flush(CompressionBuffer outData)
        {
            long flushedSize = 0;
            long endSize;
            byte[] buff = new byte[1];
            long inSize = 0;

            fixed (byte* inputPtr = buff)
            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
            {
                UIntPtr res = SZ_ZStd_v1_5_6_FlushStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out flushedSize);

                if (res != UIntPtr.Zero)
                    throw new Exception("ZStd flush failed");

                outData.Write(_outputBuffer, 0, (int)flushedSize);

                res = SZ_ZStd_v1_5_6_EndStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out endSize);

                if (res != UIntPtr.Zero)
                    throw new Exception("Failed to finalize Zstd compression");

                outData.Write(_outputBuffer, 0, (int)endSize);

                return flushedSize + endSize;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="ZStdEncoder"/>.
        /// </summary>
        public void Dispose()
        {
            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
            {
                SZ_ZStd_v1_5_6_FreeCompressionContext(ctxPtr);
            }

            if (_outputPinned.IsAllocated)
                try { _outputPinned.Free(); } catch { }

            BufferPool.Return(_outputBuffer);
        }
    }
}

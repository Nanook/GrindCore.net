using System;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.ZStd;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a decoder for Zstandard (ZStd) compressed data, supporting streaming decompression.
    /// </summary>
    internal unsafe class ZStdDecoder : IDisposable
    {
        private SZ_ZStd_v1_5_6_DecompressionContext _context;

        /// <summary>
        /// Gets the recommended input buffer size for ZStd decompression.
        /// </summary>
        public int InputBufferSize { get; }

        /// <summary>
        /// Gets the recommended output buffer size for ZStd decompression.
        /// </summary>
        public int OutputBufferSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdDecoder"/> class and creates a decompression context.
        /// </summary>
        /// <exception cref="Exception">Thrown if the decompression context cannot be created.</exception>
        public ZStdDecoder()
        {
            // Get recommended buffer sizes
            InputBufferSize = (int)SZ_ZStd_v1_5_6_CStreamInSize();
            OutputBufferSize = (int)SZ_ZStd_v1_5_6_CStreamOutSize() * 0x2;

            _context = new SZ_ZStd_v1_5_6_DecompressionContext();

            fixed (SZ_ZStd_v1_5_6_DecompressionContext* ctxPtr = &_context)
            {
                if (SZ_ZStd_v1_5_6_CreateDecompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create Zstd decompression context");
            }
        }

        /// <summary>
        /// Decodes ZStd-compressed data from the input buffer into the output buffer.
        /// </summary>
        /// <param name="inData">The input buffer containing compressed data.</param>
        /// <param name="readSz">Outputs the number of bytes read from the input buffer.</param>
        /// <param name="outData">The output buffer to write decompressed data to.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <returns>The total number of bytes written to the output buffer.</returns>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        public long DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData, CancellableTask cancel)
        {
            outData.Tidy(); //ensure all the space is at the end making _buffer.AvailableWrite safe for interop

            int totalDecompressed = 0;
            readSz = 0;
            cancel.ThrowIfCancellationRequested();

            int srcCapacity = Math.Min(inData.AvailableRead, InputBufferSize);
            long inSize;
            long outSize;

            fixed (byte* inputPtr = inData.Data)
            fixed (byte* outputPtr = outData.Data)
            fixed (SZ_ZStd_v1_5_6_DecompressionContext* ctxPtr = &_context)
            {
                *&inputPtr += inData.Pos;
                *&outputPtr += outData.Pos;

                UIntPtr toFlush = SZ_ZStd_v1_5_6_DecompressStream(
                    ctxPtr, outputPtr, (UIntPtr)outData.AvailableWrite,
                    inputPtr, (UIntPtr)srcCapacity, out inSize, out outSize);

                //if (toFlush != UIntPtr.Zero)
                //    throw new Exception("TODO: ZStd decompression has more to flush");

                inData.Read((int)inSize);
                readSz += (int)inSize;
                outData.Write((int)outSize);
                totalDecompressed += (int)outSize;
            }

            return totalDecompressed;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="ZStdDecoder"/>.
        /// </summary>
        public void Dispose()
        {
            fixed (SZ_ZStd_v1_5_6_DecompressionContext* ctxPtr = &_context)
            {
                SZ_ZStd_v1_5_6_FreeDecompressionContext(ctxPtr);
            }
        }
    }
}


using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.ZStd;

namespace Nanook.GrindCore.ZStd
{
    internal unsafe class ZStdDecoder : IDisposable
    {
        private SZ_ZStd_v1_5_6_DecompressionContext _context;

        public int InputBufferSize { get; }
        public int OutputBufferSize { get; }

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

        public long DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData, CancellableTask cancel)
        {
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

        public void Dispose()
        {
            fixed (SZ_ZStd_v1_5_6_DecompressionContext* ctxPtr = &_context)
            {
                SZ_ZStd_v1_5_6_FreeDecompressionContext(ctxPtr);
            }

        }
    }
}
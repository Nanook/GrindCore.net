using System;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a decoder for Zstandard (ZStd) compressed data, supporting streaming decompression.
    /// This class implements the latest ZStd version (1.5.7) directly.
    /// For older versions (e.g., 1.5.2), use <see cref="ZStdDecoderV1_5_2"/>, which inherits from this class and overrides only the version-specific logic.
    /// </summary>
    internal unsafe class ZStdDecoder : IDisposable
    {
        protected SZ_ZStd_v1_5_7_DecompressionContext _ctx;
        public int InputBufferSize { get; protected set; }
        public int OutputBufferSize { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdDecoder"/> class and creates a decompression context for ZStd v1.5.7.
        /// </summary>
        public ZStdDecoder()
        {
            InputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_7_CStreamInSize();
            OutputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_7_CStreamOutSize() * 0x2;
            _ctx = new SZ_ZStd_v1_5_7_DecompressionContext();

            fixed (SZ_ZStd_v1_5_7_DecompressionContext* ctxPtr = &_ctx)
            {
                if (Interop.ZStd.SZ_ZStd_v1_5_7_CreateDecompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create Zstd v1.5.7 decompression context");
            }
        }

        /// <summary>
        /// Decodes ZStd-compressed data from the input buffer into the output buffer.
        /// </summary>
        public virtual long DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData, CancellableTask cancel)
        {
            outData.Tidy();

            int totalDecompressed = 0;
            readSz = 0;
            cancel.ThrowIfCancellationRequested();

            int srcCapacity = Math.Min(inData.AvailableRead, InputBufferSize);
            long inSize;
            long outSize;

            fixed (byte* inputPtr = inData.Data)
            fixed (byte* outputPtr = outData.Data)
            fixed (SZ_ZStd_v1_5_7_DecompressionContext* ctxPtr = &_ctx)
            {
                *&inputPtr += inData.Pos;
                *&outputPtr += outData.Size;

                UIntPtr toFlush = Interop.ZStd.SZ_ZStd_v1_5_7_DecompressStream(
                    ctxPtr, outputPtr, (UIntPtr)outData.AvailableWrite,
                    inputPtr, (UIntPtr)srcCapacity, out inSize, out outSize);

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
        public virtual void Dispose()
        {
            fixed (SZ_ZStd_v1_5_7_DecompressionContext* ctxPtr = &_ctx)
            {
                Interop.ZStd.SZ_ZStd_v1_5_7_FreeDecompressionContext(ctxPtr);
            }
        }
    }

    /// <summary>
    /// Provides a decoder for Zstandard (ZStd) compressed data using version 1.5.2.
    /// Inherits from <see cref="ZStdDecoder"/> and overrides only the version-specific logic.
    /// </summary>
    internal unsafe class ZStdDecoderV1_5_2 : ZStdDecoder
    {
        private SZ_ZStd_v1_5_2_DecompressionContext _ctx152;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdDecoderV1_5_2"/> class and creates a decompression context for ZStd v1.5.2.
        /// </summary>
        public ZStdDecoderV1_5_2()
        {
            InputBufferSize = (int)Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CStreamInSize();
            OutputBufferSize = (int)Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CStreamOutSize() * 0x2;
            _ctx152 = new SZ_ZStd_v1_5_2_DecompressionContext();

            fixed (SZ_ZStd_v1_5_2_DecompressionContext* ctxPtr = &_ctx152)
            {
                if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateDecompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create Zstd v1.5.2 decompression context");
            }
        }

        public override long DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData, CancellableTask cancel)
        {
            outData.Tidy();

            int totalDecompressed = 0;
            readSz = 0;
            cancel.ThrowIfCancellationRequested();

            int srcCapacity = Math.Min(inData.AvailableRead, InputBufferSize);
            long inSize;
            long outSize;

            fixed (byte* inputPtr = inData.Data)
            fixed (byte* outputPtr = outData.Data)
            fixed (SZ_ZStd_v1_5_2_DecompressionContext* ctxPtr = &_ctx152)
            {
                *&inputPtr += inData.Pos;
                *&outputPtr += outData.Size;

                UIntPtr toFlush = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_DecompressStream(
                    ctxPtr, outputPtr, (UIntPtr)outData.AvailableWrite,
                    inputPtr, (UIntPtr)srcCapacity, out inSize, out outSize);

                inData.Read((int)inSize);
                readSz += (int)inSize;
                outData.Write((int)outSize);
                totalDecompressed += (int)outSize;
            }

            return totalDecompressed;
        }

        public override void Dispose()
        {
            fixed (SZ_ZStd_v1_5_2_DecompressionContext* ctxPtr = &_ctx152)
            {
                Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeDecompressionContext(ctxPtr);
            }
        }
    }
}
using System;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a decoder for Zstandard (ZStd) compressed data, supporting streaming decompression.
    /// </summary>
    internal unsafe class ZStdDecoder : IDisposable
    {
        private SZ_ZStd_v1_5_6_DecompressionContext _ctx156;
        private SZ_ZStd_v1_5_2_DecompressionContext _ctx152;
        private readonly int _versionIndex;
        public int InputBufferSize { get; }
        public int OutputBufferSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdDecoder"/> class and creates a decompression context.
        /// </summary>
        /// <param name="version">The compression version to use (null or 0 = latest, 1 = v1.5.2).</param>
        /// <exception cref="Exception">Thrown if the decompression context cannot be created.</exception>
        public ZStdDecoder(CompressionVersion? version = null)
        {
            _versionIndex = version?.Index ?? 0;

            if (_versionIndex == 0)
            {
                InputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_6_CStreamInSize();
                OutputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_6_CStreamOutSize() * 0x2;
                _ctx156 = new SZ_ZStd_v1_5_6_DecompressionContext();

                fixed (SZ_ZStd_v1_5_6_DecompressionContext* ctxPtr = &_ctx156)
                {
                    if (Interop.ZStd.SZ_ZStd_v1_5_6_CreateDecompressionContext(ctxPtr) < 0)
                        throw new Exception("Failed to create Zstd v1.5.6 decompression context");
                }
            }
            else // v1.5.2
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
        }

        /// <summary>
        /// Decodes ZStd-compressed data from the input buffer into the output buffer.
        /// </summary>
        public long DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData, CancellableTask cancel)
        {
            outData.Tidy();

            int totalDecompressed = 0;
            readSz = 0;
            cancel.ThrowIfCancellationRequested();

            int srcCapacity = Math.Min(inData.AvailableRead, InputBufferSize);
            long inSize;
            long outSize;

            if (_versionIndex == 0)
            {
                fixed (byte* inputPtr = inData.Data)
                fixed (byte* outputPtr = outData.Data)
                fixed (SZ_ZStd_v1_5_6_DecompressionContext* ctxPtr = &_ctx156)
                {
                    *&inputPtr += inData.Pos;
                    *&outputPtr += outData.Size;

                    UIntPtr toFlush = Interop.ZStd.SZ_ZStd_v1_5_6_DecompressStream(
                        ctxPtr, outputPtr, (UIntPtr)outData.AvailableWrite,
                        inputPtr, (UIntPtr)srcCapacity, out inSize, out outSize);

                    inData.Read((int)inSize);
                    readSz += (int)inSize;
                    outData.Write((int)outSize);
                    totalDecompressed += (int)outSize;
                }
            }
            else // v1.5.2
            {
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
            }

            return totalDecompressed;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="ZStdDecoder"/>.
        /// </summary>
        public void Dispose()
        {
            if (_versionIndex == 0)
            {
                fixed (SZ_ZStd_v1_5_6_DecompressionContext* ctxPtr = &_ctx156)
                {
                    Interop.ZStd.SZ_ZStd_v1_5_6_FreeDecompressionContext(ctxPtr);
                }
            }
            else
            {
                fixed (SZ_ZStd_v1_5_2_DecompressionContext* ctxPtr = &_ctx152)
                {
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeDecompressionContext(ctxPtr);
                }
            }
        }
    }
}
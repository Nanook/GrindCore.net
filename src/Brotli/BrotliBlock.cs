using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Brotli;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>
    /// Provides a Brotli compression block for compressing and decompressing data using the Brotli algorithm.
    /// </summary>
    public class BrotliBlock : CompressionBlock
    {
        private const int WindowBits_Min = 10;
        private const int WindowBits_Default = 22;
        private const int WindowBits_Max = 24;

        private readonly int _windowBits;

        /// <summary>
        /// Gets the required output buffer size for compression, including Brotli overhead.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrotliBlock"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        public BrotliBlock(CompressionOptions options) : base(CompressionAlgorithm.Brotli, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.BlockSize == null)
                throw new ArgumentNullException(nameof(options.BlockSize));

            int blockSize = (int)options.BlockSize!;

            // Read window bits from options.Dictionary when present; clamp to valid Brotli range.
            int wb = options?.Dictionary?.WindowBits ?? WindowBits_Default;
            if (wb < WindowBits_Min)
                wb = WindowBits_Min;
            if (wb > WindowBits_Max)
                wb = WindowBits_Max;
            _windowBits = wb;

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x10; // Adjust for overhead
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using Brotli.
        /// </summary>
        /// <param name="srcData">The source data block to compress.</param>
        /// <param name="dstData">The destination data block to write compressed data to.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal unsafe override CompressionResultCode OnCompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                UIntPtr compressedSize = (UIntPtr)dstCount;
                BOOL success = DN9_BRT_v1_1_0_BrotliEncoderCompress(
                    (int)this.CompressionType, // level (resolved by base/constructor)
                    _windowBits,               // LGWin from options.Dictionary or default
                    0,
                    (UIntPtr)srcData.Length,
                    srcPtr,
                    &compressedSize,
                    dstPtr
                );

                if (success == BOOL.FALSE)
                {
                    dstCount = 0;
                    return mapResult(0);
                }

                dstCount = (int)compressedSize;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using Brotli.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal unsafe override CompressionResultCode OnDecompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                UIntPtr srcSize = (UIntPtr)srcData.Length;
                UIntPtr decompressedSize = (UIntPtr)dstCount;

                BOOL success = DN9_BRT_v1_1_0_BrotliDecoderDecompress(srcSize, srcPtr, &decompressedSize, dstPtr);

                if (success == BOOL.FALSE)
                {
                    dstCount = 0;
                    return mapResult(0);
                }

                dstCount = (int)decompressedSize;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Releases resources used by the Brotli encoder.
        /// </summary>
        internal override void OnDispose()
        {
            // nothing to dispose at block-level now
        }

        private static CompressionResultCode mapResult(int code)
        {
            return code switch
            {
                1 => CompressionResultCode.Success, // BROTLI_TRUE, BROTLI_DECODER_RESULT_SUCCESS
                0 => CompressionResultCode.Error,   // BROTLI_FALSE, BROTLI_DECODER_RESULT_ERROR
                2 => CompressionResultCode.InsufficientBuffer, // BROTLI_DECODER_RESULT_NEEDS_MORE_INPUT
                3 => CompressionResultCode.InsufficientBuffer, // BROTLI_DECODER_RESULT_NEEDS_MORE_OUTPUT
                _ => CompressionResultCode.Error
            };
        }
    }
}

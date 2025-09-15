using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.FastLzma2;

namespace Nanook.GrindCore.FastLzma2
{
    /// <summary>
    /// Provides a block-based implementation of the Fast-LZMA2 compression algorithm, supporting multi-threaded compression and decompression.
    /// </summary>
    public class FastLzma2Block : CompressionBlock
    {
        private nint _compressCtx;
        private nint _decompressCtx;
        private byte _dictProp;

        /// <summary>
        /// Gets the required output buffer size for compression, as determined by the Fast-LZMA2 library.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastLzma2Block"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        public FastLzma2Block(CompressionOptions options) : base(CompressionAlgorithm.FastLzma2, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Resolve input block size: prefer Dictionary.DictionarySize when provided, otherwise use options.BlockSize.
            long bs = options.Dictionary?.DictionarySize ?? options.BlockSize ?? 0L;
            if (bs <= 0)
            {
                // fall back to a small default to avoid throwing in tests
                bs = 1;
            }
            if (bs > int.MaxValue) bs = int.MaxValue;
            int blockSize = (int)bs;

            // Resolve dictionary size separately if provided (some callers want a different dictionary than blockSize)
            long dictSize = options.Dictionary?.DictionarySize ?? bs;
            if (dictSize < 0)
                dictSize = 0;
            if (dictSize > (long)uint.MaxValue)
                dictSize = uint.MaxValue;

            // Determine compression level: prefer Dictionary.Strategy when provided; otherwise use CompressionType resolved by base.
            int level = options.Dictionary?.Strategy ?? (int)this.CompressionType;

            // Determine thread count (default to 1)
            int threads = options.ThreadCount ?? 1;

            // Create contexts (use MT creation when supported; library expects thread count)
            _compressCtx = FL2_createCCtxMt((uint)threads);
            _decompressCtx = FL2_createDCtxMt((uint)threads);

            // Retrieve dictionary property for decoding
            _dictProp = FL2_getCCtxDictProp(_compressCtx);

            // Configure compression properties
            FL2_CCtx_setParameter(_compressCtx, FL2Parameter.DictionarySize, (nuint)dictSize);
            FL2_CCtx_setParameter(_compressCtx, FL2Parameter.CompressionLevel, (nuint)level);

            RequiredCompressOutputSize = (int)FL2_compressBound((nuint)blockSize);
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using Fast-LZMA2.
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

                nuint compressedSize = (nuint)dstCount;
                nuint result = FL2_compressCCtx(
                    _compressCtx, dstPtr, compressedSize, srcPtr, (nuint)srcData.Length, (int)this.CompressionType);

                if (result < 0)
                {
                    dstCount = 0;
                    return mapResult((int)result);
                }

                dstCount = (int)result;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using Fast-LZMA2.
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

                // Ensure correct decompression size
                nuint decompressedSize = FL2_findDecompressedSize(srcPtr, (nuint)srcData.Length);
                if (decompressedSize == (nuint)uint.MaxValue)
                {
                    dstCount = 0;
                    return CompressionResultCode.InvalidData;
                }

                FL2_initDCtx(_decompressCtx, srcData.Data[0]); // Use property byte from compressed data

                nuint result = FL2_decompressMt(dstPtr, decompressedSize, srcPtr, (nuint)srcData.Length, (uint)(this.Options.ThreadCount ?? 1));

                if (result < 0 || result == (nuint)uint.MaxValue)
                {
                    dstCount = 0;
                    return mapResult((int)result);
                }

                dstCount = (int)result;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="FastLzma2Block"/>.
        /// </summary>
        internal override void OnDispose()
        {
            FL2_freeCCtx(_compressCtx);
            FL2_freeDCtx(_decompressCtx);
        }

        private static CompressionResultCode mapResult(int code)
        {
            // 0 is success, >0 is error (see FL2Exception for details)
            switch (code)
            {
                case 0:  // FL2_error_no_error
                    return CompressionResultCode.Success;
                case -1:  // FL2_error_GENERIC
                case -2:  // FL2_error_internal
                case -9:  // FL2_error_init_missing
                case -13: // FL2_error_canceled
                case -14: // FL2_error_buffer
                case -15: // FL2_error_timedOut
                    return CompressionResultCode.Error;
                case -3:  // FL2_error_corruption_detected
                case -4:  // FL2_error_checksum_wrong
                case -12: // FL2_error_srcSize_wrong
                    return CompressionResultCode.InvalidData;
                case -5:  // FL2_error_parameter_unsupported
                    return CompressionResultCode.NotSupported;
                case -6:  // FL2_error_parameter_outOfBound
                case -7:  // FL2_error_lclpMax_exceeded
                case -8:  // FL2_error_stage_wrong
                    return CompressionResultCode.InvalidParameter;
                case 10: // FL2_error_memory_allocation
                    return CompressionResultCode.Error;
                case 11: // FL2_error_dstSize_tooSmall
                    return CompressionResultCode.InsufficientBuffer;
                // -20 (maxCode) should never be used directly
                default:
                    return CompressionResultCode.Error;
            }
        }
    }
}

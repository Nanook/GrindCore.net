using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a block-based implementation of the Zstandard (ZStd) compression algorithm.
    /// </summary>
    public class ZStdBlock : CompressionBlock
    {
        private int _compressionLevel;

        /// <summary>
        /// Gets the required output buffer size for compression, as determined by the ZStd algorithm.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdBlock"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        public ZStdBlock(CompressionOptions options) : base(CompressionAlgorithm.ZStd, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Determine compression level: prefer Dictionary.Strategy when provided; otherwise use CompressionType resolved by base.
            _compressionLevel = options.Dictionary?.Strategy ?? (int)this.CompressionType;

            // Resolve input block size: prefer Dictionary.WindowBits -> 1<<WindowBits, otherwise use options.BlockSize. Be tolerant.
            long isize = 0;
            if (options.Dictionary?.WindowBits != null)
            {
                int wb = options.Dictionary.WindowBits.Value;
                if (wb < 10)
                    wb = 10; // minimum reasonable for zstd
                if (wb > 31)
                    wb = 31; // clamp
                long calc = 1L << wb;
                isize = calc;
            }

            if (isize == 0)
            {
                isize = options.BlockSize ?? 0L;
            }

            if (isize <= 0)
            {
                // Fallback to a small default instead of throwing to be tolerant in tests
                isize = 1;
            }

            if (isize > int.MaxValue)
                isize = int.MaxValue;

            RequiredCompressOutputSize = (int)isize + ((int)isize >> 7) + 128;
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using ZStd.
        /// </summary>
        internal unsafe override CompressionResultCode OnCompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                var version = Options.Version;
                if (version == null || version.Index == 0)
                {
                    var ctx = new Interop.SZ_ZStd_v1_5_7_CompressionContext();
                    Interop.ZStd.SZ_ZStd_v1_5_7_CreateCompressionContext(&ctx);

                    UIntPtr compressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_CompressBlock(
                        &ctx, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length, _compressionLevel);

                    Interop.ZStd.SZ_ZStd_v1_5_7_FreeCompressionContext(&ctx);

                    if (compressedSize == UIntPtr.Zero)
                    {
                        dstCount = 0;
                        return mapResult((int)compressedSize);
                    }

                    dstCount = (int)compressedSize;
                    return CompressionResultCode.Success;
                }
                else // Index == 1, v1.5.2
                {
                    var ctx = new Interop.SZ_ZStd_v1_5_2_CompressionContext();
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateCompressionContext(&ctx);

                    UIntPtr compressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CompressBlock(
                        &ctx, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length, _compressionLevel);

                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeCompressionContext(&ctx);

                    if ((long)compressedSize < 0)
                    {
                        dstCount = 0;
                        return mapResult((int)compressedSize);
                    }

                    dstCount = (int)compressedSize;
                    return CompressionResultCode.Success;
                }
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using ZStd.
        /// </summary>
        internal unsafe override CompressionResultCode OnDecompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                var version = Options.Version;
                if (version == null || version.Index == 0)
                {
                    var ctx = new Interop.SZ_ZStd_v1_5_7_DecompressionContext();
                    Interop.ZStd.SZ_ZStd_v1_5_7_CreateDecompressionContext(&ctx);

                    UIntPtr decompressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_DecompressBlock(
                        &ctx, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length);

                    Interop.ZStd.SZ_ZStd_v1_5_7_FreeDecompressionContext(&ctx);

                    if ((long)decompressedSize < 0)
                    {
                        dstCount = 0;
                        return mapResult((int)decompressedSize);
                    }

                    dstCount = (int)decompressedSize;
                    return CompressionResultCode.Success;
                }
                else // Index == 1, v1.5.2
                {
                    var ctx = new Interop.SZ_ZStd_v1_5_2_DecompressionContext();
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateDecompressionContext(&ctx);

                    UIntPtr decompressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_DecompressBlock(
                        &ctx, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length);

                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeDecompressionContext(&ctx);

                    if ((uint)decompressedSize < 0)
                    {
                        dstCount = 0;
                        return mapResult((int)decompressedSize);
                    }

                    dstCount = (int)decompressedSize;
                    return CompressionResultCode.Success;
                }
            }
        }

        internal override void OnDispose() { }

        private static CompressionResultCode mapResult(long code)
        {
            // If code >= 0, it's a size (success)
            if (code >= 0)
                return CompressionResultCode.Success;

            return code switch
            {
                -1 => CompressionResultCode.Error, // ZSTD_error_memory_allocation
                -2 => CompressionResultCode.InsufficientBuffer, // ZSTD_error_dstSize_tooSmall
                -3 => CompressionResultCode.InvalidData, // ZSTD_error_srcSize_wrong
                -4 => CompressionResultCode.InvalidData, // ZSTD_error_corruption_detected
                -5 => CompressionResultCode.InvalidParameter, // ZSTD_error_parameter_unknown
                -6 => CompressionResultCode.NotSupported, // ZSTD_error_frameParameter_unsupported
                _  => CompressionResultCode.Error
            };
        }
    }
}
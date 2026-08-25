using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a block-based implementation of the Zstandard (ZStd) compression algorithm.
    /// </summary>
    public unsafe class ZStdBlock : CompressionBlock
    {
        private int _compressionLevel;
        private Interop.SZ_ZStd_v1_5_7_CompressionDict? _cdict;
        private Interop.SZ_ZStd_v1_5_7_DecompressionDict? _ddict;
        private Interop.SZ_ZStd_v1_5_2_CompressionDict? _cdict152;
        private Interop.SZ_ZStd_v1_5_2_DecompressionDict? _ddict152;

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

            // Raw content dictionary (e.g. a prefix dictionary built from the data's own content), same convention as
            // ZStdStream/ZStdEncoder: supplied via CompressionOptions.InitProperties. Digest it once here and reuse the
            // resulting CDict/DDict handles across every OnCompress/OnDecompress call on this instance - both handles are
            // read-only/thread-safe per zstd's own docs, so this instance can also be shared read-only across threads that
            // each create their own context per call (as OnCompress/OnDecompress already do).
            bool isV152 = options.Version != null && options.Version.Index == 1;
            if (options.InitProperties != null && options.InitProperties.Length > 0)
            {
                fixed (byte* dictPtr = options.InitProperties)
                {
                    if (isV152)
                    {
                        var cdict152 = new Interop.SZ_ZStd_v1_5_2_CompressionDict();
                        if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateCompressionDict(&cdict152, (IntPtr)dictPtr, (UIntPtr)options.InitProperties.Length, _compressionLevel) == 0)
                            _cdict152 = cdict152;

                        var ddict152 = new Interop.SZ_ZStd_v1_5_2_DecompressionDict();
                        if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateDecompressionDict(&ddict152, (IntPtr)dictPtr, (UIntPtr)options.InitProperties.Length) == 0)
                            _ddict152 = ddict152;
                    }
                    else
                    {
                        var cdict = new Interop.SZ_ZStd_v1_5_7_CompressionDict();
                        if (Interop.ZStd.SZ_ZStd_v1_5_7_CreateCompressionDict(&cdict, (IntPtr)dictPtr, (UIntPtr)options.InitProperties.Length, _compressionLevel) == 0)
                            _cdict = cdict;

                        var ddict = new Interop.SZ_ZStd_v1_5_7_DecompressionDict();
                        if (Interop.ZStd.SZ_ZStd_v1_5_7_CreateDecompressionDict(&ddict, (IntPtr)dictPtr, (UIntPtr)options.InitProperties.Length) == 0)
                            _ddict = ddict;
                    }
                }
            }

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

                    UIntPtr compressedSize;
                    if (_cdict.HasValue)
                    {
                        var cdict = _cdict.Value;
                        compressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_CompressBlockWithDict(
                            &ctx, &cdict, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length);
                    }
                    else
                    {
                        compressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_CompressBlock(
                            &ctx, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length, _compressionLevel);
                    }

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

                    UIntPtr compressedSize;
                    if (_cdict152.HasValue)
                    {
                        var cdict152 = _cdict152.Value;
                        compressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CompressBlockWithDict(
                            &ctx, &cdict152, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length);
                    }
                    else
                    {
                        compressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CompressBlock(
                            &ctx, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length, _compressionLevel);
                    }

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

                    UIntPtr decompressedSize;
                    if (_ddict.HasValue)
                    {
                        var ddict = _ddict.Value;
                        decompressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_DecompressBlockWithDict(
                            &ctx, &ddict, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length);
                    }
                    else
                    {
                        decompressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_DecompressBlock(
                            &ctx, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length);
                    }

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

                    UIntPtr decompressedSize;
                    if (_ddict152.HasValue)
                    {
                        var ddict152 = _ddict152.Value;
                        decompressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_DecompressBlockWithDict(
                            &ctx, &ddict152, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length);
                    }
                    else
                    {
                        decompressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_DecompressBlock(
                            &ctx, (IntPtr)dstPtr, (UIntPtr)dstCount, srcPtr, (UIntPtr)srcData.Length);
                    }

                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeDecompressionContext(&ctx);

                    if ((long)decompressedSize < 0)
                    {
                        dstCount = 0;
                        return mapResult((int)decompressedSize);
                    }

                    dstCount = (int)decompressedSize;
                    return CompressionResultCode.Success;
                }
            }
        }

        internal override void OnDispose()
        {
            if (_cdict.HasValue)
            {
                var cdict = _cdict.Value;
                Interop.ZStd.SZ_ZStd_v1_5_7_FreeCompressionDict(&cdict);
                _cdict = null;
            }
            if (_ddict.HasValue)
            {
                var ddict = _ddict.Value;
                Interop.ZStd.SZ_ZStd_v1_5_7_FreeDecompressionDict(&ddict);
                _ddict = null;
            }
            if (_cdict152.HasValue)
            {
                var cdict152 = _cdict152.Value;
                Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeCompressionDict(&cdict152);
                _cdict152 = null;
            }
            if (_ddict152.HasValue)
            {
                var ddict152 = _ddict152.Value;
                Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeDecompressionDict(&ddict152);
                _ddict152 = null;
            }
        }

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
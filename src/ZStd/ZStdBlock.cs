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
            _compressionLevel = (int)this.CompressionType;
            int isize = (int)options.BlockSize!;
            RequiredCompressOutputSize = isize + (isize >> 7) + 128;
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using ZStd.
        /// </summary>
        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                var version = Options.Version;
                if (version == null || version.Index == 0)
                {
                    var ctx = new Interop.SZ_ZStd_v1_5_6_CompressionContext();
                    Interop.ZStd.SZ_ZStd_v1_5_6_CreateCompressionContext(&ctx);

                    UIntPtr compressedSize = Interop.ZStd.SZ_ZStd_v1_5_6_CompressBlock(
                        &ctx, (IntPtr)dstPtr, (UIntPtr)dstData.Length, srcPtr, (UIntPtr)srcData.Length, _compressionLevel);

                    Interop.ZStd.SZ_ZStd_v1_5_6_FreeCompressionContext(&ctx);

                    if (compressedSize == UIntPtr.Zero)
                        throw new InvalidOperationException("Zstd Block Compression failed.");

                    return (int)compressedSize;
                }
                else // Index == 1, v1.5.2
                {
                    var ctx = new Interop.SZ_ZStd_v1_5_2_CompressionContext();
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateCompressionContext(&ctx);

                    UIntPtr compressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CompressBlock(
                        &ctx, (IntPtr)dstPtr, (UIntPtr)dstData.Length, srcPtr, (UIntPtr)srcData.Length, _compressionLevel);

                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeCompressionContext(&ctx);

                    if (compressedSize == UIntPtr.Zero)
                        throw new InvalidOperationException("Zstd Block Compression failed.");

                    return (int)compressedSize;
                }
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using ZStd.
        /// </summary>
        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                var version = Options.Version;
                if (version == null || version.Index == 0)
                {
                    var ctx = new Interop.SZ_ZStd_v1_5_6_DecompressionContext();
                    Interop.ZStd.SZ_ZStd_v1_5_6_CreateDecompressionContext(&ctx);

                    UIntPtr decompressedSize = Interop.ZStd.SZ_ZStd_v1_5_6_DecompressBlock(
                        &ctx, (IntPtr)dstPtr, (UIntPtr)dstData.Length, srcPtr, (UIntPtr)srcData.Length);

                    Interop.ZStd.SZ_ZStd_v1_5_6_FreeDecompressionContext(&ctx);

                    if ((uint)decompressedSize < 0)
                        throw new InvalidOperationException("Zstd Block Decompression failed.");

                    return (int)decompressedSize;
                }
                else // Index == 1, v1.5.2
                {
                    var ctx = new Interop.SZ_ZStd_v1_5_2_DecompressionContext();
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateDecompressionContext(&ctx);

                    UIntPtr decompressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_DecompressBlock(
                        &ctx, (IntPtr)dstPtr, (UIntPtr)dstData.Length, srcPtr, (UIntPtr)srcData.Length);

                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeDecompressionContext(&ctx);

                    if ((uint)decompressedSize < 0)
                        throw new InvalidOperationException("Zstd Block Decompression failed.");

                    return (int)decompressedSize;
                }
            }
        }

        internal override void OnDispose() { }
    }
}
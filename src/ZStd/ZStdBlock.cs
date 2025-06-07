using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.ZStd;

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
        /// <param name="srcData">The source data block to compress.</param>
        /// <param name="dstData">The destination data block to write compressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if compression fails.</exception>
        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                SZ_ZStd_v1_5_6_CompressionContext ctx = new SZ_ZStd_v1_5_6_CompressionContext();
                SZ_ZStd_v1_5_6_CreateCompressionContext(&ctx);

                UIntPtr compressedSize = SZ_ZStd_v1_5_6_CompressBlock(
                    &ctx, (IntPtr)dstPtr, (UIntPtr)dstData.Length, srcPtr, (UIntPtr)srcData.Length, _compressionLevel);

                SZ_ZStd_v1_5_6_FreeCompressionContext(&ctx);

                if (compressedSize == UIntPtr.Zero)
                    throw new InvalidOperationException("Zstd Block Compression failed.");

                return (int)compressedSize;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using ZStd.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if decompression fails.</exception>
        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                SZ_ZStd_v1_5_6_DecompressionContext ctx = new SZ_ZStd_v1_5_6_DecompressionContext();
                SZ_ZStd_v1_5_6_CreateDecompressionContext(&ctx);

                UIntPtr decompressedSize = SZ_ZStd_v1_5_6_DecompressBlock(
                    &ctx, (IntPtr)dstPtr, (UIntPtr)dstData.Length, srcPtr, (UIntPtr)srcData.Length);

                SZ_ZStd_v1_5_6_FreeDecompressionContext(&ctx);

                if ((uint)decompressedSize < 0)
                    throw new InvalidOperationException("Zstd Block Decompression failed.");

                return (int)decompressedSize;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="ZStdBlock"/>.
        /// </summary>
        internal override void OnDispose()
        {
        }
    }
}

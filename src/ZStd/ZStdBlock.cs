using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.ZStd;

namespace Nanook.GrindCore.ZStd
{
    public class ZStdBlock : CompressionBlock
    {
        private int _compressionLevel;

        public override int RequiredCompressOutputSize { get; }

        public ZStdBlock(CompressionAlgorithm algorithm, CompressionOptions options) : base(algorithm, options)
        {
            _compressionLevel = (int)options.Type;
            int isize = (int)options.BlockSize!;
            RequiredCompressOutputSize = isize + (isize >> 7) + 128;
        }

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
    }
}
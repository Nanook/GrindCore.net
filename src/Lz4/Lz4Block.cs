using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;
using System.Reflection.Emit;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lz4;

namespace Nanook.GrindCore.Lz4
{
    /// <summary>
    /// Provides a block-based implementation of the LZ4 compression algorithm.
    /// </summary>
    public class Lz4Block : CompressionBlock
    {
        /// <summary>
        /// Gets the required output buffer size for compression, as determined by the LZ4 algorithm.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lz4Block"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        public Lz4Block(CompressionOptions options) : base(CompressionAlgorithm.Lz4, options)
        {
            int isize = (int)options.BlockSize!;
            RequiredCompressOutputSize = isize + (isize / 255) + 16;
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using LZ4.
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

                int compressedSize;

                if ((int)this.CompressionType >= 3) // Use HC compression for level 3 or higher
                {
                    compressedSize = Interop.Lz4.SZ_Lz4_v1_10_0_CompressHC(
                        srcPtr, (IntPtr)dstPtr,
                        srcData.Length, dstCount,
                        (int)this.CompressionType); // Pass HC compression level
                }
                else
                {
                    SZ_Lz4_v1_10_0_Stream stream = new SZ_Lz4_v1_10_0_Stream();
                    SZ_Lz4_v1_10_0_Init(ref stream);

                    compressedSize = SZ_Lz4_v1_10_0_CompressFastContinue(
                        ref stream, srcPtr, (IntPtr)dstPtr, srcData.Length, dstCount, this.CompressionType == CompressionType.Level1 ? 1 : 0);

                    SZ_Lz4_v1_10_0_End(ref stream);
                }

                if (compressedSize <= 0)
                {
                    dstCount = 0;
                    return mapResult(compressedSize);
                }

                dstCount = compressedSize;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using LZ4.
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

                SZ_Lz4_v1_10_0_Stream stream = new SZ_Lz4_v1_10_0_Stream();
                SZ_Lz4_v1_10_0_Init(ref stream);

                int decompressedSize = SZ_Lz4_v1_10_0_DecompressSafeContinue(
                    ref stream, srcPtr, dstPtr, srcData.Length, dstCount);

                SZ_Lz4_v1_10_0_End(ref stream);

                if (decompressedSize < 0)
                {
                    dstCount = 0;
                    return mapResult(decompressedSize);
                }

                dstCount = decompressedSize;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="Lz4Block"/>.
        /// </summary>
        internal override void OnDispose()
        {
        }

        private static CompressionResultCode mapResult(int code)
        {
            return code switch
            {
                0      => CompressionResultCode.Success,
                -1     => CompressionResultCode.Error,
                -2     => CompressionResultCode.InsufficientBuffer,
                -3     => CompressionResultCode.InvalidParameter,
                -4     => CompressionResultCode.InvalidData,
                _      => CompressionResultCode.Error
            };
        }
    }
}


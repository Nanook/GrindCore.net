using System;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.BZip2;

namespace Nanook.GrindCore.BZip2
{
    /// <summary>
    /// Provides a block-based implementation of the BZip2 compression algorithm.
    /// </summary>
    public class BZip2Block : CompressionBlock
    {
        private readonly int _blockSize100k;
        private readonly int _workFactor;
        private readonly int _small;

        /// <summary>
        /// Gets the required output buffer size for compression, as determined by bzip2's own
        /// documented worst-case bound (source size + 1% + 600 bytes; see BZ2_bzBuffToBuffCompress
        /// in bzlib.h).
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BZip2Block"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        public BZip2Block(CompressionOptions options) : base(CompressionAlgorithm.BZip2, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.BlockSize == null)
                throw new ArgumentNullException(nameof(options.BlockSize));

            // bzip2's "level" is blockSize100k (1-9, 900k*n block size); it has no 0/no-compression
            // concept, so clamp CompressionType (which may resolve to Level0/NoCompression via the
            // base class) into bzip2's valid 1-9 range.
            int level = (int)this.CompressionType;
            _blockSize100k = level < 1 ? 1 : (level > 9 ? 9 : level);

            _workFactor = options.Dictionary?.WorkFactor ?? 0;
            _small = (options.Dictionary?.SmallDecompress ?? false) ? 1 : 0;

            int sourceLen = (int)options.BlockSize!;
            // bzlib.h's own documented bound: dest buffer must be at least 1% larger than the
            // source, plus 600 extra bytes, to guarantee BZ2_bzBuffToBuffCompress never fails with
            // BZ_OUTBUFF_FULL. Integer division truncates the 1%, so add 1 to stay on the safe side.
            RequiredCompressOutputSize = sourceLen + (sourceLen / 100 + 1) + 600;
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using BZip2.
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
                byte* s = srcPtr + srcData.Offset;
                byte* d = dstPtr + dstData.Offset;

                UIntPtr dstCapacity = (UIntPtr)dstCount;
                int result = SZ_BZip2_v1_0_8_CompressBlock(d, ref dstCapacity, s, (UIntPtr)srcData.Length, _blockSize100k, _workFactor);

                if (result != BZ_OK)
                {
                    dstCount = 0;
                    return mapResult(result);
                }

                dstCount = (int)dstCapacity;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using BZip2.
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
                byte* s = srcPtr + srcData.Offset;
                byte* d = dstPtr + dstData.Offset;

                UIntPtr dstCapacity = (UIntPtr)dstCount;
                int result = SZ_BZip2_v1_0_8_DecompressBlock(d, ref dstCapacity, s, (UIntPtr)srcData.Length, _small);

                if (result != BZ_OK)
                {
                    dstCount = 0;
                    return mapResult(result);
                }

                dstCount = (int)dstCapacity;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="BZip2Block"/>. No resources to release for BZip2 blocks.
        /// </summary>
        internal override void OnDispose()
        {
        }

        private static CompressionResultCode mapResult(int code)
        {
            return code switch
            {
                BZ_OK => CompressionResultCode.Success,
                BZ_SEQUENCE_ERROR => CompressionResultCode.Error,
                BZ_PARAM_ERROR => CompressionResultCode.InvalidParameter,
                BZ_MEM_ERROR => CompressionResultCode.Error,
                BZ_DATA_ERROR => CompressionResultCode.InvalidData,
                BZ_DATA_ERROR_MAGIC => CompressionResultCode.InvalidData,
                BZ_UNEXPECTED_EOF => CompressionResultCode.InvalidData,
                BZ_OUTBUFF_FULL => CompressionResultCode.InsufficientBuffer,
                BZ_CONFIG_ERROR => CompressionResultCode.Error,
                _ => CompressionResultCode.Error
            };
        }
    }
}

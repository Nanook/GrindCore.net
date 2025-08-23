using System;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore.Copy
{
    /// <summary>
    /// Provides a block-based Copy with no compression.
    /// </summary>
    public class CopyBlock : CompressionBlock
    {
        /// <summary>
        /// Gets the required output buffer size for compression, which is equal to the input block size for Copy.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyBlock"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        public CopyBlock(CompressionOptions options) : base(CompressionAlgorithm.Copy, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.BlockSize == null)
                throw new ArgumentNullException(nameof(options.BlockSize));

            int blockSize = (int)options.BlockSize!;
            RequiredCompressOutputSize = blockSize; // No overhead for Copy
        }

        /// <summary>
        /// Copies the source data block to the destination data block (no compression).
        /// </summary>
        /// <param name="srcData">The source data block.</param>
        /// <param name="dstData">The destination data block.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal override CompressionResultCode OnCompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            if (dstCount < srcData.Length)
            {
                dstCount = 0;
                return CompressionResultCode.InsufficientBuffer;
            }
            
            srcData.CopyTo(dstData, srcData.Offset, 0, srcData.Length);
            dstCount = srcData.Length;
            return CompressionResultCode.Success;
        }

        /// <summary>
        /// Copies the source data block to the destination data block (no decompression).
        /// </summary>
        /// <param name="srcData">The source data block.</param>
        /// <param name="dstData">The destination data block.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal override CompressionResultCode OnDecompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            if (dstCount < srcData.Length)
            {
                dstCount = 0;
                return CompressionResultCode.InsufficientBuffer;
            }
            
            srcData.CopyTo(dstData, srcData.Offset, 0, srcData.Length);
            dstCount = srcData.Length;
            return CompressionResultCode.Success;
        }

        /// <summary>
        /// Releases any resources used by the <see cref="CopyBlock"/>. No resources to release for Copy.
        /// </summary>
        internal override void OnDispose()
        {
        }
    }
}

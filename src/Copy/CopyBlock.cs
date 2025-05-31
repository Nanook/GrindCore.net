using System;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore.Copy
{
    public class CopyBlock : CompressionBlock
    {

        public override int RequiredCompressOutputSize { get; }

        public CopyBlock(CompressionAlgorithm algorithm, CompressionOptions options) : base(algorithm, options)
        {
            int blockSize = (int)options.BlockSize!;
            RequiredCompressOutputSize = blockSize; // Adjust for overhead
        }

        internal override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            srcData.CopyTo(dstData, srcData.Offset, 0, srcData.Length);
            return srcData.Length;
        }

        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            srcData.CopyTo(dstData, srcData.Offset, 0, srcData.Length);
            return srcData.Length;
        }

    }
}
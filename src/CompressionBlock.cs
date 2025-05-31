using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

#if !CLASSIC && (NET40_OR_GREATER || NETSTANDARD || NETCOREAPP)
using System.Threading.Tasks;
using static Nanook.GrindCore.Interop;
#endif

namespace Nanook.GrindCore
{
    public abstract class CompressionBlock
    {
        protected readonly CompressionAlgorithm Algorithm;
        protected readonly CompressionOptions Options;

        public abstract int RequiredCompressOutputSize { get; }

        public CompressionBlock(CompressionAlgorithm algorithm, CompressionOptions options)
        {
            Algorithm = algorithm;
            Options = options;
        }

        internal abstract int OnCompress(DataBlock srcData, DataBlock dstData);
        internal abstract int OnDecompress(DataBlock srcData, DataBlock dstData);

        public virtual int Decompress(byte[] srcBuffer, int srcOffset, int srcCount, byte[] dstBuffer, int dstOffset, int dstCount)
        {
            DataBlock srcDataBlock = new DataBlock(srcBuffer, srcOffset, srcCount); // Use DataBlock for internal logic
            DataBlock dstDataBlock = new DataBlock(dstBuffer, dstOffset, dstCount); // Use DataBlock for internal logic
            return OnDecompress(srcDataBlock, dstDataBlock); // Use framework independent data container
        }

        /// <summary>
        /// Writes data asynchronously to the stream using byte[]. Converts to DataBlock internally.
        /// </summary>
        public int Compress(byte[] srcBuffer, int srcOffset, int srcCount, byte[] dstBuffer, int dstOffset, int dstCount)
        {
            DataBlock srcDataBlock = new DataBlock(srcBuffer, srcOffset, srcCount); // Use DataBlock for internal logic
            DataBlock dstDataBlock = new DataBlock(dstBuffer, dstOffset, dstCount); // Use DataBlock for internal logic
            return OnCompress(srcDataBlock, dstDataBlock); // Use framework independent data container
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        public virtual async ValueTask<int> DecompressAsync(ReadOnlyMemory<byte> src, Memory<byte> dst)
        {
            if (SynchronizationContext.Current == null)
            {
                DataBlock srcDataBlock = new DataBlock(src.Span, 0, src.Length); // Use DataBlock for internal logic
                DataBlock dstDataBlock = new DataBlock(dst.Span, 0, src.Length); // Use DataBlock for internal logic
                return OnDecompress(srcDataBlock, dstDataBlock); // Use framework independent data container
            }

            return await Task.Run(() =>
            {
                DataBlock srcDataBlock = new DataBlock(src.Span, 0, src.Length); // Use DataBlock for internal logic
                DataBlock dstDataBlock = new DataBlock(dst.Span, 0, src.Length); // Use DataBlock for internal logic
                return OnDecompress(srcDataBlock, dstDataBlock); // Use framework independent data container
            }).ConfigureAwait(false);
        }

        public virtual async ValueTask<int> CompressAsync(ReadOnlyMemory<byte> src, Memory<byte> dst)
        {
            if (SynchronizationContext.Current == null)
            {
                DataBlock srcDataBlock = new DataBlock(src.Span, 0, src.Length); // Use DataBlock for internal logic
                DataBlock dstDataBlock = new DataBlock(dst.Span, 0, src.Length); // Use DataBlock for internal logic
                return OnCompress(srcDataBlock, dstDataBlock); // Use framework independent data container
            }

            return await Task.Run(() =>
            {
                DataBlock srcDataBlock = new DataBlock(src.Span, 0, src.Length); // Use DataBlock for internal logic
                DataBlock dstDataBlock = new DataBlock(dst.Span, 0, src.Length); // Use DataBlock for internal logic
                return OnCompress(srcDataBlock, dstDataBlock); // Use framework independent data container
            }).ConfigureAwait(false);
        }
#endif

#if CLASSIC || NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
        public virtual async Task<int> DecompressAsync(byte[] srcBuffer, int srcOffset, int srcCount, byte[] dstBuffer, int dstOffset, int dstCount)
        {
            if (SynchronizationContext.Current == null)
            {
                DataBlock srcDataBlock = new DataBlock(srcBuffer, srcOffset, srcCount); // Use DataBlock for internal logic
                DataBlock dstDataBlock = new DataBlock(dstBuffer, dstOffset, dstCount); // Use DataBlock for internal logic
                return OnDecompress(srcDataBlock, dstDataBlock); // Use framework independent data container
            }

            return await Task.Run(() =>
            {
                DataBlock srcDataBlock = new DataBlock(srcBuffer, srcOffset, srcCount); // Use DataBlock for internal logic
                DataBlock dstDataBlock = new DataBlock(dstBuffer, dstOffset, dstCount); // Use DataBlock for internal logic
                return OnDecompress(srcDataBlock, dstDataBlock); // Use framework independent data container
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Writes data asynchronously to the stream using byte[]. Converts to DataBlock internally.
        /// </summary>
        public virtual async Task<int> CompressAsync(byte[] srcBuffer, int srcOffset, int srcCount, byte[] dstBuffer, int dstOffset, int dstCount)
        {
            if (SynchronizationContext.Current == null)
            {
                DataBlock srcDataBlock = new DataBlock(srcBuffer, srcOffset, srcCount); // Use DataBlock for internal logic
                DataBlock dstDataBlock = new DataBlock(dstBuffer, dstOffset, dstCount); // Use DataBlock for internal logic
                return OnCompress(srcDataBlock, dstDataBlock); // Use framework independent data container
            }

            return await Task.Run(() =>
            {
                DataBlock srcDataBlock = new DataBlock(srcBuffer, srcOffset, srcCount); // Use DataBlock for internal logic
                DataBlock dstDataBlock = new DataBlock(dstBuffer, dstOffset, dstCount); // Use DataBlock for internal logic
                return OnCompress(srcDataBlock, dstDataBlock); // Use framework independent data container
            }).ConfigureAwait(false);
        }
#endif

    }
}

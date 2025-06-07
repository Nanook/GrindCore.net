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
    /// <summary>
    /// Provides a base class for block-based compression and decompression operations.
    /// </summary>
    public abstract class CompressionBlock : IDisposable
    {
        private bool _disposed;
        /// <summary>
        /// Gets the compression options used by this block compression instance.
        /// </summary>
        protected readonly CompressionOptions Options;

        /// <summary>
        /// Gets or sets the compression type for this block compression instance.
        /// </summary>
        internal CompressionType CompressionType;

        /// <summary>
        /// Gets or sets the compression properties for this stream.
        /// </summary>
        public byte[]? Properties { get; protected set; }

        /// <summary>
        /// Gets the maximum required output buffer size for compression.
        /// </summary>
        public abstract int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Gets the compression defaults for this block, e.g. Fastest, Optimal and SmallestSize levels.
        /// </summary>
        internal virtual CompressionDefaults Defaults { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionBlock"/> class.
        /// </summary>
        /// <param name="defaultAlgorithm">The default algorithm, used when options.Version is not set to override it.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the compression type is invalid.</exception>
        protected CompressionBlock(CompressionAlgorithm defaultAlgorithm, CompressionOptions options)
        {
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            Options = options;

            this.Defaults = new CompressionDefaults(defaultAlgorithm, options.Version);
            this.CompressionType = options.Type;

            if (CompressionType == CompressionType.Optimal)
                CompressionType = this.Defaults.LevelOptimal;
            else if (CompressionType == CompressionType.SmallestSize)
                CompressionType = this.Defaults.LevelSmallestSize;
            else if (CompressionType == CompressionType.Fastest)
                CompressionType = this.Defaults.LevelFastest;

            if (CompressionType < 0 || CompressionType > this.Defaults.LevelSmallestSize)
                throw new ArgumentException("Invalid Option, CompressionType / Level");
        }

        /// <summary>
        /// Compresses data from the source block into the destination block.
        /// </summary>
        /// <param name="srcData">The source data block.</param>
        /// <param name="dstData">The destination data block.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        internal abstract int OnCompress(DataBlock srcData, DataBlock dstData);

        /// <summary>
        /// Decompresses data from the source block into the destination block.
        /// </summary>
        /// <param name="srcData">The source data block.</param>
        /// <param name="dstData">The destination data block.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        internal abstract int OnDecompress(DataBlock srcData, DataBlock dstData);

        /// <summary>
        /// Performs custom cleanup for managed resources.
        /// </summary>
        internal abstract void OnDispose();

        /// <summary>
        /// Decompresses data from a source buffer to a destination buffer.
        /// </summary>
        /// <param name="srcBuffer">The source buffer.</param>
        /// <param name="srcOffset">The offset in the source buffer.</param>
        /// <param name="srcCount">The number of bytes to decompress from the source buffer.</param>
        /// <param name="dstBuffer">The destination buffer.</param>
        /// <param name="dstOffset">The offset in the destination buffer.</param>
        /// <param name="dstCount">The maximum number of bytes available in the destination buffer.</param>
        /// <returns>The number of bytes written to the destination buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="srcBuffer"/> or <paramref name="dstBuffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any offset or count is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of offset and count exceeds the buffer length.</exception>
        public virtual int Decompress(byte[] srcBuffer, int srcOffset, int srcCount, byte[] dstBuffer, int dstOffset, int dstCount)
        {
            if (srcBuffer == null)
                throw new ArgumentNullException(nameof(srcBuffer));
            if (dstBuffer == null)
                throw new ArgumentNullException(nameof(dstBuffer));
            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(srcOffset), "Source offset must be non-negative.");
            if (srcCount < 0)
                throw new ArgumentOutOfRangeException(nameof(srcCount), "Source count must be non-negative.");
            if (dstOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(dstOffset), "Destination offset must be non-negative.");
            if (dstCount < 0)
                throw new ArgumentOutOfRangeException(nameof(dstCount), "Destination count must be non-negative.");
            if (srcBuffer.Length - srcOffset < srcCount)
                throw new ArgumentException("The sum of srcOffset and srcCount is greater than the source buffer length.");
            if (dstBuffer.Length - dstOffset < dstCount)
                throw new ArgumentException("The sum of dstOffset and dstCount is greater than the destination buffer length.");

            DataBlock srcDataBlock = new DataBlock(srcBuffer, srcOffset, srcCount); // Use DataBlock for internal logic
            DataBlock dstDataBlock = new DataBlock(dstBuffer, dstOffset, dstCount); // Use DataBlock for internal logic
            return OnDecompress(srcDataBlock, dstDataBlock); // Use framework independent data container
        }

        /// <summary>
        /// Compresses data from a source buffer to a destination buffer.
        /// </summary>
        /// <param name="srcBuffer">The source buffer.</param>
        /// <param name="srcOffset">The offset in the source buffer.</param>
        /// <param name="srcCount">The number of bytes to compress from the source buffer.</param>
        /// <param name="dstBuffer">The destination buffer.</param>
        /// <param name="dstOffset">The offset in the destination buffer.</param>
        /// <param name="dstCount">The maximum number of bytes available in the destination buffer.</param>
        /// <returns>The number of bytes written to the destination buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="srcBuffer"/> or <paramref name="dstBuffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any offset or count is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of offset and count exceeds the buffer length.</exception>
        public int Compress(byte[] srcBuffer, int srcOffset, int srcCount, byte[] dstBuffer, int dstOffset, int dstCount)
        {
            if (srcBuffer == null)
                throw new ArgumentNullException(nameof(srcBuffer));
            if (dstBuffer == null)
                throw new ArgumentNullException(nameof(dstBuffer));
            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(srcOffset), "Source offset must be non-negative.");
            if (srcCount < 0)
                throw new ArgumentOutOfRangeException(nameof(srcCount), "Source count must be non-negative.");
            if (dstOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(dstOffset), "Destination offset must be non-negative.");
            if (dstCount < 0)
                throw new ArgumentOutOfRangeException(nameof(dstCount), "Destination count must be non-negative.");
            if (srcBuffer.Length - srcOffset < srcCount)
                throw new ArgumentException("The sum of srcOffset and srcCount is greater than the source buffer length.");
            if (dstBuffer.Length - dstOffset < dstCount)
                throw new ArgumentException("The sum of dstOffset and dstCount is greater than the destination buffer length.");

            DataBlock srcDataBlock = new DataBlock(srcBuffer, srcOffset, srcCount); // Use DataBlock for internal logic
            DataBlock dstDataBlock = new DataBlock(dstBuffer, dstOffset, dstCount); // Use DataBlock for internal logic
            return OnCompress(srcDataBlock, dstDataBlock); // Use framework independent data container
        }

        /// <summary>
        /// Releases the resources used by the <see cref="CompressionBlock"/> class.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        public void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    OnDispose(); // Custom cleanup for managed resources

                _disposed = true;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="CompressionBlock"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Asynchronously decompresses data from a source memory region to a destination memory region.
        /// </summary>
        /// <param name="src">The source memory region.</param>
        /// <param name="dst">The destination memory region.</param>
        /// <returns>A task that represents the asynchronous decompress operation. The value of the result parameter contains the total number of bytes written to the destination.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="src"/> or <paramref name="dst"/> length is negative.</exception>
        public virtual async ValueTask<int> DecompressAsync(ReadOnlyMemory<byte> src, Memory<byte> dst)
        {
            if (src.Length < 0)
                throw new ArgumentOutOfRangeException(nameof(src), "Source length must be non-negative.");
            if (dst.Length < 0)
                throw new ArgumentOutOfRangeException(nameof(dst), "Destination length must be non-negative.");

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

        /// <summary>
        /// Asynchronously compresses data from a source memory region to a destination memory region.
        /// </summary>
        /// <param name="src">The source memory region.</param>
        /// <param name="dst">The destination memory region.</param>
        /// <returns>A task that represents the asynchronous compress operation. The value of the result parameter contains the total number of bytes written to the destination.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="src"/> or <paramref name="dst"/> length is negative.</exception>
        public virtual async ValueTask<int> CompressAsync(ReadOnlyMemory<byte> src, Memory<byte> dst)
        {
            if (src.Length < 0)
                throw new ArgumentOutOfRangeException(nameof(src), "Source length must be non-negative.");
            if (dst.Length < 0)
                throw new ArgumentOutOfRangeException(nameof(dst), "Destination length must be non-negative.");

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

        /// <summary>
        /// Asynchronously releases all resources used by the <see cref="CompressionBlock"/>.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (SynchronizationContext.Current == null)
            {
                Dispose(true);
                return;
            }
            await Task.Run(() =>
            {
                Dispose(true);
            }).ConfigureAwait(false);
        }
#endif

#if CLASSIC || NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
        /// <summary>
        /// Asynchronously decompresses data from a source buffer to a destination buffer.
        /// </summary>
        /// <param name="srcBuffer">The source buffer.</param>
        /// <param name="srcOffset">The offset in the source buffer.</param>
        /// <param name="srcCount">The number of bytes to decompress from the source buffer.</param>
        /// <param name="dstBuffer">The destination buffer.</param>
        /// <param name="dstOffset">The offset in the destination buffer.</param>
        /// <param name="dstCount">The maximum number of bytes available in the destination buffer.</param>
        /// <returns>A task that represents the asynchronous decompress operation. The value of the result parameter contains the total number of bytes written to the destination buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="srcBuffer"/> or <paramref name="dstBuffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any offset or count is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of offset and count exceeds the buffer length.</exception>
        public virtual async Task<int> DecompressAsync(byte[] srcBuffer, int srcOffset, int srcCount, byte[] dstBuffer, int dstOffset, int dstCount)
        {
            if (srcBuffer == null)
                throw new ArgumentNullException(nameof(srcBuffer));
            if (dstBuffer == null)
                throw new ArgumentNullException(nameof(dstBuffer));
            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(srcOffset), "Source offset must be non-negative.");
            if (srcCount < 0)
                throw new ArgumentOutOfRangeException(nameof(srcCount), "Source count must be non-negative.");
            if (dstOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(dstOffset), "Destination offset must be non-negative.");
            if (dstCount < 0)
                throw new ArgumentOutOfRangeException(nameof(dstCount), "Destination count must be non-negative.");
            if (srcBuffer.Length - srcOffset < srcCount)
                throw new ArgumentException("The sum of srcOffset and srcCount is greater than the source buffer length.");
            if (dstBuffer.Length - dstOffset < dstCount)
                throw new ArgumentException("The sum of dstOffset and dstCount is greater than the destination buffer length.");

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
        /// Asynchronously compresses data from a source buffer to a destination buffer.
        /// </summary>
        /// <param name="srcBuffer">The source buffer.</param>
        /// <param name="srcOffset">The offset in the source buffer.</param>
        /// <param name="srcCount">The number of bytes to compress from the source buffer.</param>
        /// <param name="dstBuffer">The destination buffer.</param>
        /// <param name="dstOffset">The offset in the destination buffer.</param>
        /// <param name="dstCount">The maximum number of bytes available in the destination buffer.</param>
        /// <returns>A task that represents the asynchronous compress operation. The value of the result parameter contains the total number of bytes written to the destination buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="srcBuffer"/> or <paramref name="dstBuffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any offset or count is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of offset and count exceeds the buffer length.</exception>
        public virtual async Task<int> CompressAsync(byte[] srcBuffer, int srcOffset, int srcCount, byte[] dstBuffer, int dstOffset, int dstCount)
        {
            if (srcBuffer == null)
                throw new ArgumentNullException(nameof(srcBuffer));
            if (dstBuffer == null)
                throw new ArgumentNullException(nameof(dstBuffer));
            if (srcOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(srcOffset), "Source offset must be non-negative.");
            if (srcCount < 0)
                throw new ArgumentOutOfRangeException(nameof(srcCount), "Source count must be non-negative.");
            if (dstOffset < 0)
                throw new ArgumentOutOfRangeException(nameof(dstOffset), "Destination offset must be non-negative.");
            if (dstCount < 0)
                throw new ArgumentOutOfRangeException(nameof(dstCount), "Destination count must be non-negative.");
            if (srcBuffer.Length - srcOffset < srcCount)
                throw new ArgumentException("The sum of srcOffset and srcCount is greater than the source buffer length.");
            if (dstBuffer.Length - dstOffset < dstCount)
                throw new ArgumentException("The sum of dstOffset and dstCount is greater than the destination buffer length.");

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

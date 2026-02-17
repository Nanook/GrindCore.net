using System;
using System.IO;

namespace Nanook.GrindCore.Copy
{
    /// <summary>
    /// A non-compression stream that performs a direct stream copy. 
    /// This allows a stream copy to work seamlessly with this library and the <see cref="CompressionStream"/> base.
    /// </summary>
    public class CopyStream : CompressionStream
    {
        /// <summary>
        /// Gets the input buffer size for copy operations.
        /// </summary>
        internal override int BufferSizeInput => 1 * 0x400 * 0x400;

        /// <summary>
        /// Gets the output buffer size for copy operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyStream"/> class.
        /// </summary>
        /// <param name="stream">The stream to copy to or from.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        public CopyStream(Stream stream, CompressionOptions options) : base(true, stream, CompressionAlgorithm.Copy, options)
        {
            BufferSizeOutput = BufferThreshold;
        }

        /// <summary>
        /// Reads data from the stream and writes it to the buffer. Position is updated with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="inData">The buffer to read data into.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesReadFromStream">The number of bytes read from the underlying stream.</param>
        /// <param name="length"> The maximum number of bytes to read.If 0, the method will fill the buffer if possible.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        /// <exception cref="NotSupportedException">Thrown if the stream is not in decompression mode.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override int OnRead(CompressionBuffer inData, CancellableTask cancel, out int bytesReadFromStream, int length = 0)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            cancel.ThrowIfCancellationRequested();

            bytesReadFromStream = BaseRead(inData, inData.AvailableWrite);
            return bytesReadFromStream;
        }

        /// <summary>
        /// Writes data from the buffer to the stream. Position is updated with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="outData">The buffer containing data to write.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <exception cref="NotSupportedException">Thrown if the stream is not in compression mode.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnWrite(CompressionBuffer outData, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = outData.AvailableRead;
            cancel.ThrowIfCancellationRequested();

            outData.Tidy();
            BaseWrite(outData, outData.AvailableRead);
        }

        /// <summary>
        /// Flushes any remaining data in the buffer to the stream.
        /// </summary>
        /// <param name="outData">The buffer containing data to flush.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        internal override void OnFlush(CompressionBuffer outData, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;
            if (IsCompress)
            {
                if (outData.AvailableRead != 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    BaseWrite(outData, outData.AvailableRead);
                }
            }
        }

        /// <summary>
        /// Disposes the <see cref="CopyStream"/> and its resources.
        /// </summary>
        protected override void OnDispose()
        {
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Asynchronously reads data from the stream and writes it to the buffer.
        /// </summary>
        /// <param name="inData">The buffer to read data into.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="length">The maximum number of bytes to read. If 0, the method will fill the buffer if possible.</param>
        /// <returns>A tuple containing (bytes read, bytes read from stream).</returns>
        internal override async System.Threading.Tasks.ValueTask<(int result, int bytesRead)> OnReadAsync(
            CompressionBuffer inData,
            System.Threading.CancellationToken cancellationToken,
            int length = 0)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            cancellationToken.ThrowIfCancellationRequested();

            int bytesReadFromStream = await BaseReadAsync(inData, inData.AvailableWrite, cancellationToken).ConfigureAwait(false);
            return (bytesReadFromStream, bytesReadFromStream);
        }

        /// <summary>
        /// Asynchronously writes data from the buffer to the stream.
        /// </summary>
        /// <param name="outData">The buffer containing data to write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of bytes written to the stream.</returns>
        internal override async System.Threading.Tasks.ValueTask<int> OnWriteAsync(
            CompressionBuffer outData,
            System.Threading.CancellationToken cancellationToken)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            int bytesWrittenToStream = outData.AvailableRead;
            cancellationToken.ThrowIfCancellationRequested();

            outData.Tidy();
            await BaseWriteAsync(outData, outData.AvailableRead, cancellationToken).ConfigureAwait(false);
            return bytesWrittenToStream;
        }

        /// <summary>
        /// Asynchronously flushes any remaining data in the buffer to the stream.
        /// </summary>
        /// <param name="outData">The buffer containing data to flush.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        /// <returns>The number of bytes written to the stream.</returns>
        internal override async System.Threading.Tasks.ValueTask<int> OnFlushAsync(
            CompressionBuffer outData,
            System.Threading.CancellationToken cancellationToken,
            bool flush,
            bool complete)
        {
            int bytesWrittenToStream = 0;
            if (IsCompress)
            {
                if (outData.AvailableRead != 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await BaseWriteAsync(outData, outData.AvailableRead, cancellationToken).ConfigureAwait(false);
                    bytesWrittenToStream = outData.AvailableRead;
                }
            }
            return bytesWrittenToStream;
        }
#endif
    }
}

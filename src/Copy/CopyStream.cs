using System;
using System.IO;
using System.Threading;

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
            BufferSizeOutput = CacheThreshold;
        }

        /// <summary>
        /// Reads data from the stream and writes it to the buffer. Position is updated with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="data">The buffer to read data into.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesReadFromStream">The number of bytes read from the underlying stream.</param>
        /// <param name="length"> The maximum number of bytes to read.If 0, the method will fill the buffer if possible.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        /// <exception cref="NotSupportedException">Thrown if the stream is not in decompression mode.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream, int length = 0)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            cancel.ThrowIfCancellationRequested();

            bytesReadFromStream = BaseRead(data.Data, data.Size, data.AvailableWrite);
            data.Write(bytesReadFromStream);
            return bytesReadFromStream;
        }

        /// <summary>
        /// Writes data from the buffer to the stream. Position is updated with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="data">The buffer containing data to write.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <exception cref="NotSupportedException">Thrown if the stream is not in compression mode.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = data.AvailableRead;
            cancel.ThrowIfCancellationRequested();

            BaseWrite(data.Data, data.Pos, data.AvailableRead);
            data.Read(data.AvailableRead);
        }

        /// <summary>
        /// Flushes any remaining data in the buffer to the stream.
        /// </summary>
        /// <param name="data">The buffer containing data to flush.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;
            if (IsCompress)
            {
                if (data.AvailableRead != 0)
                {
                    cancel.ThrowIfCancellationRequested();
                    BaseWrite(data.Data, data.Pos, data.AvailableRead);
                    data.Read(data.AvailableRead);
                }
            }
        }

        /// <summary>
        /// Disposes the <see cref="CopyStream"/> and its resources.
        /// </summary>
        protected override void OnDispose()
        {
        }
    }
}

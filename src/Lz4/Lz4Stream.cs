using System;
using System.IO;
using System.Reflection.Emit;
using System.Threading;

namespace Nanook.GrindCore.Lz4
{
    /// <summary>
    /// Provides a stream implementation for LZ4 compression and decompression.
    /// Inherits common <see cref="Stream"/> functionality from <see cref="CompressionStream"/>.
    /// Uses a customized LZ4 implementation to allow the Stream.Write pattern for compression.
    /// </summary>
    public class Lz4Stream : CompressionStream
    {
        private readonly Lz4Decoder _decoder;
        private readonly Lz4Encoder _encoder;
        private readonly CompressionBuffer _buffer;

        /// <summary>
        /// Gets the input buffer size for LZ4 operations.
        /// </summary>
        internal override int BufferSizeInput => 2 * 0x400 * 0x400;

        /// <summary>
        /// Gets the output buffer size for LZ4 operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lz4Stream"/> class with the specified stream and options.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        public Lz4Stream(Stream stream, CompressionOptions options)
            : base(true, stream, CompressionAlgorithm.Lz4, options)
        {
            if (IsCompress)
            {
                this.BufferSizeOutput = BufferThreshold + (BufferThreshold / 255) + 0x10 + 1;
                _encoder = new Lz4Encoder(BufferThreshold, (int)this.CompressionType);
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
            else
            {
                this.BufferSizeOutput = BufferThreshold;
                _decoder = new Lz4Decoder(this.BufferSizeOutput);
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using LZ4.
        /// Updates the position with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="data">The buffer to read decompressed data into.</param>
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

            bytesReadFromStream = 0;
            int total = 0;

            while (data.AvailableWrite > total)
            {
                cancel.ThrowIfCancellationRequested();

                if (_buffer.AvailableRead == 0)
                    BaseRead(_buffer, _buffer.AvailableWrite);

                if (_buffer.AvailableRead == 0)
                    return total;

                int decoded = (int)_decoder.DecodeData(_buffer, out int readSz, data);
                bytesReadFromStream += readSz;
                total += decoded;
            }

            return total;
        }

        /// <summary>
        /// Compresses data using LZ4 and writes it to the stream.
        /// Updates the position with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="data">The buffer containing data to compress and write.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <exception cref="NotSupportedException">Thrown if the stream is not in compression mode.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = 0;
            cancel.ThrowIfCancellationRequested();

            int avRead = data.AvailableRead;
            long size = _encoder.EncodeData(data, _buffer, false, cancel);

            if (size > 0)
            {
                _buffer.Tidy(); //reset, no data to shuffle
                BaseWrite(_buffer, _buffer.AvailableRead);
                bytesWrittenToStream += (int)size;
            }
        }

        /// <summary>
        /// Flushes any remaining compressed data to the stream.
        /// </summary>
        /// <param name="data">The buffer containing data to flush.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;

            if (IsCompress)
            {
                cancel.ThrowIfCancellationRequested();
                long size = data.AvailableRead == 0 ? 0 : _encoder.EncodeData(data, _buffer, true, cancel);
                size += _encoder.Flush(_buffer, flush, complete);
                if (size > 0)
                {
                    BaseWrite(_buffer, _buffer.AvailableRead);
                    bytesWrittenToStream = (int)size;
                }
            }
        }

        /// <summary>
        /// Disposes the <see cref="Lz4Stream"/> and its resources.
        /// </summary>
        protected override void OnDispose()
        {
            if (IsCompress)
                try { _encoder.Dispose(); } catch { }
            else
                try { _decoder.Dispose(); } catch { }
        }
    }
}

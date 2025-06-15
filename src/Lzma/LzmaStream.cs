using System;
using System.IO;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Provides a stream implementation for LZMA compression and decompression.
    /// Inherits common <see cref="Stream"/> functionality from <see cref="CompressionStream"/>.
    /// Uses a customized LZMA implementation to allow the Stream.Write pattern for compression.
    /// </summary>
    public class LzmaStream : CompressionStream
    {
        private readonly LzmaDecoder _decoder;
        private readonly LzmaEncoder _encoder;
        private readonly CompressionBuffer _buffer;

        /// <summary>
        /// Gets the input buffer size for LZMA operations.
        /// </summary>
        internal override int BufferSizeInput => 2 * 0x400 * 0x400;

        /// <summary>
        /// Gets the output buffer size for LZMA operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LzmaStream"/> class with the specified stream, options, and optional dictionary size.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <param name="dictionarySize">The dictionary size to use for compression (default is 0).</param>
        /// <exception cref="Exception">Thrown if <paramref name="options"/>.InitProperties is not set when decompressing.</exception>
        private LzmaStream(Stream stream, CompressionOptions options, int dictionarySize = 0)
            : base(true, stream, CompressionAlgorithm.Lzma, options)
        {
            if (IsCompress)
            {
                _encoder = new LzmaEncoder((int)CompressionType, (uint)dictionarySize, 0);
                Properties = _encoder.Properties;
                this.BufferSizeOutput = BufferThreshold + (BufferThreshold >> 1) + 0x10;
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
            else
            {
                if (options.InitProperties == null)
                    throw new Exception("LZMA requires CompressionOptions.InitProperties to be set to an array when decompressing");

                this.BufferSizeOutput = BufferThreshold;
                _decoder = new LzmaDecoder(options.InitProperties);
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LzmaStream"/> class for compression or decompression.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        public LzmaStream(Stream stream, CompressionOptions options)
            : this(stream, options, 0)
        {
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using LZMA.
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
            if (!CanRead)
                throw new NotSupportedException("Not for Compression mode");

            bytesReadFromStream = 0;
            int decoded = -1;
            int total = 0;
            int read = -1;

            if (length == 0 || length > data.AvailableWrite)
                length = data.AvailableWrite;

            while ((read != 0 || decoded != 0) && total < length)
            {
                cancel.ThrowIfCancellationRequested();
                if (decoded <= 0 && _buffer.AvailableRead == 0)
                    read = BaseRead(_buffer, _buffer.AvailableWrite);
                if (_buffer.AvailableRead == 0)
                    return total;
                decoded = _decoder.DecodeData(_buffer, out var readSz, data, length - total, out var _);
                bytesReadFromStream += readSz;
                total += decoded;
            }
            return total;
        }

        /// <summary>
        /// Compresses data using LZMA and writes it to the stream.
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
                while (true)
                {
                    long size = _encoder.EncodeData(data, _buffer, true, cancel);
                    if (size == 0)
                        break;
                    BaseWrite(_buffer, _buffer.AvailableRead);
                    bytesWrittenToStream += (int)size;
                }
            }
        }

        /// <summary>
        /// Disposes the <see cref="LzmaStream"/> and its resources.
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

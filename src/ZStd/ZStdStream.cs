using System;
using System.IO;
using System.Reflection.Emit;
using System.Threading;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a stream implementation for Zstandard (ZStd) compression and decompression.
    /// Inherits common <see cref="Stream"/> functionality from <see cref="CompressionStream"/>.
    /// Uses a customized ZStd implementation to allow the Stream.Write pattern for compression.
    /// </summary>
    public class ZStdStream : CompressionStream
    {
        private readonly ZStdDecoder _decoder;
        private readonly ZStdEncoder _encoder;
        private readonly CompressionBuffer _buffer;
        private bool _wroteData;

        /// <summary>
        /// Gets the input buffer size for ZStd operations.
        /// </summary>
        internal override int BufferSizeInput => 0x20000;

        /// <summary>
        /// Gets the output buffer size for ZStd operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdStream"/> class with the specified stream and options.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        public ZStdStream(Stream stream, CompressionOptions options)
            : base(true, stream, CompressionAlgorithm.ZStd, options)
        {
            _wroteData = false;

            if (IsCompress)
            {
                this.BufferSizeOutput = CacheThreshold + (CacheThreshold >> 7) + 128;
                _encoder = new ZStdEncoder(CacheThreshold, (int)this.CompressionType);
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
            else
            {
                this.BufferSizeOutput = CacheThreshold;
                _decoder = new ZStdDecoder();
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using ZStd.
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

                if (_buffer.Pos == 0)
                    _buffer.Write(BaseRead(_buffer.Data, _buffer.Size, _buffer.AvailableWrite));

                if (_buffer.AvailableRead == 0)
                    return total;

                int decoded = (int)_decoder.DecodeData(_buffer, out int readSz, data, cancel);
                bytesReadFromStream += readSz;
                total += decoded;
            }

            return total;
        }

        /// <summary>
        /// Compresses data using ZStd and writes it to the stream.
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
            _wroteData = true;

            if (size > 0)
            {
                BaseWrite(_buffer.Data, _buffer.Pos, _buffer.AvailableRead);
                _buffer.Read(_buffer.AvailableRead);
                bytesWrittenToStream += (int)size;
            }
        }

        /// <summary>
        /// Flushes any remaining compressed data to the stream and finalizes the compression if requested.
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
                long size = data.AvailableRead == 0 && _wroteData ? 0 : _encoder.EncodeData(data, _buffer, true, cancel);
                // Always flush at the end of compression
                size += _encoder.Flush(_buffer);
                if (size > 0)
                {
                    BaseWrite(_buffer.Data, _buffer.Pos, _buffer.AvailableRead);
                    _buffer.Read(_buffer.AvailableRead);
                    bytesWrittenToStream = (int)size;
                }
            }
        }

        /// <summary>
        /// Disposes the <see cref="ZStdStream"/> and its resources.
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

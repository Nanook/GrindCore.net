using System;
using System.IO;

namespace Nanook.GrindCore.BZip2
{
    /// <summary>
    /// Provides a stream implementation for BZip2 compression and decompression.
    /// Inherits common <see cref="Stream"/> functionality from <see cref="CompressionStream"/>.
    /// bzip2's bz_stream is deliberately shaped like zlib's z_stream, so this follows
    /// <c>DeflateStream</c>'s Read/Write/Flush structure rather than LZ4/LZMA's.
    /// </summary>
    public class BZip2Stream : CompressionStream
    {
        private BZip2Decoder? _decoder;
        private BZip2Encoder? _encoder;
        private readonly CompressionBuffer _buffer;

        /// <summary>
        /// Gets the input buffer size for BZip2 operations.
        /// </summary>
        internal override int BufferSizeInput => 0x200000;

        /// <summary>
        /// Gets the output buffer size for BZip2 operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BZip2Stream"/> class with the specified stream and options.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        public BZip2Stream(Stream stream, CompressionOptions options)
            : base(true, stream, CompressionAlgorithm.BZip2, options)
        {
            if (!IsCompress)
            {
                this.BufferSizeOutput = 8192;
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
                bool small = options?.Dictionary?.SmallDecompress ?? false;
                _decoder = new BZip2Decoder(small);
            }
            else
            {
                this.BufferSizeOutput = this.BufferThreshold != 0 ? this.BufferThreshold : this.BufferSizeInput;
                _buffer = new CompressionBuffer(this.BufferSizeOutput);

                // bzip2's "level" is blockSize100k (1-9); it has no 0/no-compression concept.
                int level = (int)this.CompressionType;
                int blockSize100k = level < 1 ? 1 : (level > 9 ? 9 : level);
                int workFactor = options?.Dictionary?.WorkFactor ?? 0;
                _encoder = new BZip2Encoder(blockSize100k, workFactor);
            }
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using BZip2.
        /// Updates the position with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="data">The buffer to read decompressed data into.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesReadFromStream">The number of bytes read from the underlying stream.</param>
        /// <param name="length">The maximum number of bytes to read. If 0, the method will fill the buffer if possible.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        /// <exception cref="NotSupportedException">Thrown if the stream is not in decompression mode.</exception>
        /// <exception cref="InvalidDataException">Thrown if the input stream is truncated.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream, int length = 0)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            bytesReadFromStream = 0;
            int bytesRead;
            while (true)
            {
                cancel.ThrowIfCancellationRequested();

                bytesRead = _decoder!.DecodeData(_buffer, data, length);
                if (bytesRead != 0 || _decoder.Finished)
                    break;

                if (_decoder.NeedsInput(_buffer))
                {
                    int available = _buffer.AvailableWrite;
                    int n = BaseRead(_buffer, _buffer.AvailableWrite);
                    if (n <= 0)
                    {
                        if (available != 0 && !_decoder.Finished && _decoder.NonEmptyInput)
                            throw new InvalidDataException(SR.TruncatedData);
                        break;
                    }
                    bytesReadFromStream += n;
                }

                if (data.AvailableWrite == 0)
                    break;
            }

            return bytesRead;
        }

        /// <summary>
        /// Compresses data using BZip2 and writes it to the stream.
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

            while (data.AvailableRead > 0)
            {
                cancel.ThrowIfCancellationRequested();

                int before = data.AvailableRead;
                int produced = _encoder!.EncodeData(data, _buffer);

                if (_buffer.AvailableRead > 0)
                {
                    BaseWrite(_buffer, _buffer.AvailableRead);
                    bytesWrittenToStream += produced;
                }

                if (data.AvailableRead == before && produced == 0)
                    break; // avoid a busy loop if no progress was made (output buffer full)
            }
        }

        /// <summary>
        /// Flushes any remaining compressed data to the stream.
        /// </summary>
        /// <param name="data">The buffer containing data to flush.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="flush">Indicates if this is a flush operation (BZ_FLUSH: block-boundary flush, stream stays open).</param>
        /// <param name="complete">Indicates that there is no more data to compress (BZ_FINISH: ends the stream).</param>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;

            if (IsCompress)
            {
                cancel.ThrowIfCancellationRequested();

                OnWrite(data, cancel, out int written);
                bytesWrittenToStream += written;

                if (flush)
                {
                    bool flushDone;
                    do
                    {
                        cancel.ThrowIfCancellationRequested();
                        flushDone = _encoder!.Flush(_buffer, out int flushedBytes);
                        if (flushedBytes > 0)
                        {
                            BaseWrite(_buffer, flushedBytes);
                            bytesWrittenToStream += flushedBytes;
                        }
                    } while (!flushDone);
                }

                if (complete)
                {
                    bool finished;
                    do
                    {
                        cancel.ThrowIfCancellationRequested();
                        finished = _encoder!.Finish(_buffer, out int finishedBytes);
                        if (finishedBytes > 0)
                        {
                            BaseWrite(_buffer, finishedBytes);
                            bytesWrittenToStream += finishedBytes;
                        }
                    } while (!finished);
                }
            }
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Asynchronously reads data from the stream and decompresses it using BZip2.
        /// This override provides true async I/O without blocking.
        /// </summary>
        internal override async System.Threading.Tasks.ValueTask<(int result, int bytesRead)> OnReadAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken,
            int length = 0)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            int bytesReadFromStream = 0;
            int bytesRead;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bytesRead = _decoder!.DecodeData(_buffer, data, length);
                if (bytesRead != 0 || _decoder.Finished)
                    break;

                if (_decoder.NeedsInput(_buffer))
                {
                    int available = _buffer.AvailableWrite;
                    int n = await BaseReadAsync(_buffer, _buffer.AvailableWrite, cancellationToken).ConfigureAwait(false);
                    if (n <= 0)
                    {
                        if (available != 0 && !_decoder.Finished && _decoder.NonEmptyInput)
                            throw new InvalidDataException(SR.TruncatedData);
                        break;
                    }
                    bytesReadFromStream += n;
                }

                if (data.AvailableWrite == 0)
                    break;
            }

            return (bytesRead, bytesReadFromStream);
        }

        /// <summary>
        /// Asynchronously compresses data using BZip2 and writes it to the stream.
        /// This override provides true async I/O without blocking.
        /// </summary>
        internal override async System.Threading.Tasks.ValueTask<int> OnWriteAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            int bytesWrittenToStream = 0;

            while (data.AvailableRead > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int before = data.AvailableRead;
                int produced = _encoder!.EncodeData(data, _buffer);

                if (_buffer.AvailableRead > 0)
                {
                    await BaseWriteAsync(_buffer, _buffer.AvailableRead, cancellationToken).ConfigureAwait(false);
                    bytesWrittenToStream += produced;
                }

                if (data.AvailableRead == before && produced == 0)
                    break;
            }

            return bytesWrittenToStream;
        }

        /// <summary>
        /// Asynchronously flushes any remaining compressed data to the stream.
        /// This override provides true async I/O without blocking.
        /// </summary>
        internal override async System.Threading.Tasks.ValueTask<int> OnFlushAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken,
            bool flush,
            bool complete)
        {
            int bytesWrittenToStream = 0;

            if (IsCompress)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bytesWrittenToStream += await OnWriteAsync(data, cancellationToken).ConfigureAwait(false);

                if (flush)
                {
                    bool flushDone;
                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        flushDone = _encoder!.Flush(_buffer, out int flushedBytes);
                        if (flushedBytes > 0)
                        {
                            await BaseWriteAsync(_buffer, flushedBytes, cancellationToken).ConfigureAwait(false);
                            bytesWrittenToStream += flushedBytes;
                        }
                    } while (!flushDone);
                }

                if (complete)
                {
                    bool finished;
                    do
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        finished = _encoder!.Finish(_buffer, out int finishedBytes);
                        if (finishedBytes > 0)
                        {
                            await BaseWriteAsync(_buffer, finishedBytes, cancellationToken).ConfigureAwait(false);
                            bytesWrittenToStream += finishedBytes;
                        }
                    } while (!finished);
                }
            }

            return bytesWrittenToStream;
        }
#endif

        /// <summary>
        /// Disposes the <see cref="BZip2Stream"/> and its resources.
        /// </summary>
        protected override void OnDispose()
        {
            if (IsCompress)
                try { _encoder?.Dispose(); } catch { }
            else
                try { _decoder?.Dispose(); } catch { }
        }
    }
}

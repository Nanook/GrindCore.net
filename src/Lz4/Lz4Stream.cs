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
            // Determine an encoder/decoder block size and compression level using Dictionary settings when provided.
            // Defaults preserve existing behavior (BufferThreshold for block size and this.CompressionType for level).
            int resolvedBlockSize = BufferThreshold;
            int resolvedCompressionLevel = (int)this.CompressionType;

            if (options?.Dictionary != null)
            {
                if (options.Dictionary.DictionarySize.HasValue && options.Dictionary.DictionarySize.Value > 0)
                {
                    // Clamp dictionary size into int range; prefer provided dictionary size for LZ4 block sizing.
                    long ds = options.Dictionary.DictionarySize.Value;
                    if (ds > int.MaxValue)
                        ds = int.MaxValue;
                    resolvedBlockSize = (int)ds;
                }

                if (options.Dictionary.Strategy.HasValue) // For LZ4 we map Strategy (when supplied) to the encoder compression level.
                    resolvedCompressionLevel = options.Dictionary.Strategy.Value;
            }

            if (IsCompress)
            {
                // Map BufferThreshold to LZ4-supported block sizes (64KB,256KB,1MB,4MB).
                // If a threshold was provided, select the smallest LZ4 block size >= threshold.
                // If threshold == 0 (wait-until-full) leave it as 0.

                // Respect both the user-requested threshold and the encoder's recommended input size.
                int target = BufferThreshold != 0 ? Math.Max(BufferThreshold, this.BufferSizeInput) : Math.Max(this.BufferSizeInput, 0x10000);
                int[] lz4Blocks = new int[] { 0x10000, 0x40000, 0x100000, 0x400000 };
                int mapped = lz4Blocks[lz4Blocks.Length - 1];
                foreach (var b in lz4Blocks)
                {
                    if (target <= b)
                    {
                        mapped = b;
                        break;
                    }
                }
                if (BufferThreshold != 0)
                {
                    BufferThreshold = mapped;
                    resolvedBlockSize = BufferThreshold; // use mapped block size for encoder
                }
                else // leave BufferThreshold==0 to indicate wait-until-full behavior
                    resolvedBlockSize = mapped;

                // Ensure we have a valid encoder block size (LZ4 does not accept 0).
                if (resolvedBlockSize <= 0)
                    resolvedBlockSize = this.BufferSizeInput;

                this.BufferSizeOutput = (BufferThreshold != 0) ? BufferThreshold + (BufferThreshold / 255) + 0x10 + 1 : BufferSizeInput + (BufferSizeInput / 255) + 0x10 + 1;
                _encoder = new Lz4Encoder(resolvedBlockSize, resolvedCompressionLevel);
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
            else
            {
                this.BufferSizeOutput = BufferThreshold;

                // Decoder block size: prefer dictionary size if provided, otherwise use BufferSizeOutput.
                int decoderBlockSize = resolvedBlockSize > 0 ? resolvedBlockSize : this.BufferSizeOutput;
                _decoder = new Lz4Decoder(decoderBlockSize);
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

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Asynchronously reads data from the stream and decompresses it using LZ4.
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
            int total = 0;

            while (data.AvailableWrite > total)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_buffer.AvailableRead == 0)
                    await BaseReadAsync(_buffer, _buffer.AvailableWrite, cancellationToken).ConfigureAwait(false);

                if (_buffer.AvailableRead == 0)
                    return (total, bytesReadFromStream);

                int decoded = (int)_decoder.DecodeData(_buffer, out int readSz, data);
                bytesReadFromStream += readSz;
                total += decoded;
            }

            return (total, bytesReadFromStream);
        }

        /// <summary>
        /// Asynchronously compresses data using LZ4 and writes it to the stream.
        /// This override provides true async I/O without blocking.
        /// </summary>
        internal override async System.Threading.Tasks.ValueTask<int> OnWriteAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            int bytesWrittenToStream = 0;
            cancellationToken.ThrowIfCancellationRequested();

            int avRead = data.AvailableRead;
            long size = _encoder.EncodeData(data, _buffer, false, new CancellableTask(cancellationToken));

            if (size > 0)
            {
                _buffer.Tidy(); //reset, no data to shuffle
                await BaseWriteAsync(_buffer, _buffer.AvailableRead, cancellationToken).ConfigureAwait(false);
                bytesWrittenToStream += (int)size;
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
                long size = data.AvailableRead == 0 ? 0 : _encoder.EncodeData(data, _buffer, true, new CancellableTask(cancellationToken));
                size += _encoder.Flush(_buffer, flush, complete);
                if (size > 0)
                {
                    await BaseWriteAsync(_buffer, _buffer.AvailableRead, cancellationToken).ConfigureAwait(false);
                    bytesWrittenToStream = (int)size;
                }
            }
            return bytesWrittenToStream;
        }
#endif

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

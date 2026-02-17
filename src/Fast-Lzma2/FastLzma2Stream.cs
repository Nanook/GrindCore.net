using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Nanook.GrindCore;

namespace Nanook.GrindCore.FastLzma2
{
    /// <summary>
    /// Provides a streaming interface for Fast-LZMA2 compression and decompression.
    /// </summary>
    public class FastLzma2Stream : CompressionStream
    {
        private FastLzma2Encoder _encoder;
        private FastLzma2Decoder _decoder;

        /// <summary>
        /// Gets the input buffer size for Fast-LZMA2 operations.
        /// </summary>
        internal override int BufferSizeInput => 2 * 0x400 * 0x400;

        /// <summary>
        /// Gets the output buffer size for Fast-LZMA2 operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastLzma2Stream"/> class with the specified stream and options.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        public FastLzma2Stream(Stream stream, CompressionOptions options) : this(stream, options, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastLzma2Stream"/> class with the specified stream, options, and compression parameters.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <param name="compressParams">Optional compression parameters for fine-tuning Fast-LZMA2.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        public FastLzma2Stream(Stream stream, CompressionOptions options, CompressionParameters? compressParams = null)
            : base(true, stream, CompressionAlgorithm.FastLzma2, options)
        {
            // Build compression parameters with proper dictionary options handling
            int threads = options?.ThreadCount ?? 1; // Default to single thread like other encoders
            int resolvedLevel = (int)CompressionType;

            if (IsCompress)
                this.BufferSizeOutput = BufferThreshold * 4;
            else
                this.BufferSizeOutput = BufferThreshold;

            if (compressParams == null)
            {
                compressParams = new CompressionParameters(threads);

                // Apply dictionary options directly to Fast-LZMA2 parameters when provided
                if (options?.Dictionary != null)
                {
                    // Dictionary size - only set if explicitly provided
                    if (options.Dictionary.DictionarySize.HasValue && options.Dictionary.DictionarySize.Value > 0)
                    {
                        long ds = options.Dictionary.DictionarySize.Value;
                        compressParams.DictionarySize = (int)Math.Min(ds, int.MaxValue);
                    }

                    // Map other dictionary options to Fast-LZMA2 parameters
                    if (options.Dictionary.FastBytes.HasValue)
                        compressParams.FastLength = options.Dictionary.FastBytes.Value;
                        
                    if (options.Dictionary.LiteralContextBits.HasValue)
                        compressParams.LiteralCtxBits = options.Dictionary.LiteralContextBits.Value;
                        
                    if (options.Dictionary.LiteralPositionBits.HasValue)
                        compressParams.LiteralPosBits = options.Dictionary.LiteralPositionBits.Value;
                        
                    if (options.Dictionary.PositionBits.HasValue)
                        compressParams.PosBits = options.Dictionary.PositionBits.Value;
                        
                    if (options.Dictionary.SearchDepth.HasValue)
                        compressParams.SearchDepth = options.Dictionary.SearchDepth.Value;

                    // Strategy handling - use Strategy from dictionary, then level as fallback
                    if (options.Dictionary.Strategy.HasValue)
                    {
                        resolvedLevel = options.Dictionary.Strategy.Value;
                        compressParams.Strategy = options.Dictionary.Strategy.Value;
                    }
                    // Algorithm mapping: 0=fast -> 1=fast, 1=normal -> 3=ultra
                    else if (options.Dictionary.Algorithm.HasValue)
                    {
                        compressParams.Strategy = options.Dictionary.Algorithm.Value == 0 ? 1 : 3;
                    }
                }
            }

            if (IsCompress)
                _encoder = new FastLzma2Encoder(this.BufferSizeOutput, resolvedLevel, compressParams, options?.Dictionary);
            else
                _decoder = new FastLzma2Decoder(base.BaseRead, BufferSizeOutput, base.BaseLength, resolvedLevel, compressParams);
        }

        /// <summary>
        /// Writes data from the provided buffer to the underlying stream using Fast-LZMA2 compression.
        /// </summary>
        /// <param name="data">The buffer containing data to compress and write.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            int avRead = data.AvailableRead;
            if (avRead == 0)
                return;
            _encoder.EncodeData(data, appending: true, base.BaseWrite, cancel, out bytesWrittenToStream);
        }

        /// <summary>
        /// Reads and decompresses data from the underlying stream into the provided buffer using Fast-LZMA2.
        /// </summary>
        /// <param name="data">The buffer to read decompressed data into.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesReadFromStream">The number of bytes read from the underlying stream.</param>
        /// <param name="length"> The maximum number of bytes to read.If 0, the method will fill the buffer if possible.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream, int length = 0)
        {
            return _decoder.DecodeData(data, base.BaseRead, cancel, out bytesReadFromStream);
        }

        /// <summary>
        /// Flushes the compression buffers and finalizes the stream, writing any remaining compressed data.
        /// </summary>
        /// <param name="data">The buffer containing data to flush.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        /// <exception cref="FL2Exception">Thrown if a fatal compression error occurs.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;

            if (IsCompress)
            {
                cancel.ThrowIfCancellationRequested();
                OnWrite(data, cancel, out bytesWrittenToStream);
                // Always flush at the end of compression
                _encoder.Flush(base.BaseWrite, cancel, out var bytesWrittenToStream2);
                bytesWrittenToStream += bytesWrittenToStream2;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="FastLzma2Stream"/>.
        /// </summary>
        protected override void OnDispose()
        {
            if (IsCompress)
                try { _encoder.Dispose(); } catch { }
            else
                try { _decoder.Dispose(); } catch { }
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Asynchronously reads and decompresses data from the underlying stream into the provided buffer using Fast-LZMA2.
        /// </summary>
        /// <param name="data">The buffer to read decompressed data into.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="length">The maximum number of bytes to read. If 0, the method will fill the buffer if possible.</param>
        /// <returns>A tuple containing (bytes decompressed, bytes read from stream).</returns>
        internal override async System.Threading.Tasks.ValueTask<(int result, int bytesRead)> OnReadAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken,
            int length = 0)
        {
            // FastLzma2Decoder uses a delegate callback for BaseRead, so we wrap the async version
            // The decoder itself is synchronous, but we can make the I/O async
            return await System.Threading.Tasks.Task.Run(() =>
            {
                // Create async-aware read delegate
                Func<CompressionBuffer, int, int> asyncReadWrapper = (buffer, size) =>
                {
                    return BaseReadAsync(buffer, size, cancellationToken).GetAwaiter().GetResult();
                };
                
                int result = _decoder.DecodeData(data, asyncReadWrapper, new CancellableTask(cancellationToken), out int bytesRead);
                return (result, bytesRead);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously writes data from the provided buffer to the underlying stream using Fast-LZMA2 compression.
        /// </summary>
        /// <param name="data">The buffer containing data to compress and write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of bytes written to the stream.</returns>
        internal override async System.Threading.Tasks.ValueTask<int> OnWriteAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken)
        {
            int bytesWrittenToStream = 0;
            int avRead = data.AvailableRead;
            if (avRead == 0)
                return 0;

            // Create async-aware write delegate
            Func<CompressionBuffer, int, int> asyncWriteWrapper = (buffer, length) =>
            {
                return BaseWriteAsync(buffer, length, cancellationToken).GetAwaiter().GetResult();
            };

            await System.Threading.Tasks.Task.Run(() =>
            {
                _encoder.EncodeData(data, appending: true, asyncWriteWrapper, new CancellableTask(cancellationToken), out bytesWrittenToStream);
            }, cancellationToken).ConfigureAwait(false);

            return bytesWrittenToStream;
        }

        /// <summary>
        /// Asynchronously flushes the compression buffers and finalizes the stream, writing any remaining compressed data.
        /// </summary>
        /// <param name="data">The buffer containing data to flush.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        /// <returns>The number of bytes written to the stream.</returns>
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

                // Create async-aware write delegate
                Func<CompressionBuffer, int, int> asyncWriteWrapper = (buffer, length) =>
                {
                    return BaseWriteAsync(buffer, length, cancellationToken).GetAwaiter().GetResult();
                };

                await System.Threading.Tasks.Task.Run(() =>
                {
                    OnWrite(data, new CancellableTask(cancellationToken), out bytesWrittenToStream);
                    _encoder.Flush(asyncWriteWrapper, new CancellableTask(cancellationToken), out var bytesWrittenToStream2);
                    bytesWrittenToStream += bytesWrittenToStream2;
                }, cancellationToken).ConfigureAwait(false);
            }
            return bytesWrittenToStream;
        }
#endif
    }
}

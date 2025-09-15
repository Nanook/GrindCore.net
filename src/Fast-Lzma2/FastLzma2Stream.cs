using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

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
            if (IsCompress)
                this.BufferSizeOutput = BufferThreshold * 4;
            else
                this.BufferSizeOutput = BufferThreshold;

            // Build or override compression parameters from options.Dictionary when provided
            int threads = options.ThreadCount ?? 0;
            int dictSizeParam = 0;
            int resolvedLevel = (int)CompressionType; // default level passed previously

            if (options?.Dictionary != null)
            {
                if (options.Dictionary.DictionarySize.HasValue)
                {
                    long ds = options.Dictionary.DictionarySize.Value;
                    if (ds < 0) ds = 0;
                    if (ds > int.MaxValue) ds = int.MaxValue;
                    dictSizeParam = (int)ds;
                }

                if (options.Dictionary.Strategy.HasValue)
                    resolvedLevel = options.Dictionary.Strategy.Value; // map Strategy to FastLzma2 compression level
            }

            if (compressParams == null)
            {
                compressParams = new CompressionParameters(threads, dictSizeParam);

                // Map some dictionary fields into compression parameters when specified
                if (options?.Dictionary != null)
                {
                    if (options.Dictionary.FastBytes.HasValue)
                        compressParams.FastLength = options.Dictionary.FastBytes.Value;

                    // Strategy already used as resolvedLevel; also expose as CompressionLevel param
                    if (options.Dictionary.Strategy.HasValue)
                        compressParams.CompressionLevel = options.Dictionary.Strategy.Value;
                }
            }

            if (IsCompress)
                _encoder = new FastLzma2Encoder(this.BufferSizeOutput, resolvedLevel, compressParams);
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
    }
}

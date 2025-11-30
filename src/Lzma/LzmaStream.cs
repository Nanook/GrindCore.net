using System;
using System.IO;
using Nanook.GrindCore;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Provides a stream implementation for LZMA compression and decompression.
    /// Inherits common <see cref="Stream"/> functionality from <see cref="CompressionStream"/>.
    /// Uses a customized LZMA implementation to allow the Stream.Write pattern for compression.
    /// </summary>
    public class LzmaStream : CompressionStream
    {
        protected override int MinimumBufferThreshold => 0x10000; // 64 KiB algorithm minimum

        private readonly LzmaDecoder _decoder;
        private readonly LzmaEncoder _encoder;
        private readonly CompressionBuffer _buffer;
        private readonly CompressionBuffer _outBuffer;

        /// <summary>
        /// Gets the input buffer size for LZMA operations.
        /// </summary>
        internal override int BufferSizeInput => 2 * 0x400 * 0x400;

        /// <summary>
        /// Gets the output buffer size for LZMA operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LzmaStream"/> class with the specified stream and options.
        /// Dictionary size is taken from <see cref="CompressionDictionaryOptions.DictionarySize"/>, then <see cref="CompressionOptions.BufferSize"/>,
        /// and finally falls back to the stream default <see cref="BufferSizeInput"/>.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="Exception">Thrown if <paramref name="options"/>.InitProperties is not set when decompressing.</exception>
        public LzmaStream(Stream stream, CompressionOptions options)
            : base(true, stream, CompressionAlgorithm.Lzma, options)
        {
            if (IsCompress)
            {
                // Determine dictionary size using the new CompressionOptions.Dictionary property first,
                // then options.BufferSize, then default input buffer size.
                uint dictSizeToUse;
                long? dictOptSize = options?.Dictionary?.DictionarySize;
                if (dictOptSize.HasValue && dictOptSize.Value != 0)
                {
                    if (dictOptSize.Value < 0 || dictOptSize.Value > uint.MaxValue)
                        throw new ArgumentOutOfRangeException(nameof(options.Dictionary.DictionarySize), $"DictionarySize must be between 0 and {uint.MaxValue}.");
                    dictSizeToUse = (uint)dictOptSize.Value;
                }
                else if (options?.BufferSize is int bs && bs > 0)
                    // Use the actual buffer capacity allocated by the base CompressionStream (snapped)
                    // rather than the raw requested value. This ensures the LZMA encoder uses the
                    // same rounded/allowed buffer size the rest of the stream uses.
                    dictSizeToUse = (uint)(this.InternalBuffer?.Length ?? bs);
                else
                    dictSizeToUse = (uint)this.BufferSizeInput;

                // Build merged dictionary options so encoder can read dictSize and fast-bytes from it.
                CompressionDictionaryOptions mergedDict = options?.Dictionary != null
                    ? new CompressionDictionaryOptions
                    {
                        DictionarySize = options.Dictionary.DictionarySize ?? dictSizeToUse,
                        FastBytes = options.Dictionary.FastBytes,
                        LiteralContextBits = options.Dictionary.LiteralContextBits,
                        LiteralPositionBits = options.Dictionary.LiteralPositionBits,
                        PositionBits = options.Dictionary.PositionBits,
                        Algorithm = options.Dictionary.Algorithm,
                        BinaryTreeMode = options.Dictionary.BinaryTreeMode,
                        HashBytes = options.Dictionary.HashBytes,
                        MatchCycles = options.Dictionary.MatchCycles
                    }
                    : new CompressionDictionaryOptions { DictionarySize = dictSizeToUse };

                // Pass merged dictionary options and thread count into encoder. Provide compression level as fallback.
                _encoder = new LzmaEncoder(mergedDict, options?.ThreadCount, (int)CompressionType);
                // Expose encoder properties so callers can initialize a decompressor.
                Properties = _encoder.Properties;

                // BufferThreshold is enforced by base via MinimumBufferThreshold.
                this.BufferSizeOutput = BufferThreshold + (BufferThreshold >> 1) + 0x10;
                _buffer = new CompressionBuffer((int)Math.Max(dictSizeToUse, this.BufferSizeOutput));
                // Use a larger output buffer to allow native encoder to emit bigger chunks and reduce round-trips
                int outBufSize = this.BufferSizeOutput * 4;
                _outBuffer = new CompressionBuffer(outBufSize);
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

            while (decoded != 0 && total < length)
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

            // Loop: encode then flush output immediately, repeating while input remains.
            // This lets the native encoder produce small chunks that are written out
            // so it can accept further input within the same OnWrite call.
            while (data.AvailableRead > 0)
            {
                cancel.ThrowIfCancellationRequested();

                int before = data.AvailableRead;
                long produced = _encoder.EncodeData(data, _outBuffer, false, cancel);

                if (_outBuffer.AvailableRead > 0)
                {
                    BaseWrite(_outBuffer, _outBuffer.AvailableRead);
                    bytesWrittenToStream += (int)produced;
                }

                int after = data.AvailableRead;
                int consumed = before - after;

                // If neither input was consumed nor output produced, break to avoid busy-loop.
                if (consumed == 0 && produced == 0)
                    break;
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

                // Call EncodeData repeatedly until encoder indicates no more output.
                // Use a separate output buffer so we don't mix input and output buffers.
                const int MAX_ITER = 10000;
                int iter = 0;
                while (true)
                {
                    int beforeIterAvail = data.AvailableRead;
                    if (++iter > MAX_ITER)
                        throw new Exception("LZMA encoder did not make progress (iteration limit reached)");

                    long size = _encoder.EncodeData(data, _outBuffer, true, cancel);
                    if (size == 0 && _outBuffer.AvailableRead == 0)
                        break;

                    if (_outBuffer.AvailableRead > 0)
                    {
                        BaseWrite(_outBuffer, _outBuffer.AvailableRead);
                        bytesWrittenToStream += (int)size;
                    }
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
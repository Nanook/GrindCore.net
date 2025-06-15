using System.Diagnostics;
using System.IO;
using System;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>
    /// Provides methods and properties used to compress and decompress streams by using the Brotli data format specification.
    /// </summary>
    public sealed class BrotliStream : CompressionStream
    {
        private BrotliEncoder _encoder;
        private BrotliDecoder _decoder;
        private CompressionBuffer _buffer;
        private bool _nonEmptyInput;

        /// <summary>
        /// Gets the input buffer size for Brotli operations.
        /// </summary>
        internal override int BufferSizeInput => (1 << 16) - 16; // 65520

        /// <summary>
        /// Gets the output buffer size for Brotli operations.
        /// </summary>
        internal override int BufferSizeOutput => (1 << 16) - 16; // 65520

        /// <summary>
        /// Initializes a new instance of the <see cref="BrotliStream"/> class by using the specified stream and compression options.
        /// </summary>
        /// <param name="stream">The stream to which compressed data is written or from which data to decompress is read.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        public BrotliStream(Stream stream, CompressionOptions options) : base(true, stream, CompressionAlgorithm.Brotli, options)
        {
            if (IsCompress)
            {
                _encoder.SetQuality(CompressionType);
                _encoder.SetWindow();
            }

            _buffer = new CompressionBuffer(this.BufferSizeOutput);
        }

        /// <summary>
        /// Attempts to decompress data from the internal buffer into the output buffer.
        /// </summary>
        /// <param name="outData">The output buffer to write decompressed data to.</param>
        /// <param name="allBytesConsumed">The total number of bytes consumed from the input buffer.</param>
        /// <param name="bytesWritten">The number of bytes written to the output buffer.</param>
        /// <returns>True if decompression produced output or completed; otherwise, false if more data is needed.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the decompressed data is invalid.</exception>
        private bool tryDecompress(CompressionBuffer outData, out int allBytesConsumed, out int bytesWritten)
        {
            allBytesConsumed = 0;
            // Decompress any data we may have in our _outBuffer.
            int bytesConsumed;
            int origAvailableOut = outData.AvailableWrite;
            int origAvailableIn = _buffer.AvailableRead;
            OperationStatus lastResult = _decoder.DecodeData(_buffer, outData, out bytesConsumed, out bytesWritten);
            if (lastResult == OperationStatus.InvalidData)
                throw new InvalidOperationException(SR.BrotliStream_Decompress_InvalidData);

            if (bytesConsumed != 0)
                allBytesConsumed += bytesConsumed;

            // If we successfully decompressed any bytes, or if we've reached the end of the decompression, we're done.
            if (bytesWritten != 0 || lastResult == OperationStatus.Done)
                return true;

            if (origAvailableOut == 0)
            {
                // The caller provided a zero-byte _outBuffer.  This is typically done in order to avoid allocating/renting
                // a _outBuffer until data is known to be available.  We don't have perfect knowledge here, as _decoder.DecodeData
                // will return DestinationTooSmall whether or not more data is required.  As such, we assume that if there's
                // any data in our input _outBuffer, it would have been decompressible into at least one byte of output, and
                // otherwise we need to do a read on the underlying stream.  This isn't perfect, because having input data
                // doesn't necessarily mean it'll decompress into at least one byte of output, but it's a reasonable approximation
                // for the 99% case.  If it's wrong, it just means that a caller using zero-byte reads as a way to delay
                // getting a _outBuffer to use for a subsequent call may end up getting one earlier than otherwise preferred.
                Debug.Assert(lastResult == OperationStatus.DestinationTooSmall);
                if (origAvailableIn != 0)
                {
                    Debug.Assert(bytesWritten == 0);
                    return true;
                }
            }

            Debug.Assert(
                lastResult == OperationStatus.NeedMoreData ||
                (lastResult == OperationStatus.DestinationTooSmall && origAvailableOut == 0 && origAvailableIn == 0), $"{nameof(lastResult)} == {lastResult}, {nameof(outData.AvailableWrite)} == {origAvailableOut}");


            return false;
        }

        /// <summary>
        /// Reads and decompresses data from the underlying stream into the provided buffer.
        /// </summary>
        /// <param name="data">The buffer to read decompressed data into.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesReadFromStream">The number of bytes read from the underlying stream.</param>
        /// <param name="length"> The maximum number of bytes to read.If 0, the method will fill the buffer if possible.</param>
        /// <returns>The number of bytes written to the output buffer.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the stream is in compression mode.</exception>
        /// <exception cref="InvalidDataException">Thrown if the stream is truncated or invalid.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the stream is disposed.</exception>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream, int length = 0)
        {
            if (IsCompress)
                throw new InvalidOperationException(SR.BrotliStream_Compress_UnsupportedOperation);
            bytesReadFromStream = 0;
            int bytesWritten;
            int bytesConsumed;
            while (!tryDecompress(data, out bytesConsumed, out bytesWritten))
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken
                bytesReadFromStream += bytesConsumed; //read from the compressed stream (that were used - important distinction)

                int available = _buffer.AvailableWrite;
                int bytesRead = BaseRead(_buffer, available);
                if (bytesRead <= 0)
                {
                    if (/*s_useStrictValidation &&*/ _nonEmptyInput && available != 0)
                        throw new InvalidDataException(SR.BrotliStream_Decompress_TruncatedData);
                    break;
                }

                _nonEmptyInput = true;

                // The stream is either malicious or poorly implemented and returned a number of
                // bytes larger than the _outBuffer supplied to it.
                if (bytesRead > available)
                    throw new InvalidDataException(SR.BrotliStream_Decompress_InvalidStream);
            }

            bytesReadFromStream += bytesConsumed; //read from the compressed stream (that were used - important distinction)

            return bytesWritten;
        }

        /// <summary>
        /// Writes compressed data from the provided buffer to the underlying stream.
        /// </summary>
        /// <param name="data">The buffer containing data to write.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <exception cref="InvalidOperationException">Thrown if the stream is in decompression mode.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the stream is disposed.</exception>
        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            OnWrite(data, cancel, out bytesWrittenToStream, false);
        }

        /// <summary>
        /// Writes compressed data from the provided buffer to the underlying stream, with an option to indicate the final block.
        /// </summary>
        /// <param name="data">The buffer containing data to write.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="isFinalBlock">Indicates whether this is the final block of data.</param>
        /// <exception cref="InvalidOperationException">Thrown if the stream is in decompression mode.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the stream is disposed.</exception>
        internal void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool isFinalBlock)
        {
            if (!IsCompress)
                throw new InvalidOperationException(SR.BrotliStream_Decompress_UnsupportedOperation);

            bytesWrittenToStream = 0;

            OperationStatus lastResult = OperationStatus.DestinationTooSmall;
            while (lastResult == OperationStatus.DestinationTooSmall)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                int bytesConsumed;
                int bytesWritten;
                lastResult = _encoder.EncodeData(data, _buffer, out bytesConsumed, out bytesWritten, isFinalBlock);
                if (lastResult == OperationStatus.InvalidData)
                    throw new InvalidOperationException(SR.BrotliStream_Compress_InvalidData);

                bytesWrittenToStream += bytesWritten;

                if (bytesWritten > 0)
                    BaseWrite(_buffer, bytesWritten); //read the output bytes and write to BaseStream
            }
        }

        /// <summary>
        /// Flushes the compression buffers and finalizes stream writes and positions.
        /// </summary>
        /// <param name="data">The buffer containing data to flush.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        /// <exception cref="InvalidDataException">The encoder ran into invalid data.</exception>
        /// <exception cref="ObjectDisposedException">The stream is disposed.</exception>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;

            if (IsCompress)
            {
                if (_encoder._state == null || _encoder._state.IsClosed)
                    return;

                OnWrite(data, cancel, out bytesWrittenToStream, true); //data may have 0 bytes

                OperationStatus lastResult = OperationStatus.DestinationTooSmall;
                while (flush && lastResult == OperationStatus.DestinationTooSmall)
                {
                    cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                    int bytesWritten = 0;

                    lastResult = _encoder.Flush(_buffer, out bytesWritten);
                    if (lastResult == OperationStatus.InvalidData)
                        throw new InvalidDataException(SR.BrotliStream_Compress_InvalidData);

                    bytesWrittenToStream += bytesWritten;

                    if (bytesWritten > 0)
                        BaseWrite(_buffer, bytesWritten); //read the output bytes and write to BaseStream
                }
            }
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="BrotliStream"/> and optionally releases the managed resources.
        /// </summary>
        protected override void OnDispose()
        {
            if (IsCompress)
                try { _encoder.Dispose(); } catch { }
            else
                try { _decoder.Dispose(); } catch { }
            try { _buffer.Dispose(); } catch { }
        }
    }
}

using System.Diagnostics;
using System.Threading;
using System.IO;
using System;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>Provides methods and properties used to compress and decompress streams by using the Brotli data format specification.</summary>
    public sealed class BrotliStream : CompressionStream, ICompressionDefaults
    {
        private BrotliEncoder _encoder;
        private BrotliDecoder _decoder;
        private CompressionBuffer _buffer;
        private bool _nonEmptyInput;

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.Brotli;
        internal override int DefaultBufferOverflowSize => 0x200000;
        internal override int DefaultBufferSize => 0x200000;

        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level4;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxBrotli;

        /// <summary>Initializes a new instance of the <see cref="Nanook.GrindCore.BrotliStream" /> class by using the specified stream and compression mode, and optionally leaves the stream open.</summary>
        /// <param name="stream">The stream to which compressed data is written or from which data to decompress is read.</param>
        /// <param name="mode">One of the enumeration values that indicates whether to compress data to the stream or decompress data from the stream.</param>
        /// <param name="leaveOpen"><see langword="true" /> to leave the stream open after the <see cref="Nanook.GrindCore.BrotliStream" /> object is _disposed; otherwise, <see langword="false" />.</param>
        public BrotliStream(Stream stream, CompressionOptions options) : base(true, stream, options)
        {
            if (IsCompress)
            {
                _encoder.SetQuality(CompressionType);
                _encoder.SetWindow();
            }

            _buffer = new CompressionBuffer((1 << 16) - 16); //65520
        }

        private bool tryDecompress(CompressionBuffer outData, out int allBytesConsumed, out int bytesWritten)
        {
            allBytesConsumed = 0;
            // Decompress any data we may have in our _outBuffer.
            int bytesConsumed;
            int origAvailableOut = outData.AvailableWrite;
            int origAvailableIn = _buffer.AvailableRead;
            OperationStatus lastResult = _decoder.Decompress(_buffer, outData, out bytesConsumed, out bytesWritten);
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
                // a _outBuffer until data is known to be available.  We don't have perfect knowledge here, as _decoder.Decompress
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

            // Ensure any left over data is at the beginning of the array so we can fill the remainder.
            _buffer.Tidy(); //move any data back to start

            return false;
        }

        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            if (IsCompress)
                throw new InvalidOperationException(SR.BrotliStream_Compress_UnsupportedOperation);
            EnsureNotDisposed();
            bytesReadFromStream = 0;
            int bytesWritten;
            int bytesConsumed;
            while (!tryDecompress(data, out bytesConsumed, out bytesWritten))
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken
                bytesReadFromStream += bytesConsumed; //read from the compressed stream (that were used - important distiction)

                int bytesRead = BaseStream.Read(_buffer.Data, _buffer.Size, _buffer.AvailableWrite);
                if (bytesRead <= 0)
                {
                    if (/*s_useStrictValidation &&*/ _nonEmptyInput && data.AvailableWrite != 0)
                        throw new InvalidDataException(SR.BrotliStream_Decompress_TruncatedData);
                    break;
                }

                _nonEmptyInput = true;

                // The stream is either malicious or poorly implemented and returned a number of
                // bytes larger than the _outBuffer supplied to it.
                if (bytesRead > _buffer.AvailableWrite)
                    throw new InvalidDataException(SR.BrotliStream_Decompress_InvalidStream);


                _buffer.Write(bytesRead); //update bytes written to _buffer
            }

            bytesReadFromStream += bytesConsumed; //read from the compressed stream (that were used - important distiction)

            return bytesWritten;
        }

        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            OnWrite(data, cancel, out bytesWrittenToStream, false);
        }
        internal void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool isFinalBlock)
        {
            if (!IsCompress)
                throw new InvalidOperationException(SR.BrotliStream_Decompress_UnsupportedOperation);
            EnsureNotDisposed();

            bytesWrittenToStream = 0;

            OperationStatus lastResult = OperationStatus.DestinationTooSmall;
            while (lastResult == OperationStatus.DestinationTooSmall)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                int bytesConsumed;
                int bytesWritten;
                lastResult = _encoder.Compress(data, _buffer, out bytesConsumed, out bytesWritten, isFinalBlock);
                if (lastResult == OperationStatus.InvalidData)
                    throw new InvalidOperationException(SR.BrotliStream_Compress_InvalidData);

                bytesWrittenToStream += bytesWritten;

                if (bytesWritten > 0)
                {
                    BaseStream.Write(_buffer.Data, 0, bytesWritten); //read the output bytes and write to BaseStream
                    _buffer.Read(bytesWritten);
                }
            }
        }

        /// <summary>If the stream is not _disposed, and the compression mode is set to compress, writes all the remaining encoder's data into this stream.</summary>
        /// <exception cref="InvalidDataException">The encoder ran into invalid data.</exception>
        /// <exception cref="ObjectDisposedException">The stream is _disposed.</exception>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            EnsureNotDisposed();

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
                    {
                        BaseStream.Write(_buffer.Data, 0, bytesWritten); //read the output bytes and write to BaseStream
                        _buffer.Read(bytesWritten);
                    }
                }
            }
        }

        private void EnsureNotDisposed()
        {
            if (BaseStream == null)
                throw new ObjectDisposedException(nameof(BrotliStream));
        }

        /// <summary>Releases the unmanaged resources used by the <see cref="Nanook.GrindCore.BrotliStream" /> and optionally releases the managed resources.</summary>
        /// <param name="disposing"><see langword="true" /> to release both managed and unmanaged resources; <see langword="false" /> to release only unmanaged resources.</param>
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

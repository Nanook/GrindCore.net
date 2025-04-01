using System.Diagnostics;
using System.Threading;
using System.IO;
using System;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>Provides methods and properties used to compress and decompress streams by using the Brotli data format specification.</summary>
    public sealed class BrotliStream : StreamBase
    {
        private const int DefaultInternalBufferSize = (1 << 16) - 16; //65520;
        private Stream _baseStream;
        private BrotliEncoder _encoder;
        private BrotliDecoder _decoder;
        private int _bufferOffset;
        private int _bufferCount;
        private bool _nonEmptyInput;
        private byte[] _buffer;
        private readonly bool _leaveOpen;
        private readonly bool _compress;
        private readonly CompressionVersion _version;

        public override bool CanRead => _baseStream != null && !_compress && _baseStream.CanRead;
        public override bool CanWrite => _baseStream != null && _compress && _baseStream.CanWrite;
        public override bool CanSeek => false;
        public Stream BaseStream => _baseStream;

        /// <summary>Initializes a new instance of the <see cref="Nanook.GrindCore.BrotliStream" /> class by using the specified stream and compression mode.</summary>
        /// <param name="stream">The stream to which compressed data is written or from which data to decompress is read.</param>
        /// <param name="type">CompressionLevel or Decompress, indicates whether to emphasize speed or compression efficiency when compressing data to the stream.</param>
        public BrotliStream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, leaveOpen: false, version) { }

        /// <summary>Initializes a new instance of the <see cref="Nanook.GrindCore.BrotliStream" /> class by using the specified stream and compression mode, and optionally leaves the stream open.</summary>
        /// <param name="stream">The stream to which compressed data is written or from which data to decompress is read.</param>
        /// <param name="mode">One of the enumeration values that indicates whether to compress data to the stream or decompress data from the stream.</param>
        /// <param name="leaveOpen"><see langword="true" /> to leave the stream open after the <see cref="Nanook.GrindCore.BrotliStream" /> object is _disposed; otherwise, <see langword="false" />.</param>
        public BrotliStream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null) : base(true)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _version = version ?? CompressionVersion.BrotliLatest();

            if (type != CompressionType.Decompress)
            {
                if (!stream.CanWrite)
                    throw new ArgumentException(SR.Stream_FalseCanWrite, nameof(stream));

                _encoder.SetQuality(BrotliUtils.GetQualityFromCompressionLevel(type));
                _encoder.SetWindow(BrotliUtils.WindowBits_Default);
                _compress = true;
            }
            else
            {
                if (!stream.CanRead)
                    throw new ArgumentException(SR.Stream_FalseCanRead, nameof(stream));

                _compress = false;
            }

            _baseStream = stream;
            _leaveOpen = leaveOpen;
            _buffer = new byte[DefaultInternalBufferSize];
        }

        internal override int OnRead(DataBlock buffer)
        {
            if (_compress)
                throw new InvalidOperationException(SR.BrotliStream_Compress_UnsupportedOperation);
            EnsureNotDisposed();

            int bytesWritten;
            while (!TryDecompress(buffer, out bytesWritten))
            {
                int bytesRead = _baseStream.Read(_buffer, _bufferCount, _buffer.Length - _bufferCount);
                if (bytesRead <= 0)
                {
                    if (/*s_useStrictValidation &&*/ _nonEmptyInput && buffer.Length != 0)
                        throw new InvalidDataException(SR.BrotliStream_Decompress_TruncatedData);
                    break;
                }

                _nonEmptyInput = true;
                _bufferCount += bytesRead;

                // The stream is either malicious or poorly implemented and returned a number of
                // bytes larger than the buffer supplied to it.
                if (_bufferCount > _buffer.Length)
                    throw new InvalidDataException(SR.BrotliStream_Decompress_InvalidStream);
            }

            return bytesWritten;
        }

        internal override void OnWrite(DataBlock buffer)
        {
            OnWrite(buffer, false);
        }
        internal void OnWrite(DataBlock buffer, bool isFinalBlock)
        {
            if (!_compress)
                throw new InvalidOperationException(SR.BrotliStream_Decompress_UnsupportedOperation);
            EnsureNotDisposed();

            OperationStatus lastResult = OperationStatus.DestinationTooSmall;
            DataBlock output = new DataBlock(_buffer);
            while (lastResult == OperationStatus.DestinationTooSmall)
            {
                int bytesConsumed;
                int bytesWritten;
                lastResult = _encoder.Compress(buffer, output, out bytesConsumed, out bytesWritten, isFinalBlock);
                if (lastResult == OperationStatus.InvalidData)
                    throw new InvalidOperationException(SR.BrotliStream_Compress_InvalidData);
                if (bytesWritten > 0)
                    output.Read(0, _baseStream, bytesWritten); //read the output bytes and write to _baseStream
                if (bytesConsumed > 0 && bytesConsumed < output.Length)
                    buffer = new DataBlock(output.Data, bytesConsumed, output.Length - bytesConsumed);
            }
        }

        /// <summary>If the stream is not _disposed, and the compression mode is set to compress, writes all the remaining encoder's data into this stream.</summary>
        /// <exception cref="InvalidDataException">The encoder ran into invalid data.</exception>
        /// <exception cref="ObjectDisposedException">The stream is _disposed.</exception>
        internal override void OnFlush()
        {
            EnsureNotDisposed();
            if (_compress)
            {
                if (_encoder._state == null || _encoder._state.IsClosed)
                    return;

                OperationStatus lastResult = OperationStatus.DestinationTooSmall;
                DataBlock output = new DataBlock(_buffer);
                while (lastResult == OperationStatus.DestinationTooSmall)
                {
                    int bytesWritten;
                    lastResult = _encoder.Flush(output, out bytesWritten);
                    if (lastResult == OperationStatus.InvalidData)
                        throw new InvalidDataException(SR.BrotliStream_Compress_InvalidData);
                    if (bytesWritten > 0)
                        output.Read(0, _baseStream, bytesWritten); //read the output bytes and write to _baseStream
                }

                _baseStream.Flush();
            }
        }

        private bool TryDecompress(DataBlock destination, out int bytesWritten)
        {
            // Decompress any data we may have in our buffer.
            OperationStatus lastResult = _decoder.Decompress(new DataBlock(_buffer, _bufferOffset, _bufferCount), destination, out int bytesConsumed, out bytesWritten);
            if (lastResult == OperationStatus.InvalidData)
            {
                throw new InvalidOperationException(SR.BrotliStream_Decompress_InvalidData);
            }

            if (bytesConsumed != 0)
            {
                _bufferOffset += bytesConsumed;
                _bufferCount -= bytesConsumed;
            }

            // If we successfully decompressed any bytes, or if we've reached the end of the decompression, we're done.
            if (bytesWritten != 0 || lastResult == OperationStatus.Done)
            {
                return true;
            }

            if (destination.Length == 0)
            {
                // The caller provided a zero-byte buffer.  This is typically done in order to avoid allocating/renting
                // a buffer until data is known to be available.  We don't have perfect knowledge here, as _decoder.Decompress
                // will return DestinationTooSmall whether or not more data is required.  As such, we assume that if there's
                // any data in our input buffer, it would have been decompressible into at least one byte of output, and
                // otherwise we need to do a read on the underlying stream.  This isn't perfect, because having input data
                // doesn't necessarily mean it'll decompress into at least one byte of output, but it's a reasonable approximation
                // for the 99% case.  If it's wrong, it just means that a caller using zero-byte reads as a way to delay
                // getting a buffer to use for a subsequent call may end up getting one earlier than otherwise preferred.
                Debug.Assert(lastResult == OperationStatus.DestinationTooSmall);
                if (_bufferCount != 0)
                {
                    Debug.Assert(bytesWritten == 0);
                    return true;
                }
            }

            Debug.Assert(
                lastResult == OperationStatus.NeedMoreData ||
                (lastResult == OperationStatus.DestinationTooSmall && destination.Length == 0 && _bufferCount == 0), $"{nameof(lastResult)} == {lastResult}, {nameof(destination.Length)} == {destination.Length}");

            // Ensure any left over data is at the beginning of the array so we can fill the remainder.
            if (_bufferCount != 0 && _bufferOffset != 0)
                Array.Copy(_buffer, _bufferOffset, _buffer, 0, _bufferCount);
            _bufferOffset = 0;

            return false;
        }

        private void EnsureNotDisposed()
        {
            if (_baseStream == null)
                throw new ObjectDisposedException(nameof(BrotliStream));
        }

        /// <summary>Releases the unmanaged resources used by the <see cref="Nanook.GrindCore.BrotliStream" /> and optionally releases the managed resources.</summary>
        /// <param name="disposing"><see langword="true" /> to release both managed and unmanaged resources; <see langword="false" /> to release only unmanaged resources.</param>
        protected override void OnDispose()
        {
            try
            {
                if (_baseStream != null)
                {
                    if (_compress)
                        OnWrite(new DataBlock(), true);

                    if (!_leaveOpen)
                        _baseStream.Dispose();
                }
            }
            finally
            {
                _baseStream = null!;
                _encoder.Dispose();
                _decoder.Dispose();
            }
        }
    }
}

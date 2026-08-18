using System;
using System.IO;
using System.IO.Compression;

#if !CLASSIC && (NET45_OR_GREATER || NETSTANDARD || NETCOREAPP)
using System.Threading;
using System.Threading.Tasks;
#endif

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a stream implementation for Zstandard (ZStd) seekable compression and decompression.
    /// Seekable streams allow random access decompression via frame-based organization.
    /// 
    /// For compression: Creates seekable archives with configurable frame sizes.
    /// For decompression: Supports random access reads from seekable archives.
    /// 
    /// This class supports multiple ZStd versions (v1.5.2 and v1.5.7) via the <see cref="CompressionVersion"/> parameter.
    /// If no version is specified, the latest version (v1.5.7) is used.
    /// </summary>
    public class ZStdSeekableStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly bool _leaveOpen;
        private readonly CompressionMode _mode;
        private readonly bool _useV152;

        // Single encoder/decoder - version determined by polymorphism
        private readonly ZStdSeekableEncoder _encoder;
        private readonly ZStdSeekableDecoder _decoder;

        private readonly CompressionBuffer _buffer;
        private long _position;
        private bool _disposed;
        private bool _wroteData;
        private bool _finalized;

        /// <summary>
        /// Gets a value indicating whether the current stream supports reading (decompression).
        /// </summary>
        public override bool CanRead => _mode == CompressionMode.Decompress;

        /// <summary>
        /// Gets a value indicating whether the current stream supports seeking.
        /// Only supported for decompression mode with seekable archives.
        /// </summary>
        public override bool CanSeek => _mode == CompressionMode.Decompress && _decoder != null;

        /// <summary>
        /// Gets a value indicating whether the current stream supports writing (compression).
        /// </summary>
        public override bool CanWrite => _mode == CompressionMode.Compress;

        /// <summary>
        /// Gets the length of the stream (decompressed size for read mode).
        /// Only supported in decompression mode.
        /// </summary>
        public override long Length
        {
            get
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ZStdSeekableStream));
                if (_mode != CompressionMode.Decompress)
                    throw new NotSupportedException("Length is only available in decompression mode");
                return (long)DecompressedSize;
            }
        }

        /// <summary>
        /// Gets or sets the position within the current stream.
        /// For decompression, supports seeking to any position.
        /// For compression, only reports current write position (no seeking).
        /// </summary>
        public override long Position
        {
            get => _position;
            set
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(ZStdSeekableStream));
                if (_mode != CompressionMode.Decompress)
                    throw new NotSupportedException("Seeking is only supported in decompression mode");
                Seek(value, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// Gets the total decompressed size (only available in decompression mode).
        /// </summary>
        private ulong DecompressedSize => _decoder.DecompressedSize;

        /// <summary>
        /// Gets the output buffer size from the active encoder.
        /// </summary>
        private int EncoderOutputBufferSize => _encoder.OutputBufferSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableStream"/> class for compression.
        /// </summary>
        /// <param name="stream">The base stream to write compressed data to.</param>
        /// <param name="maxFrameSize">Maximum size of each seekable frame in bytes (default 1MB). Must be greater than zero.</param>
        /// <param name="compressionLevel">The compression level (1-22, default 3).</param>
        /// <param name="leaveOpen">True to leave the base stream open after disposing this stream.</param>
        /// <param name="version">The ZStd version to use. If null, the latest version is used.</param>
        public ZStdSeekableStream(Stream stream, int maxFrameSize = 1024 * 1024, int compressionLevel = 3, bool leaveOpen = false, CompressionVersion? version = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable for compression", nameof(stream));
            if (maxFrameSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxFrameSize), "maxFrameSize must be greater than zero");

            _baseStream = stream;
            _mode = CompressionMode.Compress;
            _leaveOpen = leaveOpen;
            _position = 0;
            _disposed = false;
            _wroteData = false;
            _finalized = false;

            // Resolve version - default to latest
            CompressionVersion resolvedVersion = version ?? CompressionVersion.ZStdLatest();
            _useV152 = resolvedVersion.Index == 1; // Index 1 = v1.5.2

            int bufferSize = 128 * 1024; // 128 KB buffer
            if (_useV152)
                _encoder = new ZStdSeekableEncoderV1_5_2(bufferSize, (uint)maxFrameSize, compressionLevel, 1);
            else
                _encoder = new ZStdSeekableEncoder(bufferSize, (uint)maxFrameSize, compressionLevel, 1);
            _buffer = new CompressionBuffer(bufferSize);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableStream"/> class for decompression from a stream.
        /// </summary>
        /// <param name="stream">The base stream to read compressed data from. Must be readable and seekable.</param>
        /// <param name="leaveOpen">True to leave the base stream open after disposing this stream.</param>
        /// <param name="version">The ZStd version to use. If null, the latest version is used.</param>
        public ZStdSeekableStream(Stream stream, bool leaveOpen = false, CompressionVersion? version = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable for decompression", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable for random access decompression", nameof(stream));

            _baseStream = stream;
            _mode = CompressionMode.Decompress;
            _leaveOpen = leaveOpen;
            _position = 0;
            _disposed = false;

            // Resolve version - default to latest
            CompressionVersion resolvedVersion = version ?? CompressionVersion.ZStdLatest();
            _useV152 = resolvedVersion.Index == 1; // Index 1 = v1.5.2

            if (_useV152)
                _decoder = new ZStdSeekableDecoderV1_5_2(stream);
            else
                _decoder = new ZStdSeekableDecoder(stream);
            int bufferSize = 128 * 1024;
            _buffer = new CompressionBuffer(bufferSize);
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));
            if (_mode != CompressionMode.Decompress)
                throw new NotSupportedException("Read is only supported in decompression mode");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (offset + count > buffer.Length)
                throw new ArgumentException("Offset and count exceed buffer length");

            ulong decompressedSize = DecompressedSize;
            if (count == 0 || _position >= (long)decompressedSize)
                return 0;

            // Clamp read to remaining data
            long remaining = (long)decompressedSize - _position;
            if (count > remaining)
                count = (int)remaining;

            // Create a temporary buffer for decompression
            byte[] tempBuffer = new byte[count];
            int bytesRead = _decoder.Decompress(tempBuffer, (ulong)_position);

            // Copy to user buffer
            Array.Copy(tempBuffer, 0, buffer, offset, bytesRead);
            _position += bytesRead;

            return bytesRead;
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the position.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));
            if (_mode != CompressionMode.Compress)
                throw new NotSupportedException("Write is only supported in compression mode");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (offset + count > buffer.Length)
                throw new ArgumentException("Offset and count exceed buffer length");

            if (count == 0)
                return;

            _wroteData = true;

            // Create input buffer
            CompressionBuffer inBuffer = new CompressionBuffer(count);
            inBuffer.Write(buffer, offset, count);
            inBuffer.Pos = 0;

            // Compress data
            CompressionBuffer outBuffer = new CompressionBuffer(EncoderOutputBufferSize);
            long compressed = _encoder.EncodeData(inBuffer, outBuffer, false, new CancellableTask());

            // Write compressed data to base stream
            if (compressed > 0)
            {
                _baseStream.Write(outBuffer.Data, 0, outBuffer.Size);
            }

            _position += count;
        }

        /// <summary>
        /// Flushes the compression buffer and writes all pending data.
        /// </summary>
        public override void Flush()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));

            if (_mode == CompressionMode.Compress && !_finalized)
            {
                _finalized = true;

                // Finalize compression and write seek table
                CompressionBuffer inBuffer = new CompressionBuffer(0); // Empty input
                CompressionBuffer outBuffer = new CompressionBuffer(EncoderOutputBufferSize);

                long compressed = _encoder.EncodeData(inBuffer, outBuffer, true, new CancellableTask());

                if (compressed > 0)
                {
                    _baseStream.Write(outBuffer.Data, 0, outBuffer.Size);
                }
            }

            _baseStream?.Flush();
        }

        /// <summary>
        /// Sets the position within the current stream (decompression mode only).
        /// </summary>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));
            if (_mode != CompressionMode.Decompress)
                throw new NotSupportedException("Seek is only supported in decompression mode");

            long decompSize = (long)DecompressedSize;

            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => decompSize + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (newPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Seek position is before the beginning of the stream");
            if (newPosition > decompSize)
                throw new ArgumentOutOfRangeException(nameof(offset), "Seek position is beyond the end of the stream");

            _position = newPosition;
            return _position;
        }

        /// <summary>
        /// Not supported for seekable streams.
        /// </summary>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("SetLength is not supported on ZStdSeekableStream");
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream and advances the position.
        /// </summary>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));
            if (_mode != CompressionMode.Decompress)
                throw new NotSupportedException("Read is only supported in decompression mode");

            cancellationToken.ThrowIfCancellationRequested();

            // Decompression is CPU-bound (native call against pinned memory/callbacks),
            // so we perform it synchronously and return the result as a completed ValueTask.
            int count = buffer.Length;
            ulong decompressedSize = DecompressedSize;
            if (count == 0 || _position >= (long)decompressedSize)
                return new ValueTask<int>(0);

            long remaining = (long)decompressedSize - _position;
            if (count > remaining)
                count = (int)remaining;

            byte[] tempBuffer = new byte[count];
            int bytesRead = _decoder.Decompress(tempBuffer, (ulong)_position);

            tempBuffer.AsSpan(0, bytesRead).CopyTo(buffer.Span);
            _position += bytesRead;

            return new ValueTask<int>(bytesRead);
        }

        /// <summary>
        /// Asynchronously writes a sequence of bytes to the current stream and advances the position.
        /// </summary>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));
            if (_mode != CompressionMode.Compress)
                throw new NotSupportedException("Write is only supported in compression mode");

            cancellationToken.ThrowIfCancellationRequested();

            int count = buffer.Length;
            if (count == 0)
                return;

            _wroteData = true;

            // Create input buffer from the memory span
            CompressionBuffer inBuffer = new CompressionBuffer(count);
            buffer.Span.CopyTo(inBuffer.Data.AsSpan(0, count));
            inBuffer.Write(count);
            inBuffer.Pos = 0;

            // Compress data (CPU-bound, synchronous)
            CompressionBuffer outBuffer = new CompressionBuffer(EncoderOutputBufferSize);
            long compressed = _encoder.EncodeData(inBuffer, outBuffer, false, new CancellableTask(cancellationToken));

            // Write compressed data to base stream asynchronously
            if (compressed > 0)
            {
                await _baseStream.WriteAsync(outBuffer.Data.AsMemory(0, outBuffer.Size), cancellationToken).ConfigureAwait(false);
            }

            _position += count;
        }
#endif

#if CLASSIC || NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream and advances the position.
        /// </summary>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));
            if (_mode != CompressionMode.Decompress)
                throw new NotSupportedException("Read is only supported in decompression mode");

            cancellationToken.ThrowIfCancellationRequested();

            // Decompression is CPU-bound, return synchronously as completed task
            int result = Read(buffer, offset, count);
            return Task.FromResult(result);
        }

        /// <summary>
        /// Asynchronously writes a sequence of bytes to the current stream and advances the position.
        /// </summary>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));
            if (_mode != CompressionMode.Compress)
                throw new NotSupportedException("Write is only supported in compression mode");
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (offset + count > buffer.Length)
                throw new ArgumentException("Offset and count exceed buffer length");

            cancellationToken.ThrowIfCancellationRequested();

            if (count == 0)
                return;

            _wroteData = true;

            // Create input buffer
            CompressionBuffer inBuffer = new CompressionBuffer(count);
            inBuffer.Write(buffer, offset, count);
            inBuffer.Pos = 0;

            // Compress data (CPU-bound, synchronous)
            CompressionBuffer outBuffer = new CompressionBuffer(EncoderOutputBufferSize);
            long compressed = _encoder.EncodeData(inBuffer, outBuffer, false, new CancellableTask(cancellationToken));

            // Write compressed data to base stream asynchronously
            if (compressed > 0)
            {
#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
                await _baseStream.WriteAsync(outBuffer.Data.AsMemory(0, outBuffer.Size), cancellationToken).ConfigureAwait(false);
#else
                await _baseStream.WriteAsync(outBuffer.Data, 0, outBuffer.Size, cancellationToken).ConfigureAwait(false);
#endif
            }

            _position += count;
        }

        /// <summary>
        /// Asynchronously flushes the compression buffer and writes all pending data.
        /// </summary>
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream));

            cancellationToken.ThrowIfCancellationRequested();

            if (_mode == CompressionMode.Compress && !_finalized)
            {
                _finalized = true;

                // Finalize compression and write seek table (CPU-bound)
                CompressionBuffer inBuffer = new CompressionBuffer(0);
                CompressionBuffer outBuffer = new CompressionBuffer(EncoderOutputBufferSize);

                long compressed = _encoder.EncodeData(inBuffer, outBuffer, true, new CancellableTask(cancellationToken));

                if (compressed > 0)
                {
#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
                    await _baseStream.WriteAsync(outBuffer.Data.AsMemory(0, outBuffer.Size), cancellationToken).ConfigureAwait(false);
#else
                    await _baseStream.WriteAsync(outBuffer.Data, 0, outBuffer.Size, cancellationToken).ConfigureAwait(false);
#endif
                }
            }

            await _baseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
#endif

        /// <summary>
        /// Releases the unmanaged resources used by the stream and optionally releases the managed resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Finalize compression if in write mode (no-op if already finalized)
                    if (_mode == CompressionMode.Compress && _wroteData)
                    {
                        try
                        {
                            Flush();
                        }
                        catch
                        {
                            // Suppress exceptions during disposal
                        }
                    }

                    _encoder?.Dispose();
                    _decoder?.Dispose();

                    if (!_leaveOpen)
                    {
                        _baseStream?.Dispose();
                    }
                }

                _disposed = true;
            }

            base.Dispose(disposing);
        }
    }
}

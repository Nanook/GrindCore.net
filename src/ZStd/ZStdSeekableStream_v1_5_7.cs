using System;
using System.IO;
using System.IO.Compression;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a stream implementation for Zstandard (ZStd) seekable compression and decompression.
    /// Seekable streams allow random access decompression via frame-based organization.
    /// 
    /// For compression: Creates seekable archives with configurable frame sizes.
    /// For decompression: Supports random access reads from seekable archives.
    /// </summary>
    public class ZStdSeekableStream_v1_5_7 : Stream
    {
        private readonly Stream _baseStream;
        private readonly bool _leaveOpen;
        private readonly CompressionMode _mode;
        private readonly ZStdSeekableEncoder_v1_5_7 _encoder;
        private readonly ZStdSeekableDecoder_v1_5_7 _decoder;
        private readonly CompressionBuffer _buffer;
        private long _position;
        private bool _disposed;
        private bool _wroteData;

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
                    throw new ObjectDisposedException(nameof(ZStdSeekableStream_v1_5_7));
                if (_mode != CompressionMode.Decompress || _decoder == null)
                    throw new NotSupportedException("Length is only available in decompression mode");
                return (long)_decoder.DecompressedSize;
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
                    throw new ObjectDisposedException(nameof(ZStdSeekableStream_v1_5_7));
                if (_mode != CompressionMode.Decompress)
                    throw new NotSupportedException("Seeking is only supported in decompression mode");
                Seek(value, SeekOrigin.Begin);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableStream_v1_5_7"/> class for compression.
        /// </summary>
        /// <param name="stream">The base stream to write compressed data to.</param>
        /// <param name="maxFrameSize">Maximum size of each seekable frame (default 1MB).</param>
        /// <param name="compressionLevel">The compression level (1-22, default 3).</param>
        /// <param name="leaveOpen">True to leave the base stream open after disposing this stream.</param>
        [CLSCompliant(false)]
        public ZStdSeekableStream_v1_5_7(Stream stream, uint maxFrameSize = 1024 * 1024, int compressionLevel = 3, bool leaveOpen = false)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable for compression", nameof(stream));

            _baseStream = stream;
            _mode = CompressionMode.Compress;
            _leaveOpen = leaveOpen;
            _position = 0;
            _disposed = false;
            _wroteData = false;

            int bufferSize = 128 * 1024; // 128 KB buffer
            _encoder = new ZStdSeekableEncoder_v1_5_7(bufferSize, maxFrameSize, compressionLevel, 1);
            _buffer = new CompressionBuffer(bufferSize);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableStream_v1_5_7"/> class for decompression.
        /// </summary>
        /// <param name="stream">The base stream to read compressed data from.</param>
        /// <param name="leaveOpen">True to leave the base stream open after disposing this stream.</param>
        public ZStdSeekableStream_v1_5_7(Stream stream, bool leaveOpen = false)
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

            _decoder = new ZStdSeekableDecoder_v1_5_7(stream);
            int bufferSize = 128 * 1024;
            _buffer = new CompressionBuffer(bufferSize);
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position.
        /// </summary>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableStream_v1_5_7));
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

            if (count == 0 || _position >= (long)_decoder.DecompressedSize)
                return 0;

            // Clamp read to remaining data
            long remaining = (long)_decoder.DecompressedSize - _position;
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
                throw new ObjectDisposedException(nameof(ZStdSeekableStream_v1_5_7));
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
            CompressionBuffer outBuffer = new CompressionBuffer(_encoder.OutputBufferSize);
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
                throw new ObjectDisposedException(nameof(ZStdSeekableStream_v1_5_7));

            if (_mode == CompressionMode.Compress && _encoder != null)
            {
                // Finalize compression and write seek table
                CompressionBuffer inBuffer = new CompressionBuffer(0); // Empty input
                CompressionBuffer outBuffer = new CompressionBuffer(_encoder.OutputBufferSize);

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
                throw new ObjectDisposedException(nameof(ZStdSeekableStream_v1_5_7));
            if (_mode != CompressionMode.Decompress)
                throw new NotSupportedException("Seek is only supported in decompression mode");

            long newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => (long)_decoder.DecompressedSize + offset,
                _ => throw new ArgumentException("Invalid seek origin", nameof(origin))
            };

            if (newPosition < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Seek position is before the beginning of the stream");
            if (newPosition > (long)_decoder.DecompressedSize)
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

        /// <summary>
        /// Releases the unmanaged resources used by the stream and optionally releases the managed resources.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Finalize compression if in write mode
                    if (_mode == CompressionMode.Compress && _encoder != null && _wroteData)
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



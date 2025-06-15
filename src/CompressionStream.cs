using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

#if !CLASSIC && (NET40_OR_GREATER || NETSTANDARD || NETCOREAPP)
using System.Threading.Tasks;
using System.Xml.Linq;
using static Nanook.GrindCore.Interop;
#endif

namespace Nanook.GrindCore
{
    /// <summary>
    /// Provides a base stream for compression and decompression operations.
    /// </summary>
    public abstract class CompressionStream : Stream
    {
        private bool _disposed;
        private bool _complete;
        /// <summary>
        /// Gets a value indicating whether the base stream should be left open after the compression stream is disposed.
        /// </summary>
        protected readonly bool LeaveOpen;
        /// <summary>
        /// Gets a value indicating whether this stream is in compression mode.
        /// </summary>
        protected readonly bool IsCompress;
        /// <summary>
        /// Gets the compression type for this stream.
        /// </summary>
        protected readonly CompressionType CompressionType;

        /// <summary>
        /// Gets the underlying base stream.
        /// </summary>
        protected Stream BaseStream { get; }

        public byte[] InternalBuffer => _buffer.Data;

        /// <summary>
        /// Gets the threshold for the internal buffer.
        /// </summary>
        protected readonly int BufferThreshold;
        private CompressionBuffer _buffer;

        protected void RewindRead(int length)
        {
            _buffer.RewindRead(length);
        }

        /// <summary>
        /// Gets the compression defaults for this stream.
        /// </summary>
        internal virtual CompressionDefaults Defaults { get; }

        /// <inheritdoc/>
        public override bool CanSeek => false;
        /// <inheritdoc/>
        public override bool CanRead => BaseStream != null && !IsCompress && BaseStream.CanRead;
        /// <inheritdoc/>
        public override bool CanWrite => BaseStream != null && IsCompress && BaseStream.CanWrite;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException("Seeking is not supported.");

        private long _position;
        private long _positionReadBase; //real amount read from stream - used for limiting
        private long _positionFullSize; //count of bytes read/written to decompressed byte arrays

        /// <summary>
        /// Gets the total number of bytes read or written to decompressed byte arrays. The Decompressed/FullSize position, Position holds the Compressed position.
        /// </summary>
        public long PositionFullSize => _positionFullSize;

        protected long? PositionLimit { get; }

        protected long? PositionFullSizeLimit { get; }

        protected long BaseLength => BaseStream.Length;

        protected int BaseRead(CompressionBuffer inData, int size)
        {
            inData.Tidy();
            int limited = (int)Math.Min(size, (PositionLimit ?? long.MaxValue) - _positionReadBase);
            int read = BaseStream.Read(inData.Data, inData.Size, limited);
            inData.Write(read);
            _positionReadBase += read;
            return read;
        }

        protected int BaseWrite(CompressionBuffer outData, int length)
        {
            int limited = (int)Math.Min(length, (PositionLimit ?? long.MaxValue) - _position);
            BaseStream.Write(outData.Data, outData.Pos, limited);
            outData.Read(limited);
            _position += limited;
            return limited;
        }

        /// <summary>
        /// Gets or sets the compression properties for this stream.
        /// </summary>
        public byte[] Properties { get; protected set; }

        /// <inheritdoc/>
        public override long Position
        {
            get => _position != -1 ? _position : throw new NotSupportedException("Seeking is not supported.");
            set => throw new NotSupportedException("Position is readonly. Seeking is not supported.");
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionStream"/> class.
        /// </summary>
        /// <param name="positionSupport">Indicates if position support is enabled.</param>
        /// <param name="stream">The base stream to wrap.</param>
        /// <param name="defaultAlgorithm">The default algorithm, used when options.Version is not set to override it.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the stream does not support required operations or if compression type is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the buffer size is not positive.</exception>
        protected CompressionStream(bool positionSupport, Stream stream, CompressionAlgorithm defaultAlgorithm, CompressionOptions options)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));
            if (options is null)
                throw new ArgumentNullException(nameof(options));

            this.Defaults = new CompressionDefaults(defaultAlgorithm, options.Version);

            _complete = false;
            _position = positionSupport ? 0 : -1;
            CompressionType = options.Type;

            IsCompress = CompressionType != CompressionType.Decompress;

            LeaveOpen = options.LeaveOpen;
            BaseStream = stream;
            Version = options.Version ?? this.Defaults.Version; // latest

            PositionLimit = options.PositionLimit;
            PositionFullSizeLimit = options.PositionFullSizeLimit;

            BufferThreshold = options.BufferSize ?? this.BufferSizeInput;
            if (BufferThreshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(options.BufferSize), "BufferSize must be positive.");
            _buffer = new CompressionBuffer(options.BufferSize ?? this.BufferSizeInput);

            if (!IsCompress) //Decompress
            {
                if (!stream.CanRead)
                    throw new ArgumentException(SR.Stream_FalseCanRead, nameof(BaseStream));
            }
            else //Process
            {
                if (CompressionType == CompressionType.Optimal)
                    CompressionType = this.Defaults.LevelOptimal;
                else if (CompressionType == CompressionType.SmallestSize)
                    CompressionType = this.Defaults.LevelSmallestSize;
                else if (CompressionType == CompressionType.Fastest)
                    CompressionType = this.Defaults.LevelFastest;

                if (CompressionType < 0 || CompressionType > this.Defaults.LevelSmallestSize)
                    throw new ArgumentException("Invalid Option, CompressionType / Level");

                if (!stream.CanWrite)
                    throw new ArgumentException(SR.Stream_FalseCanWrite, nameof(BaseStream));
            }
        }

        /// <summary>
        /// Gets the compression version used by this stream.
        /// </summary>
        internal CompressionVersion Version { get; }
        /// <summary>
        /// Gets the input buffer size for this stream.
        /// </summary>
        internal abstract int BufferSizeInput { get; }
        /// <summary>
        /// Gets the output buffer size for this stream.
        /// </summary>
        internal abstract int BufferSizeOutput { get; }
        /// <summary>
        /// Reads data from the underlying stream into the provided buffer.
        /// </summary>
        internal abstract int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream, int length = 0);
        /// <summary>
        /// Writes data from the provided buffer to the underlying stream.
        /// </summary>
        internal abstract void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream);
        /// <summary>
        /// Flushes the compression buffers and finalizes stream writes and positions.
        /// </summary>
        internal abstract void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete);
        /// <summary>
        /// Performs custom cleanup for managed resources.
        /// </summary>
        protected abstract void OnDispose();

        private int onRead(DataBlock dataBlock, CancellableTask cancel)
        {
            int total = 0;
            int read = -1;
            while (read != 0 && total != dataBlock.Length)
            {
                read = (int)Math.Min(Math.Min(_buffer.AvailableRead, dataBlock.Length - total), (PositionFullSizeLimit ?? long.MaxValue) - _positionFullSize);
                if (read != 0)
                {
                    dataBlock.Write(total, _buffer, read);
                    _positionFullSize += read;
                    total += read;
                }
                if (total < dataBlock.Length)
                {
                    read = OnRead(_buffer, cancel, out var bytesReadFromStream, dataBlock.Length);

                    _position += bytesReadFromStream;
                }
            }
            return total;
        }

        private void onWrite(DataBlock dataBlock, CancellableTask cancel)
        {
            int total = 0;
            int size = 0;
            while (total != dataBlock.Length)
            {
                size = Math.Min(dataBlock.Length - total, (int)Math.Min(_buffer.AvailableWrite, (PositionFullSizeLimit ?? long.MaxValue) - _positionFullSize));
                if (size != 0)
                {
                    dataBlock.Read(total, _buffer, size);
                    total += size;
                }
                onWrite(cancel);
            }
        }

        private void onWrite(CancellableTask cancel)
        {
            if (_buffer.AvailableRead >= BufferThreshold || _buffer.AvailableWrite == 0)
            {
                int size2 = _buffer.AvailableRead;
                OnWrite(_buffer, cancel, out int _);
                _positionFullSize += size2 - _buffer.AvailableRead;
            }
        }

        /// <summary>
        /// Flushes compression buffers and finalizes stream writes and positions. 
        /// If not called from <see cref="Flush"/>, then called from <see cref="onDispose"/>.
        /// Best practice is to call flush if the object positions are to be read as the object may be garbage collected.
        /// </summary>
        /// <param name="cancel">A cancellation task.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        private void onFlush(CancellableTask cancel, bool flush, bool complete)
        {
            if (!_complete)
            {
                _complete = complete;
                if (IsCompress)
                {
                    int size = _buffer.AvailableRead;
                    OnFlush(_buffer, cancel, out int _, flush, complete);
                    _positionFullSize += size;
                    BaseStream.Flush();
                }
            }
        }

        /// <summary>
        /// Only called once from Dispose(), will flush if onFlush was not already called.
        /// </summary>
        private void onDispose()
        {
            onFlush(new CancellableTask(), false, true);
            OnDispose();
        }

        /// <inheritdoc/>
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public override int ReadByte()
        {
            if ((PositionFullSizeLimit ?? long.MaxValue) - _positionFullSize == 0)
            {
                return -1;
            }
            if (_buffer.AvailableRead == 0)
            {
                int read = OnRead(_buffer, new CancellableTask(), out int bytesReadFromStream);
                _positionFullSize += read;
                _position += bytesReadFromStream;
            }
            if (_buffer.AvailableRead == 0)
                return -1;

            int result = _buffer.Data[_buffer.Pos];
            _buffer.Read(1);
            return result;
        }

        /// <inheritdoc/>
        public override void WriteByte(byte value)
        {
            _buffer.Data[_buffer.Size] = value;
            _buffer.Write(1);
            if (_buffer.AvailableRead >= this.BufferSizeOutput)
                onWrite(new CancellableTask());
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">The buffer to read the data into.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the stream.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length.</exception>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
            if (buffer.Length - offset < count)
                throw new ArgumentException("The sum of offset and count is greater than the buffer length.");

            return onRead(new DataBlock(buffer, offset, count), new CancellableTask());
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">The buffer containing data to write to the stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the stream.</param>
        /// <param name="count">The number of bytes to be written to the stream.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length.</exception>
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
            if (buffer.Length - offset < count)
                throw new ArgumentException("The sum of offset and count is greater than the buffer length.");

            onWrite(new DataBlock(buffer, offset, count), new CancellableTask());
        }

        /// <summary>
        /// Clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        public override void Flush()
        {
            onFlush(new CancellableTask(), true, false);
        }

        /// <summary>
        /// Completes the compression or decompression operation, flushing all buffers and finalizing the stream without disposing anything.
        /// </summary>
        public virtual void Complete()
        {
            onFlush(new CancellableTask(), false, true);
        }

        /// <summary>
        /// Closes the current stream and releases any resources associated with the current stream.
        /// </summary>
        public override void Close() => Dispose(true);

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="CompressionStream"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    onDispose(); // Custom cleanup for managed resources

                if (!LeaveOpen)
                    try { BaseStream.Dispose(); } catch { }

                _disposed = true;
            }
            base.Dispose(disposing);
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">The region of memory to write the data into.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous read operation. The value of the result parameter contains the total number of bytes read into the buffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="buffer"/> length is negative.</exception>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length < 0)
                throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer length must be non-negative.");

            if (SynchronizationContext.Current == null)
            {
                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length); // Use DataBlock for internal logic
                return onRead(dataBlock, new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
            }

            return await Task.Run(() =>
            {
                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length); // Use DataBlock for internal logic
                return onRead(dataBlock, new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">The region of memory to write data from.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="buffer"/> length is negative.</exception>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length < 0)
                throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer length must be non-negative.");

            if (SynchronizationContext.Current == null)
            {
                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length); // Use DataBlock for internal logic
                onWrite(dataBlock, new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
                return;
            }

            await Task.Run(() =>
            {
                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length); // Use DataBlock for internal logic
                onWrite(dataBlock, new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously completes the compression or decompression operation, flushing all buffers and finalizing the stream without disposing anything.
        /// </summary>
        public virtual async ValueTask CompleteAsync()
        {
            await this.CompleteAsync(new CancellationToken());
        }

        /// <summary>
        /// Asynchronously releases the unmanaged resources used by the <see cref="CompressionStream"/> and optionally releases the managed resources.
        /// </summary>
        public override async ValueTask DisposeAsync()
        {
            if (SynchronizationContext.Current == null)
            {
                Dispose(true);
                return;
            }
            await Task.Run(() =>
            {
                Dispose(true);
            }).ConfigureAwait(false);
        }
#endif
#if CLASSIC || NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
        /// <summary>
        /// Asynchronously reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <param name="buffer">The buffer to read the data into.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the stream.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous read operation. The value of the result parameter contains the total number of bytes read into the buffer.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length.</exception>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
            if (buffer.Length - offset < count)
                throw new ArgumentException("The sum of offset and count is greater than the buffer length.");

            if (SynchronizationContext.Current == null)
            {
                DataBlock dataBlock = new DataBlock(buffer, offset, count); // Use DataBlock for internal logic
                return onRead(dataBlock, new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
            }

            return await Task.Run(() =>
            {
                DataBlock dataBlock = new DataBlock(buffer, offset, count); // Use DataBlock for internal logic
                return onRead(dataBlock, new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">The buffer containing data to write to the stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the stream.</param>
        /// <param name="count">The number of bytes to be written to the stream.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous write operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="count"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of <paramref name="offset"/> and <paramref name="count"/> is greater than the buffer length.</exception>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
            if (buffer.Length - offset < count)
                throw new ArgumentException("The sum of offset and count is greater than the buffer length.");

            if (SynchronizationContext.Current == null)
            {
                DataBlock dataBlock = new DataBlock(buffer, offset, count); // Use DataBlock for internal logic
                onWrite(dataBlock, new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
                return;
            }

            await Task.Run(() =>
            {
                DataBlock dataBlock = new DataBlock(buffer, offset, count); // Use DataBlock for internal logic
                onWrite(dataBlock, new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously clears all buffers for this stream and causes any buffered data to be written to the underlying device.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous flush operation.</returns>
        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (SynchronizationContext.Current == null)
            {
                onFlush(new CancellableTask(cancellationToken), true, false); // Wrap the token to simplify frameworks that don't support it
                return;
            }

            await Task.Run(() =>
            {
                onFlush(new CancellableTask(cancellationToken), true, false);
            }, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Asynchronously completes the compression or decompression operation, flushing all buffers and finalizing the stream without disposing anything.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous complete operation.</returns>
        public virtual async Task CompleteAsync(CancellationToken cancellationToken)
        {
            if (SynchronizationContext.Current == null)
            {
                onFlush(new CancellableTask(cancellationToken), false, true); // Wrap the token to simplify frameworks that don't support it
                return;
            }

            await Task.Run(() =>
            {
                onFlush(new CancellableTask(cancellationToken), false, true);
            }, cancellationToken).ConfigureAwait(false);
        }
#endif
    }
}
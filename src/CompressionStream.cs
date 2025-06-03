using System;
using System.Drawing;
using System.IO;
using System.Threading;



#if !CLASSIC && (NET40_OR_GREATER || NETSTANDARD || NETCOREAPP)
using System.Threading.Tasks;
using static Nanook.GrindCore.Interop;
#endif

namespace Nanook.GrindCore
{
    public abstract class CompressionStream : Stream
    {
        private bool _disposed;
        private bool _complete;
        protected readonly bool LeaveOpen;
        protected readonly bool IsCompress;
        protected readonly CompressionType CompressionType;

        public Stream BaseStream { get; }

        protected readonly int CacheThreshold;
        private CompressionBuffer _cache;

        internal virtual CompressionDefaults Defaults { get; }

        public override bool CanSeek => false;
        public override bool CanRead => BaseStream != null && !IsCompress && BaseStream.CanRead;
        public override bool CanWrite => BaseStream != null && IsCompress && BaseStream.CanWrite;

        public override long Length => throw new NotSupportedException("Seeking is not supported.");

        private long _position;
        private long _positionFullSize; //count of bytes read/written to decompressed byte arrays

        public long PositionFullSize => _positionFullSize;

        public byte[] Properties { get; protected set; }

        public override long Position
        {
            get => _position != -1 ? _position : throw new NotSupportedException("Seeking is not supported.");
            set => throw new NotSupportedException("Position is readonly. Seeking is not supported.");
        }

        protected CompressionStream(bool positionSupport, Stream stream, CompressionOptions options)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            this.Defaults = new CompressionDefaults(this.Algorithm, options.Version);

            _complete = false;
            _position = positionSupport ? 0 : -1;
            CompressionType = options.Type;

            IsCompress = CompressionType != CompressionType.Decompress;

            LeaveOpen = options.LeaveOpen;
            BaseStream = stream;
            Version = options.Version ?? this.Defaults.Version; // latest

            CacheThreshold = options.BufferSize ?? this.BufferSizeInput;
            _cache = new CompressionBuffer(options.BufferSize ?? this.BufferSizeInput);

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

        internal abstract CompressionAlgorithm Algorithm { get; }
        internal CompressionVersion Version { get; }
        internal abstract int BufferSizeInput { get; }
        internal abstract int BufferSizeOutput { get; }
        internal abstract int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream);
        internal abstract void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream);
        internal abstract void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete);
        protected abstract void OnDispose();

        private int onRead(DataBlock dataBlock, CancellableTask cancel)
        {
            int total = 0;
            int read = -1;

            while (read != 0 && total != dataBlock.Length)
            {
                read = Math.Min(_cache.AvailableRead, dataBlock.Length - total);
                if (read != 0)
                {
                    dataBlock.Write(total, _cache, read);
                    total += read;
                }

                if (total < dataBlock.Length)
                {
                    read = OnRead(_cache, cancel, out int bytesReadFromStream);
                    _positionFullSize += read;
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
                size = Math.Min(dataBlock.Length - total, _cache.AvailableWrite);
                if (size != 0)
                {
                    dataBlock.Read(total, _cache, size); //read from datablock / write to _cache
                    total += size;
                }

                onWrite(cancel);
            }
        }

        private void onWrite(CancellableTask cancel)
        {
            int size2;
            if (_cache.AvailableRead >= CacheThreshold || _cache.AvailableWrite == 0) // safeguarded, should never be if (false || true)
            {
                size2 = _cache.AvailableRead;
                OnWrite(_cache, cancel, out int bytesWrittenToStream);
                _positionFullSize += size2 - _cache.AvailableRead;
                _position += bytesWrittenToStream;
            }
        }

        /// <summary>
        /// Flush compression buffers and finalise stream writes and positions. If not called from Flush() then onDispose().
        /// Best practice is to call flush if the object Positions are to be read as the object be be Garbage Collected.
        /// </summary>
        /// <param name="cancel"></param>
        private void onFlush(CancellableTask cancel, bool flush, bool complete)
        {
            if (!_complete)
            {
                _complete = complete; //set straight away in case OnFlush causes a recall on the same thread

                if (IsCompress)
                {
                    int size = _cache.AvailableRead;
                    OnFlush(_cache, cancel, out int bytesWrittenToStream, flush, complete);
                    _position += bytesWrittenToStream;
                    _positionFullSize += size;
                    BaseStream.Flush();
                }
            }
        }

        /// <summary>
        /// Only called once from Dispose(), will flush if onFlush was not already called
        /// </summary>
        private void onDispose()
        {
            onFlush(new CancellableTask(), false, true);
            OnDispose();
        }

        // Abstract method for Seek, since it's required by Stream
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int ReadByte()
        {
            if (_cache.AvailableRead == 0)
            {
                int read = OnRead(_cache, new CancellableTask(), out int bytesReadFromStream);
                _positionFullSize += read;
                _position += bytesReadFromStream;
            }
            if (_cache.AvailableRead == 0)
                return -1;

            int result = _cache.Data[_cache.Pos];
            _cache.Read(1);
            return result;
        }

        public override void WriteByte(byte value)
        {
            _cache.Data[_cache.Size] = value;
            _cache.Write(1);
            if (_cache.AvailableRead >= this.BufferSizeOutput)
                onWrite(new CancellableTask());
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return onRead(new DataBlock(buffer, offset, count), new CancellableTask());
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            onWrite(new DataBlock(buffer, offset, count), new CancellableTask());
        }

        public override void Flush()
        {
            onFlush(new CancellableTask(), true, false);
        }

        public virtual void Complete()
        {
            onFlush(new CancellableTask(), false, true);
        }

        /// <summary>
        /// Close streaming progress
        /// </summary>
        public override void Close() => Dispose(true);

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
        /// Reads data asynchronously from the stream using a Memory<byte>. Converts to DataBlock internally.
        /// </summary>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
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
        /// Writes data asynchronously to the stream using a ReadOnlyMemory<byte>. Converts to DataBlock internally.
        /// </summary>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
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

        public virtual async ValueTask CompleteAsync()
        {
            await this.CompleteAsync(new CancellationToken());
        }

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
        /// Reads data asynchronously from the stream using byte[]. Converts to DataBlock internally.
        /// </summary>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
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
        /// Writes data asynchronously to the stream using byte[]. Converts to DataBlock internally.
        /// </summary>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
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
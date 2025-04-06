using System;
using System.IO;
using System.Threading;


#if !CLASSIC && (NET40_OR_GREATER || NETSTANDARD || NETCOREAPP)
using System.Threading.Tasks;
using static Nanook.GrindCore.Interop;
#endif

// Add Generic Options class (cater for lzma properties)
// Add generic buffer pool for buffers
// Add Read Write buffers to support ReadByte, WriteByte

namespace Nanook.GrindCore
{
    public abstract class CompressionStream : Stream
    {
        private bool _disposed;
        protected readonly Stream _baseStream;
        protected readonly bool _leaveOpen;
        protected readonly bool _compress;
        protected readonly CompressionType _type;
        protected CompressionVersion? _version;

        public Stream BaseStream => _baseStream;

        public override bool CanSeek => false;
        public override bool CanRead => _baseStream != null && !_compress && _baseStream.CanRead;
        public override bool CanWrite => _baseStream != null && _compress && _baseStream.CanWrite;

        public override long Length => throw new NotSupportedException("Seeking is not supported.");

        private int _limit;
        private long _position;
        private long _positionFullSize; //count of bytes read/written to decompressed byte arrays

        public long PositionFullSize => _positionFullSize;

        public void SetNextReadLimit(int length)
        {
            _limit = length;
        }

        public override long Position
        {
            get => _position != -1 ? _position : throw new NotSupportedException("Seeking is not supported.");
            set => throw new NotSupportedException("Position is readonly. Seeking is not supported.");
        }

        protected CompressionStream(bool positionSupport, Stream stream, bool leaveOpen, CompressionType type, CompressionVersion? version = null)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            _position = positionSupport ? 0 : -1;
            _compress = type != CompressionType.Decompress;

            _leaveOpen = leaveOpen;
            _baseStream = stream;
            _version = version;

            if (!_compress) //Decompress
            {
                if (!stream.CanRead)
                    throw new ArgumentException(SR.Stream_FalseCanRead, nameof(_baseStream));
            }
            else //Compress
            {
                if (type == CompressionType.Optimal)
                    _type = ((ICompressionDefaults)this).LevelOptimal;
                else if (type == CompressionType.SmallestSize)
                    _type = ((ICompressionDefaults)this).LevelSmallestSize;
                else if (type == CompressionType.Fastest)
                    _type = ((ICompressionDefaults)this).LevelFastest;
                else
                    _type = type;

                if (_type < 0 || _type > ((ICompressionDefaults)this).LevelSmallestSize)
                    throw new ArgumentException("Invalid Compression Type / Level", nameof(type));

                if (!stream.CanWrite)
                    throw new ArgumentException(SR.Stream_FalseCanWrite, nameof(_baseStream));
            }
        }

        internal abstract int OnRead(DataBlock dataBlock, CancellableTask cancel, int limit, out int bytesReadFromStream);
        internal abstract void OnWrite(DataBlock dataBlock, CancellableTask cancel, out int bytesWrittenToStream);
        internal abstract void OnFlush(CancellableTask cancel, out int bytesWrittenToStream);
        protected abstract void OnDispose(out int bytesWrittenToStream);

        private int onRead(DataBlock dataBlock, CancellableTask cancel)
        {
            int res = OnRead(dataBlock, cancel, _limit, out int bytesReadFromStream);
            _limit = 0;
            _positionFullSize += res;
            _position += bytesReadFromStream;
            return res;
        }
        private void onWrite(DataBlock dataBlock, CancellableTask cancel)
        {
            OnWrite(dataBlock, cancel, out int bytesWrittenToStream);
            _positionFullSize += dataBlock.Length;
            _position += bytesWrittenToStream;
        }
        private void onFlush(CancellableTask cancel)
        {
            OnFlush(cancel, out int bytesWrittenToStream);
            _position += bytesWrittenToStream;
        }
        private void onDispose()
        {
            OnDispose(out int bytesWrittenToStream);
            _position += bytesWrittenToStream;
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
            onFlush(new CancellableTask());
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

                if (!_leaveOpen)
                    try { _baseStream.Dispose(); } catch { }

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
                onFlush(new CancellableTask(cancellationToken)); // Wrap the token to simplify frameworks that don't support it
                return;
            }

            await Task.Run(() =>
            {
                onFlush(new CancellableTask(cancellationToken));
            }, cancellationToken).ConfigureAwait(false);
        }
#endif
    }
}
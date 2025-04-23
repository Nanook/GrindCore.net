using System;
using System.IO;
using System.Threading;


#if !CLASSIC && (NET40_OR_GREATER || NETSTANDARD || NETCOREAPP)
using System.Threading.Tasks;
using static Nanook.GrindCore.Interop;
#endif

// Add Read Write buffers to support ReadByte, WriteByte
// Add a min buffer threshold. - use onRead and onWrite to use CompressionBuffer to manage minimum size buffers (support readbyte, writebyte etc)

namespace Nanook.GrindCore
{
    public abstract class CompressionStream : Stream
    {
        private bool _disposed;
        protected readonly bool LeaveOpen;
        protected readonly bool IsCompress;
        protected readonly CompressionType CompressionType;

        public Stream BaseStream { get; }

        protected readonly int _cacheThreshold;
        private CompressionBuffer _cache;

        public override bool CanSeek => false;
        public override bool CanRead => BaseStream != null && !IsCompress && BaseStream.CanRead;
        public override bool CanWrite => BaseStream != null && IsCompress && BaseStream.CanWrite;

        public override long Length => throw new NotSupportedException("Seeking is not supported.");

        private long _position;
        private long _positionFullSize; //count of bytes read/written to decompressed byte arrays

        public long PositionFullSize => _positionFullSize;

        public byte[] Properties { get; protected set; }

        public void ResetForBlockRead(int blockLength)
        {
            if (!this.CanRead)
                throw new InvalidOperationException("Stream must be in read mode");

            //TODO: Reset the decoder
            //_blockSize = blockLength;
        }

        public override long Position
        {
            get => _position != -1 ? _position : throw new NotSupportedException("Seeking is not supported.");
            set => throw new NotSupportedException("Position is readonly. Seeking is not supported.");
        }

        protected CompressionStream(bool positionSupport, Stream stream, CompressionOptions options)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            _position = positionSupport ? 0 : -1;
            CompressionType = options.Type;

            IsCompress = CompressionType != CompressionType.Decompress;

            LeaveOpen = options.LeaveOpen;
            BaseStream = stream;
            Version = options.Version ?? CompressionVersion.Create(this.Algorithm, ""); // latest

            _cacheThreshold = options.ProcessSizeMin ?? 0x1000;
            _cache = new CompressionBuffer(options.ProcessSizeMax ?? 0x200000);

            if (!IsCompress) //Decompress
            {
                if (!stream.CanRead)
                    throw new ArgumentException(SR.Stream_FalseCanRead, nameof(BaseStream));
            }
            else //Process
            {
                if (CompressionType == CompressionType.Optimal)
                    CompressionType = ((ICompressionDefaults)this).LevelOptimal;
                else if (CompressionType == CompressionType.SmallestSize)
                    CompressionType = ((ICompressionDefaults)this).LevelSmallestSize;
                else if (CompressionType == CompressionType.Fastest)
                    CompressionType = ((ICompressionDefaults)this).LevelFastest;

                if (CompressionType < 0 || CompressionType > ((ICompressionDefaults)this).LevelSmallestSize)
                    throw new ArgumentException("Invalid Option, CompressionType / Level");

                if (!stream.CanWrite)
                    throw new ArgumentException(SR.Stream_FalseCanWrite, nameof(BaseStream));
            }
        }

        internal abstract CompressionAlgorithm Algorithm { get; }
        internal CompressionVersion Version { get; }
        internal abstract int DefaultProcessSizeMin { get; }
        internal abstract int DefaultProcessSizeMax { get; }
        internal abstract int OnRead(CompressionBuffer data, CancellableTask cancel, int limit, out int bytesReadFromStream);
        internal abstract void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream);
        internal abstract void OnFlush(CancellableTask cancel, out int bytesWrittenToStream);
        protected abstract void OnDispose(out int bytesWrittenToStream);

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
                    read = OnRead(_cache, cancel, 0, out int bytesReadFromStream);
                    //read = OnRead(new DataBlock(_cache.Data, _cache.Size, _cache.AvailableWrite), cancel, 0, out int bytesReadFromStream);
                    //_cache.Write(read);
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

                if (_cache.AvailableRead >= _cacheThreshold || _cache.AvailableWrite == 0) // safeguarded, should never be if (false || true)
                {
                    size = _cache.AvailableRead;
                    OnWrite(_cache, cancel, out int bytesWrittenToStream);
                    //OnWrite(new DataBlock(_cache.Data, _cache.Pos, _cache.AvailableRead), cancel, out int bytesWrittenToStream);
                    //_cache.Read(size);
                    _positionFullSize += size;
                    _position += bytesWrittenToStream;
                }
            }
        }
        private void onFlush(CancellableTask cancel)
        {
            OnFlush(cancel, out int bytesWrittenToStream);
            _position += bytesWrittenToStream;
            BaseStream.Flush();
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
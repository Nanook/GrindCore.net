//using System;
//using System.IO;
//using System.Threading;

//#if !CLASSIC && (NET40_OR_GREATER || NETSTANDARD || NETCOREAPP)
//using System.Threading.Tasks;
//using static Nanook.GrindCore.Interop;
//#endif

//namespace Nanook.GrindCore
//{
//    public abstract class CompressionStream : Stream
//    {
//        private bool _disposed;
//        protected readonly Stream _baseStream;
//        protected readonly bool _leaveOpen;
//        protected readonly bool _compress;
//        protected readonly CompressionType _type;
//        protected CompressionVersion? _version;

//        private const int BufferSize = 4 * 1024; // 4KiB buffer size
//        private readonly byte[] _readBuffer = BufferPool.Rent(BufferSize);
//        private readonly byte[] _writeBuffer = BufferPool.Rent(BufferSize);
//        private int _readBufferPosition = 0;
//        private int _readBufferCount = 0;
//        private int _writeBufferPosition = 0;

//        public Stream BaseStream => _baseStream;

//        public override bool CanSeek => false;
//        public override bool CanRead => _baseStream != null && !_compress && _baseStream.CanRead;
//        public override bool CanWrite => _baseStream != null && _compress && _baseStream.CanWrite;

//        public override long Length => throw new NotSupportedException("Seeking is not supported.");

//        private long _position;

//        public override long Position
//        {
//            get => _position != -1 ? _position : throw new NotSupportedException("Seeking is not supported.");
//            set => throw new NotSupportedException("Position is readonly. Seeking is not supported.");
//        }

//        // Abstract method for Seek, since it's required by Stream
//        public override long Seek(long offset, SeekOrigin origin)
//        {
//            throw new NotImplementedException();
//        }

//        public override void SetLength(long value)
//        {
//            throw new NotImplementedException();
//        }

//        protected void AddPosition(long bytes)
//        {
//            _position += bytes;
//        }

//        protected CompressionStream(bool positionSupport, Stream stream, bool leaveOpen, CompressionType type, CompressionVersion? version = null)
//        {
//            if (stream == null)
//                throw new ArgumentNullException(nameof(stream));

//            _position = positionSupport ? 0 : -1;
//            _compress = type != CompressionType.Decompress;

//            _leaveOpen = leaveOpen;
//            _baseStream = stream;
//            _version = version;

//            if (!_compress) //Decompress
//            {
//                if (!stream.CanRead)
//                    throw new ArgumentException(SR.Stream_FalseCanRead, nameof(_baseStream));
//            }
//            else //Compress
//            {
//                if (type == CompressionType.Optimal)
//                    _type = ((ICompressionDefaults)this).LevelOptimal;
//                else if (type == CompressionType.SmallestSize)
//                    _type = ((ICompressionDefaults)this).LevelSmallestSize;
//                else if (type == CompressionType.Fastest)
//                    _type = ((ICompressionDefaults)this).LevelFastest;
//                else
//                    _type = type;

//                if (_type < 0 || _type > ((ICompressionDefaults)this).LevelSmallestSize)
//                    throw new ArgumentException("Invalid Compression Type / Level", nameof(type));

//                if (!stream.CanWrite)
//                    throw new ArgumentException(SR.Stream_FalseCanWrite, nameof(_baseStream));
//            }
//        }

//        public override int ReadByte()
//        {
//            if (_readBufferPosition >= _readBufferCount)
//            {
//                _readBufferCount = OnRead(new DataBlock(_readBuffer, 0, BufferSize), new CancellableTask());
//                _readBufferPosition = 0;

//                if (_readBufferCount == 0)
//                    return -1; // End of stream
//            }

//            return _readBuffer[_readBufferPosition++];
//        }

//        public override void WriteByte(byte value)
//        {
//            _writeBuffer[_writeBufferPosition++] = value;

//            if (_writeBufferPosition >= BufferSize)
//            {
//                OnWrite(new DataBlock(_writeBuffer, 0, _writeBufferPosition), new CancellableTask());
//                _writeBufferPosition = 0;
//            }
//        }

//        public override int Read(byte[] buffer, int offset, int count)
//        {
//            int totalBytesRead = 0;

//            while (count > 0)
//            {
//                if (_readBufferPosition >= _readBufferCount)
//                {
//                    _readBufferCount = OnRead(new DataBlock(_readBuffer, 0, BufferSize), new CancellableTask());
//                    _readBufferPosition = 0;

//                    if (_readBufferCount == 0)
//                        break; // End of stream
//                }

//                int bytesToCopy = Math.Min(count, _readBufferCount - _readBufferPosition);
//                Array.Copy(_readBuffer, _readBufferPosition, buffer, offset, bytesToCopy);

//                _readBufferPosition += bytesToCopy;
//                offset += bytesToCopy;
//                count -= bytesToCopy;
//                totalBytesRead += bytesToCopy;
//            }

//            return totalBytesRead;
//        }

//        public override void Write(byte[] buffer, int offset, int count)
//        {
//            while (count > 0)
//            {
//                int spaceInBuffer = BufferSize - _writeBufferPosition;
//                int bytesToCopy = Math.Min(count, spaceInBuffer);

//                Array.Copy(buffer, offset, _writeBuffer, _writeBufferPosition, bytesToCopy);

//                _writeBufferPosition += bytesToCopy;
//                offset += bytesToCopy;
//                count -= bytesToCopy;

//                if (_writeBufferPosition >= BufferSize)
//                {
//                    OnWrite(new DataBlock(_writeBuffer, 0, _writeBufferPosition), new CancellableTask());
//                    _writeBufferPosition = 0;
//                }
//            }
//        }

//        public override void Flush()
//        {
//            if (_writeBufferPosition > 0)
//            {
//                OnWrite(new DataBlock(_writeBuffer, 0, _writeBufferPosition), new CancellableTask());
//                _writeBufferPosition = 0;
//            }

//            OnFlush(new CancellableTask());
//        }

//        protected override void Dispose(bool disposing)
//        {
//            if (!_disposed)
//            {
//                if (disposing)
//                {
//                    Flush();
//                    BufferPool.Return(_readBuffer);
//                    BufferPool.Return(_writeBuffer);
//                    OnDispose();
//                }

//                if (!_leaveOpen)
//                {
//                    try { _baseStream.Dispose(); } catch { }
//                }

//                _disposed = true;
//            }
//            base.Dispose(disposing);
//        }

//#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
//        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
//        {
//            if (SynchronizationContext.Current == null)
//            {
//                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length);
//                return OnRead(dataBlock, new CancellableTask(cancellationToken));
//            }

//            return await Task.Run(() =>
//            {
//                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length);
//                return OnRead(dataBlock, new CancellableTask(cancellationToken));
//            }, cancellationToken).ConfigureAwait(false);
//        }

//        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
//        {
//            if (SynchronizationContext.Current == null)
//            {
//                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length);
//                OnWrite(dataBlock, new CancellableTask(cancellationToken));
//                return;
//            }

//            await Task.Run(() =>
//            {
//                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length);
//                OnWrite(dataBlock, new CancellableTask(cancellationToken));
//            }, cancellationToken).ConfigureAwait(false);
//        }
//#endif

//#if CLASSIC || NET45_OR_GREATER || NETSTANDARD || NETCOREAPP
//        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        {
//            if (SynchronizationContext.Current == null)
//            {
//                DataBlock dataBlock = new DataBlock(buffer, offset, count);
//                return OnRead(dataBlock, new CancellableTask(cancellationToken));
//            }

//            return await Task.Run(() =>
//            {
//                DataBlock dataBlock = new DataBlock(buffer, offset, count);
//                return OnRead(dataBlock, new CancellableTask(cancellationToken));
//            }, cancellationToken).ConfigureAwait(false);
//        }

//        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
//        {
//            if (SynchronizationContext.Current == null)
//            {
//                DataBlock dataBlock = new DataBlock(buffer, offset, count);
//                OnWrite(dataBlock, new CancellableTask(cancellationToken));
//                return;
//            }

//            await Task.Run(() =>
//            {
//                DataBlock dataBlock = new DataBlock(buffer, offset, count);
//                OnWrite(dataBlock, new CancellableTask(cancellationToken));
//            }, cancellationToken).ConfigureAwait(false);
//        }

//        public override async Task FlushAsync(CancellationToken cancellationToken)
//        {
//            if (SynchronizationContext.Current == null)
//            {
//                OnFlush(new CancellableTask(cancellationToken));
//                return;
//            }

//            await Task.Run(() =>
//            {
//                OnFlush(new CancellableTask(cancellationToken));
//            }, cancellationToken).ConfigureAwait(false);
//        }
//#endif

//        internal abstract int OnRead(DataBlock dataBlock, CancellableTask cancel);
//        internal abstract void OnWrite(DataBlock dataBlock, CancellableTask cancel);
//        internal abstract void OnFlush(CancellableTask cancel);
//        protected abstract void OnDispose();
//    }
//}
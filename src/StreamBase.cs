using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nanook.GrindCore
{
    public abstract class StreamBase : Stream
    {
        private bool _disposed;

        public override bool CanSeek => false;

        public override long Length => throw new NotSupportedException("Seeking is not supported.");

        private long _position;

        public override long Position
        {
            get => _position != -1 ? _position : throw new NotSupportedException("Seeking is not supported.");
            set => throw new NotSupportedException("Position is readonly. Seeking is not supported.");
        }

        protected StreamBase(bool positionSupport)
        {
            _position = positionSupport ? 0 : -1;
        }

        internal abstract int OnRead(DataBlock dataBlock);
        internal abstract void OnWrite(DataBlock dataBlock);
        internal abstract void OnFlush();

        // Abstract method for Seek, since it's required by Stream
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        protected void AddPosition(long bytes)
        {
            _position += bytes;
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            return OnRead(new DataBlock(buffer, offset, count));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            OnWrite(new DataBlock(buffer, offset, count));
        }

        public override void Flush()
        {
            OnFlush();
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Reads data asynchronously from the stream using a Memory<byte>. Converts to DataBlock internally.
        /// </summary>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int bytesRead = await Task.Run(() =>
            {
                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length); // Use DataBlock for internal logic
                return OnRead(dataBlock);
            }, cancellationToken).ConfigureAwait(false);

            return bytesRead;
        }

        /// <summary>
        /// Writes data asynchronously to the stream using a ReadOnlyMemory<byte>. Converts to DataBlock internally.
        /// </summary>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                DataBlock dataBlock = new DataBlock(buffer.Span, 0, buffer.Length); // Use DataBlock for internal logic
                OnWrite(dataBlock);
            }, cancellationToken).ConfigureAwait(false);
        }
#endif
#if CLASSIC || NET40_OR_GREATER || NETSTANDARD || NETCOREAPP
        /// <summary>
        /// Reads data asynchronously from the stream using byte[]. Converts to DataBlock internally.
        /// </summary>
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int bytesRead = await Task.Run(() =>
            {
                DataBlock dataBlock = new DataBlock(buffer, offset, count); // Use DataBlock for internal logic
                return OnRead(dataBlock);
            }, cancellationToken).ConfigureAwait(false);

            return bytesRead;
        }

        /// <summary>
        /// Writes data asynchronously to the stream using byte[]. Converts to DataBlock internally.
        /// </summary>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                DataBlock dataBlock = new DataBlock(buffer, offset, count); // Use DataBlock for internal logic
                OnWrite(dataBlock);
            }, cancellationToken).ConfigureAwait(false);
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                OnFlush();
            }, cancellationToken).ConfigureAwait(false);
        }
#endif

        /// <summary>
        /// Close streaming progress
        /// </summary>
        public override void Close() => Dispose(true);

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                    OnDispose(); // Custom cleanup for managed resources

                _disposed = true;
            }
            base.Dispose(disposing);
        }

        protected abstract void OnDispose();
    }
}
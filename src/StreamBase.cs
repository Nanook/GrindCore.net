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

        // Abstract methods for key functionality in derived classes
        public abstract override int Read(byte[] buffer, int offset, int count);
        public abstract override void Write(byte[] buffer, int offset, int count);

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



#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        // These are inefficient, each stream should override and provide proper support

        public override int Read(Span<byte> buffer)
        {
            // Use a temporary array as Span<> can be converted to array via slicing
            byte[] tempBuffer = new byte[buffer.Length];
            int bytesRead = Read(tempBuffer, 0, tempBuffer.Length); // Call the abstract Read method
            tempBuffer.AsSpan(0, bytesRead).CopyTo(buffer); // Copy into Span
            return bytesRead;
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            // Convert ReadOnlySpan<> to array for compatibility
            byte[] tempBuffer = buffer.ToArray();
            Write(tempBuffer, 0, tempBuffer.Length); // Call the abstract Write method
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Use the synchronous Read method as fallback for async
            byte[] tempBuffer = new byte[buffer.Length];
            int bytesRead = Read(tempBuffer, 0, tempBuffer.Length);
            tempBuffer.AsMemory(0, bytesRead).CopyTo(buffer);
            return new ValueTask<int>(bytesRead);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Use the synchronous Write method as fallback for async
            Write(buffer.ToArray(), 0, buffer.Length);
#if NET5_0_OR_GREATER
            return ValueTask.CompletedTask;
#else
            return new ValueTask(Task.CompletedTask);
#endif

        }
#endif

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0
            return Task.Run(() => Task.FromResult(Read(buffer, offset, count)), cancellationToken);
#else
            return base.ReadAsync(buffer, offset, count, cancellationToken);
#endif
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
#if NETSTANDARD2_0
            return Task.Run(() => { Write(buffer, offset, count); return Task.CompletedTask; }, cancellationToken);
#else
            return base.WriteAsync(buffer, offset, count, cancellationToken);
#endif
        }

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



using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System;
using Nanook.GrindCore.DeflateZLib;

/* Unmerged change from project 'GrindCore.net (netstandard2.1)'
Added:
using Nanook;
using Nanook.GrindCore;
using Nanook.GrindCore.GZip;
*/

namespace Nanook.GrindCore.GZip
{
    public class GZipStream : Stream
    {
        private DeflateStream _deflateStream;

        public GZipStream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, leaveOpen: false, version)
        {
        }

        public GZipStream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null)
        {
            _deflateStream = new DeflateStream(stream, type, leaveOpen, Interop.ZLib.GZip_DefaultWindowBits, version);
        }

        public override bool CanRead => _deflateStream?.CanRead ?? false;

        public override bool CanWrite => _deflateStream?.CanWrite ?? false;

        public override bool CanSeek => _deflateStream?.CanSeek ?? false;

        public override long Length
        {
            get { throw new NotSupportedException(SR.NotSupported); }
        }

        public override long Position
        {
            get { throw new NotSupportedException(SR.NotSupported); }
            set { throw new NotSupportedException(SR.NotSupported); }
        }

        public override void Flush()
        {
            CheckDeflateStream();
            _deflateStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException(SR.NotSupported);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException(SR.NotSupported);
        }

        public override int ReadByte()
        {
            CheckDeflateStream();
            return _deflateStream.ReadByte();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            TaskToAsyncResult.Begin(ReadAsync(buffer, offset, count, CancellationToken.None), asyncCallback!, asyncState!);

        public override int EndRead(IAsyncResult asyncResult) =>
            _deflateStream.EndRead(asyncResult);

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDeflateStream();
            return _deflateStream.Read(buffer, offset, count);
        }

#if NETFRAMEWORK
        public int Read(Span<byte> buffer)
#else
        public override int Read(Span<byte> buffer)
#endif
        {
            if (GetType() != typeof(GZipStream))
            {
                // GZipStream is not sealed, and a derived type may have overridden Read(byte[], int, int) prior
                // to this Read(Span<byte>) overload being introduced.  In that case, this Read(Span<byte>) overload
                // should use the behavior of Read(byte[],int,int) overload.
                return base.Read(buffer);
            }
            else
            {
                CheckDeflateStream();
                return _deflateStream.ReadCore(buffer);
            }
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            TaskToAsyncResult.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), asyncCallback!, asyncState!);

        public override void EndWrite(IAsyncResult asyncResult) =>
            _deflateStream.EndWrite(asyncResult);

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDeflateStream();
            _deflateStream.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            if (GetType() != typeof(GZipStream))
            {
                // GZipStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
                // to this WriteByte override being introduced.  In that case, this WriteByte override
                // should use the behavior of the Write(byte[],int,int) overload.
                base.WriteByte(value);
            }
            else
            {
                CheckDeflateStream();
#if NET7_0_OR_GREATER
                _deflateStream.WriteCore(new ReadOnlySpan<byte>(in value));
#else
                _deflateStream.WriteCore(new ReadOnlySpan<byte>(new byte[] { value }));
#endif
            }
        }

#if NETFRAMEWORK
        public void Write(ReadOnlySpan<byte> buffer)
#else
        public override void Write(ReadOnlySpan<byte> buffer)
#endif
        {
            if (GetType() != typeof(GZipStream))
            {
                // GZipStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
                // to this Write(ReadOnlySpan<byte>) overload being introduced.  In that case, this Write(ReadOnlySpan<byte>) overload
                // should use the behavior of Write(byte[],int,int) overload.
                base.Write(buffer);
            }
            else
            {
                CheckDeflateStream();
                _deflateStream.WriteCore(buffer);
            }
        }

#if NETFRAMEWORK
        public new void CopyTo(Stream destination, int bufferSize)
#else
        public override void CopyTo(Stream destination, int bufferSize)
#endif
        {
            CheckDeflateStream();
            _deflateStream.CopyTo(destination, bufferSize);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _deflateStream != null)
                {
                    _deflateStream.Dispose();
                }
                _deflateStream = null!;
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

#if NETCOREAPP
        public override ValueTask DisposeAsync()
        {
            if (GetType() != typeof(GZipStream))
            {
                return base.DisposeAsync();
            }

            DeflateStream? ds = _deflateStream;
            if (ds != null)
            {
                _deflateStream = null!;
                return ds.DisposeAsync();
            }

            return default;
        }
#endif

        public Stream BaseStream => _deflateStream?.BaseStream!;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckDeflateStream();
            return _deflateStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

#if NETFRAMEWORK
        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
#else
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
#endif
        {
            if (GetType() != typeof(GZipStream))
            {
                // GZipStream is not sealed, and a derived type may have overridden ReadAsync(byte[], int, int) prior
                // to this ReadAsync(Memory<byte>) overload being introduced.  In that case, this ReadAsync(Memory<byte>) overload
                // should use the behavior of ReadAsync(byte[],int,int) overload.
                return base.ReadAsync(buffer, cancellationToken);
            }
            else
            {
                CheckDeflateStream();
                return _deflateStream.ReadAsyncMemory(buffer, cancellationToken);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckDeflateStream();
            return _deflateStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

#if NETFRAMEWORK
        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#else
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#endif
        {
            if (GetType() != typeof(GZipStream))
            {
                // GZipStream is not sealed, and a derived type may have overridden WriteAsync(byte[], int, int) prior
                // to this WriteAsync(ReadOnlyMemory<byte>) overload being introduced.  In that case, this
                // WriteAsync(ReadOnlyMemory<byte>) overload should use the behavior of Write(byte[],int,int) overload.
                return base.WriteAsync(buffer, cancellationToken);
            }
            else
            {
                CheckDeflateStream();
                return _deflateStream.WriteAsyncMemory(buffer, cancellationToken);
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            CheckDeflateStream();
            return _deflateStream.FlushAsync(cancellationToken);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            CheckDeflateStream();
            return _deflateStream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        private void CheckDeflateStream()
        {
            if (_deflateStream is null)
                throw new ObjectDisposedException(nameof(_deflateStream));
        }
    }
}

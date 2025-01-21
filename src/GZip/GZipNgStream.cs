


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
    public class GZipNgStream : Stream
    {
        private DeflateNgStream _DeflateNgStream;

        public GZipNgStream(Stream stream, CompressionMode mode) : this(stream, mode, leaveOpen: false)
        {
        }

        public GZipNgStream(Stream stream, CompressionMode mode, bool leaveOpen)
        {
            _DeflateNgStream = new DeflateNgStream(stream, mode, leaveOpen, Interop.ZLib.GZip_DefaultWindowBits);
        }

        // Implies mode = Compress
        public GZipNgStream(Stream stream, CompressionLevel compressionLevel) : this(stream, compressionLevel, leaveOpen: false)
        {
        }

        // Implies mode = Compress
        public GZipNgStream(Stream stream, CompressionLevel compressionLevel, bool leaveOpen)
        {
            _DeflateNgStream = new DeflateNgStream(stream, compressionLevel, leaveOpen, Interop.ZLib.GZip_DefaultWindowBits);
        }

        public override bool CanRead => _DeflateNgStream?.CanRead ?? false;

        public override bool CanWrite => _DeflateNgStream?.CanWrite ?? false;

        public override bool CanSeek => _DeflateNgStream?.CanSeek ?? false;

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
            CheckDeflateNgStream();
            _DeflateNgStream.Flush();
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
            CheckDeflateNgStream();
            return _DeflateNgStream.ReadByte();
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            TaskToAsyncResult.Begin(ReadAsync(buffer, offset, count, CancellationToken.None), asyncCallback!, asyncState!);

        public override int EndRead(IAsyncResult asyncResult) =>
            _DeflateNgStream.EndRead(asyncResult);

        public override int Read(byte[] buffer, int offset, int count)
        {
            CheckDeflateNgStream();
            return _DeflateNgStream.Read(buffer, offset, count);
        }

#if NETFRAMEWORK
        public int Read(Span<byte> buffer)
#else
        public override int Read(Span<byte> buffer)
#endif
        {
            if (GetType() != typeof(GZipNgStream))
            {
                // GZipNgStream is not sealed, and a derived type may have overridden Read(byte[], int, int) prior
                // to this Read(Span<byte>) overload being introduced.  In that case, this Read(Span<byte>) overload
                // should use the behavior of Read(byte[],int,int) overload.
                return base.Read(buffer);
            }
            else
            {
                CheckDeflateNgStream();
                return _DeflateNgStream.ReadCore(buffer);
            }
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            TaskToAsyncResult.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), asyncCallback!, asyncState!);

        public override void EndWrite(IAsyncResult asyncResult) =>
            _DeflateNgStream.EndWrite(asyncResult);

        public override void Write(byte[] buffer, int offset, int count)
        {
            CheckDeflateNgStream();
            _DeflateNgStream.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            if (GetType() != typeof(GZipNgStream))
            {
                // GZipNgStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
                // to this WriteByte override being introduced.  In that case, this WriteByte override
                // should use the behavior of the Write(byte[],int,int) overload.
                base.WriteByte(value);
            }
            else
            {
                CheckDeflateNgStream();
#if NET7_0_OR_GREATER
                _DeflateNgStream.WriteCore(new ReadOnlySpan<byte>(in value));
#else
                _DeflateNgStream.WriteCore(new ReadOnlySpan<byte>(new byte[] { value }));
#endif
            }
        }

#if NETFRAMEWORK
        public void Write(ReadOnlySpan<byte> buffer)
#else
        public override void Write(ReadOnlySpan<byte> buffer)
#endif
        {
            if (GetType() != typeof(GZipNgStream))
            {
                // GZipNgStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
                // to this Write(ReadOnlySpan<byte>) overload being introduced.  In that case, this Write(ReadOnlySpan<byte>) overload
                // should use the behavior of Write(byte[],int,int) overload.
                base.Write(buffer);
            }
            else
            {
                CheckDeflateNgStream();
                _DeflateNgStream.WriteCore(buffer);
            }
        }

#if NETFRAMEWORK
        public new void CopyTo(Stream destination, int bufferSize)
#else
        public override void CopyTo(Stream destination, int bufferSize)
#endif
        {
            CheckDeflateNgStream();
            _DeflateNgStream.CopyTo(destination, bufferSize);
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _DeflateNgStream != null)
                {
                    _DeflateNgStream.Dispose();
                }
                _DeflateNgStream = null!;
            }
            finally
            {
                base.Dispose(disposing);
            }
        }

#if NETCOREAPP
        public override ValueTask DisposeAsync()
        {
            if (GetType() != typeof(GZipNgStream))
            {
                return base.DisposeAsync();
            }

            DeflateNgStream? ds = _DeflateNgStream;
            if (ds != null)
            {
                _DeflateNgStream = null!;
                return ds.DisposeAsync();
            }

            return default;
        }
#endif

        public Stream BaseStream => _DeflateNgStream?.BaseStream!;

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckDeflateNgStream();
            return _DeflateNgStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

#if NETFRAMEWORK
        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
#else
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
#endif
        {
            if (GetType() != typeof(GZipNgStream))
            {
                // GZipNgStream is not sealed, and a derived type may have overridden ReadAsync(byte[], int, int) prior
                // to this ReadAsync(Memory<byte>) overload being introduced.  In that case, this ReadAsync(Memory<byte>) overload
                // should use the behavior of ReadAsync(byte[],int,int) overload.
                return base.ReadAsync(buffer, cancellationToken);
            }
            else
            {
                CheckDeflateNgStream();
                return _DeflateNgStream.ReadAsyncMemory(buffer, cancellationToken);
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            CheckDeflateNgStream();
            return _DeflateNgStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

#if NETFRAMEWORK
        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#else
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#endif
        {
            if (GetType() != typeof(GZipNgStream))
            {
                // GZipNgStream is not sealed, and a derived type may have overridden WriteAsync(byte[], int, int) prior
                // to this WriteAsync(ReadOnlyMemory<byte>) overload being introduced.  In that case, this
                // WriteAsync(ReadOnlyMemory<byte>) overload should use the behavior of Write(byte[],int,int) overload.
                return base.WriteAsync(buffer, cancellationToken);
            }
            else
            {
                CheckDeflateNgStream();
                return _DeflateNgStream.WriteAsyncMemory(buffer, cancellationToken);
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            CheckDeflateNgStream();
            return _DeflateNgStream.FlushAsync(cancellationToken);
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            CheckDeflateNgStream();
            return _DeflateNgStream.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        private void CheckDeflateNgStream()
        {
            if (_DeflateNgStream is null)
                throw new ObjectDisposedException(nameof(_DeflateNgStream));
        }
    }
}




using System.Buffers;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>Provides methods and properties used to compress and decompress streams by using the Brotli data format specification.</summary>
    public sealed partial class BrotliStream : Stream
    {
        private const int DefaultInternalBufferSize = (1 << 16) - 16; //65520;
        private Stream _stream;
        private byte[] _buffer;
        private readonly bool _leaveOpen;
        private readonly bool _compress;
        private readonly CompressionVersion _version;

        /// <summary>Initializes a new instance of the <see cref="Nanook.GrindCore.BrotliStream" /> class by using the specified stream and compression mode.</summary>
        /// <param name="stream">The stream to which compressed data is written or from which data to decompress is read.</param>
        /// <param name="type">CompressionLevel or Decompress, indicates whether to emphasize speed or compression efficiency when compressing data to the stream.</param>
        public BrotliStream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, leaveOpen: false, version) { }

        /// <summary>Initializes a new instance of the <see cref="Nanook.GrindCore.BrotliStream" /> class by using the specified stream and compression mode, and optionally leaves the stream open.</summary>
        /// <param name="stream">The stream to which compressed data is written or from which data to decompress is read.</param>
        /// <param name="mode">One of the enumeration values that indicates whether to compress data to the stream or decompress data from the stream.</param>
        /// <param name="leaveOpen"><see langword="true" /> to leave the stream open after the <see cref="Nanook.GrindCore.BrotliStream" /> object is _disposed; otherwise, <see langword="false" />.</param>
        public BrotliStream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _version = version ?? CompressionVersion.BrotliLatest();

            if (type != CompressionType.Decompress)
            {
                if (!stream.CanWrite)
                    throw new ArgumentException(SR.Stream_FalseCanWrite, nameof(stream));

                _encoder.SetQuality(BrotliUtils.GetQualityFromCompressionLevel(type));
                _encoder.SetWindow(BrotliUtils.WindowBits_Default);
                _compress = true;
            }
            else
            {
                if (!stream.CanRead)
                    throw new ArgumentException(SR.Stream_FalseCanRead, nameof(stream));

                _compress = false;
            }

            _stream = stream;
            _leaveOpen = leaveOpen;
            _buffer = ArrayPool<byte>.Shared.Rent(DefaultInternalBufferSize);
        }

        private void EnsureNotDisposed()
        {
            if (_stream == null)
                throw new ObjectDisposedException(nameof(BrotliStream));
        }

        /// <summary>Releases the unmanaged resources used by the <see cref="Nanook.GrindCore.BrotliStream" /> and optionally releases the managed resources.</summary>
        /// <param name="disposing"><see langword="true" /> to release both managed and unmanaged resources; <see langword="false" /> to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing && _stream != null)
                {
                    if (_compress)
                    {
                        WriteCore(ReadOnlySpan<byte>.Empty, isFinalBlock: true);
                    }

                    if (!_leaveOpen)
                    {
                        _stream.Dispose();
                    }
                }
            }
            finally
            {
                ReleaseStateForDispose();
                base.Dispose(disposing);
            }
        }

#if NETCOREAPP
        /// <summary>Asynchronously releases the unmanaged resources used by the <see cref="Nanook.GrindCore.BrotliStream" />.</summary>
        /// <returns>A task that represents the asynchronous dispose operation.</returns>
        /// <remarks><para>This method lets you perform a resource-intensive dispose operation without blocking the main thread. This performance consideration is particularly important in apps where a time-consuming stream operation can block the UI thread and make your app appear as if it is not working. The async methods are used in conjunction with the <see langword="async" /> and <see langword="await" /> keywords in Visual Basic and C#.</para>
        /// <para>This method disposes the Brotli stream by writing any changes to the backing store and closing the stream to release resources.</para>
        /// <para>Calling <see cref="Nanook.GrindCore.BrotliStream.DisposeAsync" /> allows the resources used by the <see cref="Nanook.GrindCore.BrotliStream" /> to be reallocated for other purposes. For more information, see [Cleaning Up Unmanaged Resources](/dotnet/standard/garbage-collection/unmanaged).</para></remarks>
        public override async ValueTask DisposeAsync()
        {
            try
            {
                if (_stream != null)
                {
                    if (_compress)
                    {
                        await WriteAsyncMemoryCore(ReadOnlyMemory<byte>.Empty, CancellationToken.None, isFinalBlock: true).ConfigureAwait(false);
                    }

                    if (!_leaveOpen)
                    {
                        await _stream.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                ReleaseStateForDispose();
            }
        }
#endif

        private void ReleaseStateForDispose()
        {
            _stream = null!;
            _encoder.Dispose();
            _decoder.Dispose();

            byte[] buffer = _buffer;
            if (buffer != null)
            {
                _buffer = null!;
                if (!AsyncOperationIsActive)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }

        /// <summary>Gets a reference to the underlying stream.</summary>
        /// <value>A stream object that represents the underlying stream.</value>
        /// <exception cref="System.ObjectDisposedException">The underlying stream is closed.</exception>
        public Stream BaseStream => _stream;
        /// <summary>Gets a value indicating whether the stream supports reading while decompressing a file.</summary>
        /// <value><see langword="true" /> if the <see cref="System.IO.Compression.CompressionMode" /> value is <see langword="Decompress," /> and the underlying stream supports reading and is not closed; otherwise, <see langword="false" />.</value>
        public override bool CanRead => !_compress && _stream != null && _stream.CanRead;
        /// <summary>Gets a value indicating whether the stream supports writing.</summary>
        /// <value><see langword="true" /> if the <see cref="System.IO.Compression.CompressionMode" /> value is <see langword="Compress" />, and the underlying stream supports writing and is not closed; otherwise, <see langword="false" />.</value>
        public override bool CanWrite => _compress && _stream != null && _stream.CanWrite;
        /// <summary>Gets a value indicating whether the stream supports seeking.</summary>
        /// <value><see langword="false" /> in all cases.</value>
        public override bool CanSeek => false;
        /// <summary>This property is not supported and always throws a <see cref="System.NotSupportedException" />.</summary>
        /// <param name="offset">The location in the stream.</param>
        /// <param name="origin">One of the <see cref="System.IO.SeekOrigin" /> values.</param>
        /// <returns>A long value.</returns>
        /// <exception cref="System.NotSupportedException">This property is not supported on this stream.</exception>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        /// <summary>This property is not supported and always throws a <see cref="System.NotSupportedException" />.</summary>
        /// <value>A long value.</value>
        /// <exception cref="System.NotSupportedException">This property is not supported on this stream.</exception>
        public override long Length => throw new NotSupportedException();
        /// <summary>This property is not supported and always throws a <see cref="System.NotSupportedException" />.</summary>
        /// <value>A long value.</value>
        /// <exception cref="System.NotSupportedException">This property is not supported on this stream.</exception>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        /// <summary>This property is not supported and always throws a <see cref="System.NotSupportedException" />.</summary>
        /// <param name="value">The length of the stream.</param>
        public override void SetLength(long value) => throw new NotSupportedException();

        private int _activeAsyncOperation; // 1 == true, 0 == false
        private bool AsyncOperationIsActive => _activeAsyncOperation != 0;

        private void EnsureNoActiveAsyncOperation()
        {
            if (AsyncOperationIsActive)
                ThrowInvalidBeginCall();
        }

        private void AsyncOperationStarting()
        {
            if (Interlocked.Exchange(ref _activeAsyncOperation, 1) != 0)
            {
                ThrowInvalidBeginCall();
            }
        }

        private void AsyncOperationCompleting()
        {
            Debug.Assert(_activeAsyncOperation == 1);
            Volatile.Write(ref _activeAsyncOperation, 0);
        }

        private static void ThrowInvalidBeginCall() =>
            throw new InvalidOperationException(SR.InvalidBeginCall);
    }
}

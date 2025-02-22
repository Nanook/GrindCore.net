


using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System;

namespace Nanook.GrindCore.DeflateZLib
{
    public partial class DeflateStream : Stream
    {
        private const int DefaultBufferSize = 8192;

        private Stream _stream;
        private bool _compress;
        private bool _leaveOpen;
        private Inflater? _inflater;
        private Deflater? _deflater;
        private byte[]? _buffer;
        private int _activeAsyncOperation; // 1 == true, 0 == false
        private bool _wroteBytes;

       internal DeflateStream(Stream stream, long uncompressedSize, CompressionVersion? version = null) : this(stream, CompressionType.Decompress, leaveOpen: false, Interop.ZLib.Deflate_DefaultWindowBits, version, uncompressedSize)
        {
        }

        public DeflateStream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, leaveOpen: false, Interop.ZLib.Deflate_DefaultWindowBits, version)
        {
        }

        public DeflateStream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null) : this(stream, type, leaveOpen, Interop.ZLib.Deflate_DefaultWindowBits, version)
        {
        }

        /// <summary>
        /// Internal constructor to check stream validity and call the correct initialization function depending on
        /// the value of the CompressionMode given.
        /// </summary>
        internal DeflateStream(Stream stream, CompressionType type, bool leaveOpen, int windowBits, CompressionVersion? version = null, long uncompressedSize = -1)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (version == null)
                version = CompressionVersion.ZLibNgLatest();

            if (type == CompressionType.Decompress)
            {
                if (!stream.CanRead)
                    throw new ArgumentException(SR.NotSupported_UnreadableStream, nameof(stream));

                _inflater = new Inflater(version, windowBits, uncompressedSize);
                _stream = stream;
                _compress = false;
                _leaveOpen = leaveOpen;
            }
            else
                InitializeDeflater(version, stream, leaveOpen, windowBits, type);
        }
        /// <summary>
        /// Sets up this DeflateStream to be used for Zlib Deflation/Compression
        /// </summary>
        internal void InitializeDeflater(CompressionVersion version, Stream stream, bool leaveOpen, int windowBits, CompressionType compressionLevel)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream!.CanWrite)
                throw new ArgumentException(SR.NotSupported_UnwritableStream, nameof(stream));

            _deflater = new Deflater(version, compressionLevel, windowBits);

            _stream = stream;
            _compress = true;
            _leaveOpen = leaveOpen;
            InitializeBuffer();
        }

        private void InitializeBuffer()
        {
            _buffer = ArrayPool<byte>.Shared.Rent(DefaultBufferSize);
        }

        private void EnsureBufferInitialized()
        {
            if (_buffer == null)
            {
                InitializeBuffer();
            }
        }

        public Stream BaseStream => _stream;

        public override bool CanRead => _stream != null && !_compress && _stream.CanRead;

        public override bool CanWrite => _stream != null && _compress && _stream.CanWrite;

        public override bool CanSeek => false;

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
            EnsureNotDisposed();
            if (_compress)
                FlushBuffers();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            EnsureNoActiveAsyncOperation();
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            return !_compress ?
                Task.CompletedTask :
                Core(cancellationToken);

            async Task Core(CancellationToken cancellationToken)
            {
                AsyncOperationStarting();
                try
                {
                    Debug.Assert(_deflater != null && _buffer != null);

                    // Compress any bytes left:
                    await WriteDeflaterOutputAsync(cancellationToken).ConfigureAwait(false);

                    // Pull out any bytes left inside deflater:
                    bool flushSuccessful;
                    do
                    {
                        int compressedBytes;
                        flushSuccessful = _deflater!.Flush(_buffer!, out compressedBytes);
                        if (flushSuccessful)
                        {
                            await _stream.WriteAsync(new ReadOnlyMemory<byte>(_buffer, 0, compressedBytes), cancellationToken).ConfigureAwait(false);
                        }
                        Debug.Assert(flushSuccessful == compressedBytes > 0);
                    } while (flushSuccessful);

                    // Always flush on the underlying stream
                    await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    AsyncOperationCompleting();
                }
            }
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
            EnsureDecompressionMode();
            EnsureNotDisposed();

            // Try to read a single byte from zlib without allocating an array, pinning an array, etc.
            // If zlib doesn't have any data, fall back to the base stream implementation, which will do that.
            byte b;
            Debug.Assert(_inflater != null);
            return _inflater!.Inflate(out b) ? b : base.ReadByte();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadCore(new Span<byte>(buffer, offset, count));
        }

#if NETFRAMEWORK
        public int Read(Span<byte> buffer)
#else
        public override int Read(Span<byte> buffer)
#endif
        {
            if (GetType() != typeof(DeflateStream))
            {
                // DeflateStream is not sealed, and a derived type may have overridden Read(byte[], int, int) prior
                // to this Read(Span<byte>) overload being introduced.  In that case, this Read(Span<byte>) overload
                // should use the behavior of Read(byte[],int,int) overload.
                return base.Read(buffer);
            }
            else
            {
                return ReadCore(buffer);
            }
        }

        internal int ReadCore(Span<byte> buffer)
        {
            EnsureDecompressionMode();
            EnsureNotDisposed();
            EnsureBufferInitialized();
            Debug.Assert(_inflater != null);

            int bytesRead;
            while (true)
            {
                // Try to decompress any data from the inflater into the caller's buffer.
                // If we're able to decompress any bytes, or if decompression is completed, we're done.
                bytesRead = _inflater!.Inflate(buffer);
                if (bytesRead != 0 || InflatorIsFinished)
                {
                    break;
                }

                // We were unable to decompress any data.  If the inflater needs additional input
                // data to proceed, read some to populate it.
                if (_inflater.NeedsInput())
                {
                    int n = _stream.Read(_buffer!, 0, _buffer!.Length);
                    if (n <= 0)
                    {
                        // - Inflater didn't return any data although a non-empty output buffer was passed by the caller.
                        // - More input is needed but there is no more input available.
                        // - Inflation is not finished yet.
                        // - Provided input wasn't completely empty
                        // In such case, we are dealing with a truncated input stream.
                        if (s_useStrictValidation && !buffer.IsEmpty && !_inflater.Finished() && _inflater.NonEmptyInput())
                        {
                            ThrowTruncatedInvalidData();
                        }
                        break;
                    }
                    else if (n > _buffer.Length)
                    {
                        ThrowGenericInvalidData();
                    }
                    else
                    {
                        _inflater.SetInput(_buffer, 0, n);
                    }
                }

                if (buffer.IsEmpty)
                {
                    // The caller provided a zero-byte buffer.  This is typically done in order to avoid allocating/renting
                    // a buffer until data is known to be available.  We don't have perfect knowledge here, as _inflater.Inflate
                    // will return 0 whether or not more data is required, and having input data doesn't necessarily mean it'll
                    // decompress into at least one byte of output, but it's a reasonable approximation for the 99% case.  If it's
                    // wrong, it just means that a caller using zero-byte reads as a way to delay getting a buffer to use for a
                    // subsequent call may end up getting one earlier than otherwise preferred.
                    Debug.Assert(bytesRead == 0);
                    break;
                }
            }

            return bytesRead;
        }

        private bool InflatorIsFinished =>
            // If the stream is finished then we have a few potential cases here:
            // 1. DeflateStream => return
            // 2. GZipStream that is finished but may have an additional GZipStream appended => feed more input
            // 3. GZipStream that is finished and appended with garbage => return
            _inflater!.Finished() &&
            (!_inflater.IsGzipStream() || !_inflater.NeedsInput());

        private void EnsureNotDisposed()
        {
            if (_stream is null)
                throw new ObjectDisposedException(nameof(_stream));
        }

        private void EnsureDecompressionMode()
        {
            if (_compress)
                ThrowCannotReadFromDeflateStreamException();

            static void ThrowCannotReadFromDeflateStreamException() =>
                throw new InvalidOperationException(SR.CannotReadFromDeflateStream);
        }

        private void EnsureCompressionMode()
        {
            if (!_compress)
                ThrowCannotWriteToDeflateStreamException();

            static void ThrowCannotWriteToDeflateStreamException() =>
                throw new InvalidOperationException(SR.CannotWriteToDeflateStream);
        }

        private static void ThrowGenericInvalidData() =>
            // The stream is either malicious or poorly implemented and returned a number of
            // bytes < 0 || > than the buffer supplied to it.
            throw new InvalidDataException(SR.GenericInvalidData);

        private static void ThrowTruncatedInvalidData() =>
            throw new InvalidDataException(SR.TruncatedData);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            TaskToAsyncResult.Begin(ReadAsync(buffer, offset, count, CancellationToken.None), asyncCallback!, asyncState!);

        public override int EndRead(IAsyncResult asyncResult)
        {
            EnsureDecompressionMode();
            EnsureNotDisposed();
            return TaskToAsyncResult.End<int>(asyncResult);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return ReadAsyncMemory(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

#if NETFRAMEWORK
        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
#else
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
#endif
        {
            if (GetType() != typeof(DeflateStream))
            {
                // Ensure that existing streams derived from DeflateStream and that override ReadAsync(byte[],...)
                // get their existing behaviors when the newer Memory-based overload is used.
                return base.ReadAsync(buffer, cancellationToken);
            }
            else
            {
                return ReadAsyncMemory(buffer, cancellationToken);
            }
        }

        internal ValueTask<int> ReadAsyncMemory(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            EnsureDecompressionMode();
            EnsureNoActiveAsyncOperation();
            EnsureNotDisposed();

            if (cancellationToken.IsCancellationRequested)
            {
#if NET5_0_OR_GREATER
                return ValueTask.FromCanceled<int>(cancellationToken);
#else
                return ValueTaskExtensions.FromCanceled<int>(cancellationToken);
#endif
            }

            EnsureBufferInitialized();
            Debug.Assert(_inflater != null);

            return Core(buffer, cancellationToken);

            async ValueTask<int> Core(Memory<byte> buffer, CancellationToken cancellationToken)
            {
                AsyncOperationStarting();
                try
                {
                    int bytesRead;
                    while (true)
                    {
                        // Try to decompress any data from the inflater into the caller's buffer.
                        // If we're able to decompress any bytes, or if decompression is completed, we're done.
                        bytesRead = _inflater!.Inflate(buffer.Span);
                        if (bytesRead != 0 || InflatorIsFinished)
                        {
                            break;
                        }

                        // We were unable to decompress any data.  If the inflater needs additional input
                        // data to proceed, read some to populate it.
                        if (_inflater.NeedsInput())
                        {
                            int n = await _stream.ReadAsync(new Memory<byte>(_buffer, 0, _buffer!.Length), cancellationToken).ConfigureAwait(false);
                            if (n <= 0)
                            {
                                // - Inflater didn't return any data although a non-empty output buffer was passed by the caller.
                                // - More input is needed but there is no more input available.
                                // - Inflation is not finished yet.
                                // - Provided input wasn't completely empty
                                // In such case, we are dealing with a truncated input stream.
                                if (s_useStrictValidation && !_inflater.Finished() && _inflater.NonEmptyInput() && !buffer.IsEmpty)
                                {
                                    ThrowTruncatedInvalidData();
                                }
                                break;
                            }
                            else if (n > _buffer.Length)
                            {
                                ThrowGenericInvalidData();
                            }
                            else
                            {
                                _inflater.SetInput(_buffer, 0, n);
                            }
                        }

                        if (buffer.IsEmpty)
                        {
                            // The caller provided a zero-byte buffer.  This is typically done in order to avoid allocating/renting
                            // a buffer until data is known to be available.  We don't have perfect knowledge here, as _inflater.Inflate
                            // will return 0 whether or not more data is required, and having input data doesn't necessarily mean it'll
                            // decompress into at least one byte of output, but it's a reasonable approximation for the 99% case.  If it's
                            // wrong, it just means that a caller using zero-byte reads as a way to delay getting a buffer to use for a
                            // subsequent call may end up getting one earlier than otherwise preferred.
                            break;
                        }
                    }

                    return bytesRead;
                }
                finally
                {
                    AsyncOperationCompleting();
                }
            }
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            WriteCore(new ReadOnlySpan<byte>(buffer, offset, count));
        }

        public override void WriteByte(byte value)
        {
            if (GetType() != typeof(DeflateStream))
            {
                // DeflateStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
                // to this WriteByte override being introduced.  In that case, this WriteByte override
                // should use the behavior of the Write(byte[],int,int) overload.
                base.WriteByte(value);
            }
            else
            {
#if NET7_0_OR_GREATER
                WriteCore(new ReadOnlySpan<byte>(in value));
#else
                WriteCore(new ReadOnlySpan<byte>(new byte[] { value }));
#endif
            }
        }

#if NETFRAMEWORK
        public void Write(ReadOnlySpan<byte> buffer)
#else
        public override void Write(ReadOnlySpan<byte> buffer)
#endif
        {
            if (GetType() != typeof(DeflateStream))
            {
                // DeflateStream is not sealed, and a derived type may have overridden Write(byte[], int, int) prior
                // to this Write(ReadOnlySpan<byte>) overload being introduced.  In that case, this Write(ReadOnlySpan<byte>) overload
                // should use the behavior of Write(byte[],int,int) overload.
                base.Write(buffer);
            }
            else
            {
                WriteCore(buffer);
            }
        }

        internal void WriteCore(ReadOnlySpan<byte> buffer)
        {
            EnsureCompressionMode();
            EnsureNotDisposed();

            Debug.Assert(_deflater != null);
            // Write compressed the bytes we already passed to the deflater:
            WriteDeflaterOutput();

            unsafe
            {
                // Pass new bytes through deflater and write them too:
                fixed (byte* bufferPtr = &MemoryMarshal.GetReference(buffer))
                {
                    _deflater!.SetInput(bufferPtr, buffer.Length);
                    WriteDeflaterOutput();
                    _wroteBytes = true;
                }
            }
        }

        private void WriteDeflaterOutput()
        {
            Debug.Assert(_deflater != null && _buffer != null);
            while (!_deflater!.NeedsInput())
            {
                int compressedBytes = _deflater!.GetDeflateOutput(_buffer!);
                if (compressedBytes > 0)
                {
                    _stream.Write(_buffer, 0, compressedBytes);
                }
            }
        }

        // This is called by Flush:
        private void FlushBuffers()
        {
            if (_wroteBytes)
            {
                // Compress any bytes left:
                WriteDeflaterOutput();

                Debug.Assert(_deflater != null && _buffer != null);
                // Pull out any bytes left inside deflater:
                bool flushSuccessful;
                do
                {
                    int compressedBytes;
                    flushSuccessful = _deflater!.Flush(_buffer!, out compressedBytes);
                    if (flushSuccessful)
                    {
                        _stream.Write(_buffer, 0, compressedBytes);
                    }
                    Debug.Assert(flushSuccessful == compressedBytes > 0);
                } while (flushSuccessful);
            }

            // Always flush on the underlying stream
            _stream.Flush();
        }

        // This is called by Dispose:
        private void PurgeBuffers(bool disposing)
        {
            if (!disposing)
                return;

            if (_stream == null)
                return;

            if (!_compress)
                return;

            Debug.Assert(_deflater != null && _buffer != null);
            // Some deflaters (e.g. ZLib) write more than zero bytes for zero byte inputs.
            // This round-trips and we should be ok with this, but our legacy managed deflater
            // always wrote zero output for zero input and upstack code (e.g. ZipArchiveEntry)
            // took dependencies on it. Thus, make sure to only "flush" when we actually had
            // some input:
            if (_wroteBytes)
            {
                // Compress any bytes left
                WriteDeflaterOutput();

                // Pull out any bytes left inside deflater:
                bool finished;
                do
                {
                    int compressedBytes;
                    finished = _deflater!.Finish(_buffer!, out compressedBytes);

                    if (compressedBytes > 0)
                        _stream.Write(_buffer, 0, compressedBytes);
                } while (!finished);
            }
            else
            {
                // In case of zero length buffer, we still need to clean up the native created stream before
                // the object get _disposed because eventually ZLibNative.ReleaseHandle will get called during
                // the dispose operation and although it frees the stream but it return error code because the
                // stream state was still marked as in use. The symptoms of this problem will not be seen except
                // if running any diagnostic tools which check for disposing safe handle objects
                bool finished;
                do
                {
                    finished = _deflater!.Finish(_buffer!, out _);
                } while (!finished);
            }
        }

        private async ValueTask PurgeBuffersAsync()
        {
            // Same logic as PurgeBuffers, except with async counterparts.

            if (_stream == null)
                return;

            if (!_compress)
                return;

            Debug.Assert(_deflater != null && _buffer != null);
            // Some deflaters (e.g. ZLib) write more than zero bytes for zero byte inputs.
            // This round-trips and we should be ok with this, but our legacy managed deflater
            // always wrote zero output for zero input and upstack code (e.g. ZipArchiveEntry)
            // took dependencies on it. Thus, make sure to only "flush" when we actually had
            // some input.
            if (_wroteBytes)
            {
                // Compress any bytes left
                await WriteDeflaterOutputAsync(default).ConfigureAwait(false);

                // Pull out any bytes left inside deflater:
                bool finished;
                do
                {
                    int compressedBytes;
                    finished = _deflater!.Finish(_buffer!, out compressedBytes);

                    if (compressedBytes > 0)
                        await _stream.WriteAsync(new ReadOnlyMemory<byte>(_buffer, 0, compressedBytes)).ConfigureAwait(false);
                } while (!finished);
            }
            else
            {
                // In case of zero length buffer, we still need to clean up the native created stream before
                // the object get _disposed because eventually ZLibNative.ReleaseHandle will get called during
                // the dispose operation and although it frees the stream, it returns an error code because the
                // stream state was still marked as in use. The symptoms of this problem will not be seen except
                // if running any diagnostic tools which check for disposing safe handle objects.
                bool finished;
                do
                {
                    finished = _deflater!.Finish(_buffer!, out _);
                } while (!finished);
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                PurgeBuffers(disposing);
            }
            finally
            {
                // Close the underlying stream even if PurgeBuffers threw.
                // Stream.Close() may throw here (may or may not be due to the same error).
                // In this case, we still need to clean up internal resources, hence the inner finally blocks.
                try
                {
                    if (disposing && !_leaveOpen)
                        _stream?.Dispose();
                }
                finally
                {
                    _stream = null!;

                    try
                    {
                        _deflater?.Dispose();
                        _inflater?.Dispose();
                    }
                    finally
                    {
                        _deflater = null;
                        _inflater = null;

                        byte[]? buffer = _buffer;
                        if (buffer != null)
                        {
                            _buffer = null;
                            if (!AsyncOperationIsActive)
                            {
                                ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }

                        base.Dispose(disposing);
                    }
                }
            }
        }

#if NETCOREAPP
        public override ValueTask DisposeAsync()
        {
            return GetType() == typeof(DeflateStream) ?
                Core() :
                base.DisposeAsync();

            async ValueTask Core()
            {
                // Same logic as Dispose(true), except with async counterparts.
                try
                {
                    await PurgeBuffersAsync().ConfigureAwait(false);
                }
                finally
                {
                    // Close the underlying stream even if PurgeBuffers threw.
                    // Stream.Close() may throw here (may or may not be due to the same error).
                    // In this case, we still need to clean up internal resources, hence the inner finally blocks.
                    Stream stream = _stream;
                    _stream = null!;
                    try
                    {
                        if (!_leaveOpen && stream != null)
                            await stream.DisposeAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        try
                        {
                            _deflater?.Dispose();
                            _inflater?.Dispose();
                        }
                        finally
                        {
                            _deflater = null;
                            _inflater = null;

                            byte[]? buffer = _buffer;
                            if (buffer != null)
                            {
                                _buffer = null;
                                if (!AsyncOperationIsActive)
                                {
                                    ArrayPool<byte>.Shared.Return(buffer);
                                }
                            }
                        }
                    }
                }
            }
        }
#endif

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? asyncCallback, object? asyncState) =>
            TaskToAsyncResult.Begin(WriteAsync(buffer, offset, count, CancellationToken.None), asyncCallback!, asyncState!);

        public override void EndWrite(IAsyncResult asyncResult)
        {
            EnsureCompressionMode();
            EnsureNotDisposed();
            TaskToAsyncResult.End<IAsyncResult>(asyncResult);
        }

#if NETFRAMEWORK || NETCOREAPP3_1 || NET5_0 || NETSTANDARD2_1
        private void ValidateBufferArguments(byte[]? buffer, int offset, int count)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer), "Buffer cannot be null.");

            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset is out of range.");

            if (count < 0 || offset + count > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(count), "Count is out of range.");
        }


        private void ValidateCopyToArguments(Stream destination, int bufferSize)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination), "Destination stream cannot be null.");

            if (!CanRead && !CanWrite)
                throw new ObjectDisposedException(null, "Cannot copy from a disposed stream.");

            if (!destination.CanWrite)
                throw new NotSupportedException("Destination stream must be writable.");

            if (bufferSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(bufferSize), "Buffer size must be greater than zero.");
        }
#endif

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ValidateBufferArguments(buffer, offset, count);
            return WriteAsyncMemory(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

#if NETFRAMEWORK
        public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (GetType() != typeof(DeflateStream))
            {
                // Ensure that existing streams derived from DeflateStream and that override WriteAsync(byte[],...)
                // get their existing behaviors when the newer Memory-based overload is used.
                return new ValueTask(base.WriteAsync(buffer.ToArray(), 0, buffer.Length, cancellationToken));
            }
            else
            {
                return WriteAsyncMemory(buffer, cancellationToken);
            }
        }
#else
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            if (GetType() != typeof(DeflateStream))
            {
                // Ensure that existing streams derived from DeflateStream and that override WriteAsync(byte[],...)
                // get their existing behaviors when the newer Memory-based overload is used.
                return base.WriteAsync(buffer, cancellationToken);
            }
            else
            {
                return WriteAsyncMemory(buffer, cancellationToken);
            }
        }
#endif

        internal ValueTask WriteAsyncMemory(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            EnsureCompressionMode();
            EnsureNoActiveAsyncOperation();
            EnsureNotDisposed();

            return cancellationToken.IsCancellationRequested ?
#if NET5_0_OR_GREATER
                ValueTask.FromCanceled(cancellationToken)
#else
                ValueTaskExtensions.FromCanceled(cancellationToken)
#endif
                : Core(buffer, cancellationToken);

            async ValueTask Core(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
            {
                AsyncOperationStarting();
                try
                {
                    await WriteDeflaterOutputAsync(cancellationToken).ConfigureAwait(false);

                    Debug.Assert(_deflater != null);
                    // Pass new bytes through deflater
                    _deflater!.SetInput(buffer);

                    await WriteDeflaterOutputAsync(cancellationToken).ConfigureAwait(false);

                    _wroteBytes = true;
                }
                finally
                {
                    AsyncOperationCompleting();
                }
            }
        }

        /// <summary>
        /// Writes the bytes that have already been deflated
        /// </summary>
        private async ValueTask WriteDeflaterOutputAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(_deflater != null && _buffer != null);
            while (!_deflater!.NeedsInput())
            {
                int compressedBytes = _deflater.GetDeflateOutput(_buffer!);
                if (compressedBytes > 0)
                {
#if NETFRAMEWORK
                    await _stream.WriteAsync(_buffer, 0, compressedBytes, cancellationToken).ConfigureAwait(false);
#else
                    await _stream.WriteAsync(new ReadOnlyMemory<byte>(_buffer, 0, compressedBytes), cancellationToken).ConfigureAwait(false);
#endif
                }
            }
        }

#if NETFRAMEWORK
        public new void CopyTo(Stream destination, int bufferSize)
#else
        public override void CopyTo(Stream destination, int bufferSize)
#endif
        {
            ValidateCopyToArguments(destination, bufferSize);

            EnsureNotDisposed();
            if (!CanRead) throw new NotSupportedException();

            new CopyToStream(this, destination, bufferSize).CopyFromSourceToDestination();
        }

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            ValidateCopyToArguments(destination, bufferSize);

            EnsureNotDisposed();
            if (!CanRead) throw new NotSupportedException();
            EnsureNoActiveAsyncOperation();

            // Early check for cancellation
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<int>(cancellationToken);
            }

            // Do the copy
            return new CopyToStream(this, destination, bufferSize, cancellationToken).CopyFromSourceToDestinationAsync();
        }

        private sealed class CopyToStream : Stream
        {
            private readonly DeflateStream _deflateStream;
            private readonly Stream _destination;
            private readonly CancellationToken _cancellationToken;
            private byte[] _arrayPoolBuffer;

            public CopyToStream(DeflateStream deflateStream, Stream destination, int bufferSize) :
                this(deflateStream, destination, bufferSize, CancellationToken.None)
            {
            }

            public CopyToStream(DeflateStream deflateStream, Stream destination, int bufferSize, CancellationToken cancellationToken)
            {
                Debug.Assert(deflateStream != null);
                Debug.Assert(destination != null);
                Debug.Assert(bufferSize > 0);

                _deflateStream = deflateStream!;
                _destination = destination!;
                _cancellationToken = cancellationToken;
                _arrayPoolBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);
            }

            public async Task CopyFromSourceToDestinationAsync()
            {
                _deflateStream.AsyncOperationStarting();
                try
                {
                    Debug.Assert(_deflateStream._inflater != null);
                    // Flush any existing data in the inflater to the destination stream.
                    while (!_deflateStream._inflater!.Finished())
                    {
                        int bytesRead = _deflateStream._inflater.Inflate(_arrayPoolBuffer, 0, _arrayPoolBuffer.Length);
                        if (bytesRead > 0)
                        {
#if NETFRAMEWORK
                            await _destination.WriteAsync(_arrayPoolBuffer, 0, bytesRead, _cancellationToken).ConfigureAwait(false);
#else
                            await _destination.WriteAsync(new ReadOnlyMemory<byte>(_arrayPoolBuffer, 0, bytesRead), _cancellationToken).ConfigureAwait(false);
#endif
                        }
                        else if (_deflateStream._inflater.NeedsInput())
                        {
                            // only break if we read 0 and ran out of input, if input is still available it may be another GZip payload
                            break;
                        }
                    }

                    // Now, use the source stream's CopyToAsync to push directly to our inflater via this helper stream
                    await _deflateStream._stream.CopyToAsync(this, _arrayPoolBuffer.Length, _cancellationToken).ConfigureAwait(false);
                    if (s_useStrictValidation && !_deflateStream._inflater.Finished())
                    {
                        ThrowTruncatedInvalidData();
                    }
                }
                finally
                {
                    _deflateStream.AsyncOperationCompleting();

                    ArrayPool<byte>.Shared.Return(_arrayPoolBuffer);
                    _arrayPoolBuffer = null!;
                }
            }

            public void CopyFromSourceToDestination()
            {
                try
                {
                    Debug.Assert(_deflateStream._inflater != null);
                    // Flush any existing data in the inflater to the destination stream.
                    while (!_deflateStream._inflater!.Finished())
                    {
                        int bytesRead = _deflateStream._inflater.Inflate(_arrayPoolBuffer, 0, _arrayPoolBuffer.Length);
                        if (bytesRead > 0)
                        {
                            _destination.Write(_arrayPoolBuffer, 0, bytesRead);
                        }
                        else if (_deflateStream._inflater.NeedsInput())
                        {
                            // only break if we read 0 and ran out of input, if input is still available it may be another GZip payload
                            break;
                        }
                    }

                    // Now, use the source stream's CopyToAsync to push directly to our inflater via this helper stream
                    _deflateStream._stream.CopyTo(this, _arrayPoolBuffer.Length);
                    if (s_useStrictValidation && !_deflateStream._inflater.Finished())
                    {
                        ThrowTruncatedInvalidData();
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(_arrayPoolBuffer);
                    _arrayPoolBuffer = null!;
                }
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                Debug.Assert(buffer != _arrayPoolBuffer);
                _deflateStream.EnsureNotDisposed();
                if (count <= 0)
                {
                    return Task.CompletedTask;
                }
                else if (count > buffer.Length - offset)
                {
                    // The buffer stream is either malicious or poorly implemented and returned a number of
                    // bytes larger than the buffer supplied to it.
                    return Task.FromException(new InvalidDataException(SR.GenericInvalidData));
                }

                return WriteAsyncCore(buffer.AsMemory(offset, count), cancellationToken).AsTask();
            }

#if NETFRAMEWORK
            public ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#else
            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
#endif
            {
                _deflateStream.EnsureNotDisposed();

                return WriteAsyncCore(buffer, cancellationToken);
            }

            private async ValueTask WriteAsyncCore(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
            {
                Debug.Assert(_deflateStream._inflater is not null);

                // Feed the data from base stream into decompression engine.
                _deflateStream._inflater!.SetInput(buffer);

                // While there's more decompressed data available, forward it to the buffer stream.
                while (!_deflateStream._inflater.Finished())
                {
                    int bytesRead = _deflateStream._inflater.Inflate(new Span<byte>(_arrayPoolBuffer));
                    if (bytesRead > 0)
                    {
#if NETFRAMEWORK
                        await _destination.WriteAsync(_arrayPoolBuffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
#else
                        await _destination.WriteAsync(new ReadOnlyMemory<byte>(_arrayPoolBuffer, 0, bytesRead), cancellationToken).ConfigureAwait(false);
#endif
                    }
                    else if (_deflateStream._inflater.NeedsInput())
                    {
                        // only break if we read 0 and ran out of input, if input is still available it may be another GZip payload
                        break;
                    }
                }
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Debug.Assert(buffer != _arrayPoolBuffer);
                _deflateStream.EnsureNotDisposed();

                if (count <= 0)
                {
                    return;
                }
                else if (count > buffer.Length - offset)
                {
                    // The buffer stream is either malicious or poorly implemented and returned a number of
                    // bytes larger than the buffer supplied to it.
                    throw new InvalidDataException(SR.GenericInvalidData);
                }

                Debug.Assert(_deflateStream._inflater != null);
                // Feed the data from base stream into the decompression engine.
                _deflateStream._inflater!.SetInput(buffer, offset, count);

                // While there's more decompressed data available, forward it to the buffer stream.
                while (!_deflateStream._inflater.Finished())
                {
                    int bytesRead = _deflateStream._inflater.Inflate(new Span<byte>(_arrayPoolBuffer));
                    if (bytesRead > 0)
                    {
                        _destination.Write(_arrayPoolBuffer, 0, bytesRead);
                    }
                    else if (_deflateStream._inflater.NeedsInput())
                    {
                        // only break if we read 0 and ran out of input, if input is still available it may be another GZip payload
                        break;
                    }
                }
            }

            public override bool CanWrite => true;
            public override void Flush() { }
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
        }

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

        private void AsyncOperationCompleting() =>
            Volatile.Write(ref _activeAsyncOperation, 0);

        private static void ThrowInvalidBeginCall() =>
            throw new InvalidOperationException(SR.InvalidBeginCall);

        private static readonly bool s_useStrictValidation =
            AppContext.TryGetSwitch("Nanook.GrindCore.UseStrictValidation", out bool strictValidation) ? strictValidation : false;
    }
}

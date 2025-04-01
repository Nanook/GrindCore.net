

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;

namespace Nanook.GrindCore.DeflateZLib
{
    public class DeflateStream : StreamBase
    {
        private const int DefaultBufferSize = 8192;

        private Stream _baseStream;
        private bool _compress;
        private bool _leaveOpen;
        private Inflater? _inflater;
        private Deflater? _deflater;
        private byte[] _buffer;
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

        //private static readonly bool s_useStrictValidation =
        //    AppContext.TryGetSwitch("Nanook.GrindCore.UseStrictValidation", out bool strictValidation) ? strictValidation : false;

        /// <summary>
        /// Internal constructor to check stream validity and call the correct initialization function depending on
        /// the value of the CompressionMode given.
        /// </summary>
        internal DeflateStream(Stream stream, CompressionType type, bool leaveOpen, int windowBits, CompressionVersion? version = null, long uncompressedSize = -1) : base(true)
        {
            if (stream is null)
                throw new ArgumentNullException(nameof(stream));

            if (version == null)
                version = CompressionVersion.ZLibNgLatest();

            _buffer = new byte[DefaultBufferSize];

            if (type == CompressionType.Decompress)
            {
                if (!stream.CanRead)
                    throw new ArgumentException(SR.NotSupported_UnreadableStream, nameof(stream));

                _inflater = new Inflater(version, windowBits, uncompressedSize);
                _baseStream = stream;
                _compress = false;
                _leaveOpen = leaveOpen;
            }
            else
            {
                if (stream is null)
                    throw new ArgumentNullException(nameof(stream));

                if (!stream!.CanWrite)
                    throw new ArgumentException(SR.NotSupported_UnwritableStream, nameof(stream));

                _deflater = new Deflater(version, type, windowBits);

                _baseStream = stream;
                _compress = true;
                _leaveOpen = leaveOpen;
            }
        }

        public override bool CanRead => _baseStream != null && !_compress && _baseStream.CanRead;
        public override bool CanWrite => _baseStream != null && _compress && _baseStream.CanWrite;
        public override bool CanSeek => false;
        public Stream BaseStream => _baseStream;


        internal override int OnRead(DataBlock dataBlock)
        {
            int bytesRead;
            while (true)
            {
                // Try to decompress any data from the inflater into the caller's buffer.
                // If we're able to decompress any bytes, or if decompression is completed, we're done.
                bytesRead = _inflater!.Inflate(dataBlock);
                if (bytesRead != 0 || InflatorIsFinished)
                {
                    break;
                }

                // We were unable to decompress any data.  If the inflater needs additional input
                // data to proceed, read some to populate it.
                if (_inflater.NeedsInput())
                {
                    int n = _baseStream.Read(_buffer!, 0, _buffer!.Length);
                    if (n <= 0)
                    {
                        // - Inflater didn't return any data although a non-empty output buffer was passed by the caller.
                        // - More input is needed but there is no more input available.
                        // - Inflation is not finished yet.
                        // - Provided input wasn't completely empty
                        // In such case, we are dealing with a truncated input stream.
                        if ( /*s_useStrictValidation &&*/ dataBlock.Length != 0 && !_inflater.Finished() && _inflater.NonEmptyInput())
                        {
                            throw new InvalidDataException(SR.TruncatedData);
                        }
                        break;
                    }
                    else if (n > _buffer.Length)
                    {
                        // The stream is either malicious or poorly implemented and returned a number of
                        // bytes < 0 || > than the buffer supplied to it.
                        throw new InvalidDataException(SR.GenericInvalidData);
                    }
                    else
                    {
                        _inflater.SetInput(_buffer, 0, n);
                    }
                }

                if (dataBlock.Length == 0)
                {
                    // The caller provided a zero-byte buffer.  This is typically done in order to avoid allocating/renting
                    // a buffer until data is known to be available.  We don't have perfect knowledge here, as _inflater.inflate
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

        internal override void OnWrite(DataBlock dataBlock)
        {
            Debug.Assert(_deflater != null);
            // Write compressed the bytes we already passed to the deflater:
            WriteDeflaterOutput();

            unsafe
            {
                // Pass new bytes through deflater and write them too:
                fixed (byte* bufferPtr = dataBlock.Data)
                {
                    if (dataBlock.Length != 0)
                        _deflater!.SetInput(bufferPtr, dataBlock.Length);
                    WriteDeflaterOutput();
                    _wroteBytes = true;
                }
            }
        }

        internal override void OnFlush()
        {
            if (_compress)
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
                            _baseStream.Write(_buffer, 0, compressedBytes);
                        }
                        Debug.Assert(flushSuccessful == compressedBytes > 0);
                    } while (flushSuccessful);
                }

                // Always flush on the underlying stream
                _baseStream.Flush();
            }
        }

        protected override void OnDispose()
        {
            try //purge
            {
                if (_baseStream == null || !_compress)
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
                            _baseStream.Write(_buffer, 0, compressedBytes);
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
            finally
            {
                // Close the underlying stream even if PurgeBuffers threw.
                // Stream.Close() may throw here (may or may not be due to the same error).
                // In this case, we still need to clean up internal resources, hence the inner finally blocks.
                try
                {
                    if (!_leaveOpen)
                        _baseStream?.Dispose();
                }
                finally
                {
                    _baseStream = null!;

                    try
                    {
                        _deflater?.Dispose();
                        _inflater?.Dispose();
                    }
                    finally
                    {
                        _deflater = null;
                        _inflater = null;
                    }
                }
            }
        }

        private bool InflatorIsFinished =>
            // If the stream is finished then we have a few potential cases here:
            // 1. DeflateStream => return
            // 2. GZipStream that is finished but may have an additional GZipStream appended => feed more input
            // 3. GZipStream that is finished and appended with garbage => return
            _inflater!.Finished() &&
            (!_inflater.IsGzipStream() || !_inflater.NeedsInput());

        private void WriteDeflaterOutput()
        {
            Debug.Assert(_deflater != null && _buffer != null);
            while (!_deflater!.NeedsInput())
            {
                int compressedBytes = _deflater!.GetDeflateOutput(_buffer!);
                if (compressedBytes > 0)
                    _baseStream.Write(_buffer, 0, compressedBytes);
            }
        }
    }
}

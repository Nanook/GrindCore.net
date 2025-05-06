

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;

namespace Nanook.GrindCore.DeflateZLib
{
    public class DeflateStream : CompressionStream, ICompressionDefaults
    {
        private const int DefaultBufferSize = 8192;

        private Inflater? _inflater;
        private Deflater? _deflater;
        private CompressionBuffer _buffer;
        private bool _wroteBytes;
        private bool _flushed;

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.DeflateNg;
        internal override int DefaultProcessSizeMin => DefaultBufferSize;
        internal override int DefaultProcessSizeMax => 0x400 * 0x400;
        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level6;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxZLib;

        public DeflateStream(Stream stream, CompressionOptions options) : this(stream, options, Interop.ZLib.Deflate_DefaultWindowBits)
        {
        }

        /// <summary>
        /// Internal constructor to check stream validity and call the correct initialization function depending on
        /// the value of the CompressionMode given.
        /// </summary>
        internal DeflateStream(Stream stream, CompressionOptions options, int windowBits, long uncompressedSize = -1) : base(true, stream, options)
        {
            _buffer = new CompressionBuffer(options.InternalBufferSize ?? DefaultBufferSize);
            _flushed = false;

            if (!IsCompress)
                _inflater = new Inflater(base.Version, windowBits, uncompressedSize);
            else
                _deflater = new Deflater(base.Version, CompressionType, windowBits);
        }

        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            bytesReadFromStream = 0;
            int bytesRead;
            while (true)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                // Try to decompress any data from the inflater into the caller's buffer.
                // If we're able to decompress any bytes, or if decompression is completed, we're done.
                bytesRead = _inflater!.Inflate(data);
                if (bytesRead != 0 || inflatorIsFinished)
                    break;

                // We were unable to decompress any data.  If the inflater needs additional input
                // data to proceed, read some to populate it.
                if (_inflater.NeedsInput())
                {
                    int n = BaseStream.Read(_buffer.Data, 0, _buffer.AvailableWrite);
                    if (n <= 0)
                    {
                        // - Inflater didn't return any data although a non-empty output buffer was passed by the caller.
                        // - More input is needed but there is no more input available.
                        // - Inflation is not finished yet.
                        // - Provided input wasn't completely empty
                        // In such case, we are dealing with a truncated input stream.
                        if ( /*s_useStrictValidation &&*/ data.AvailableWrite != 0 && !_inflater.Finished() && _inflater.NonEmptyInput())
                            throw new InvalidDataException(SR.TruncatedData);
                        break;
                    }
                    else if (n > _buffer.AvailableWrite) // The stream is either malicious or poorly implemented and returned a number of - bytes < 0 || > than the buffer supplied to it.
                        throw new InvalidDataException(SR.GenericInvalidData);


                    _buffer.Write(n); //update
                    _inflater.SetInput(_buffer);
                    bytesReadFromStream += n;
                }

                if (data.AvailableWrite == 0)
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

        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            Debug.Assert(_deflater != null);

            bytesWrittenToStream = 0;

            // Write compressed the bytes we already passed to the deflater:
            int compressedBytes;
            writeDeflaterOutput(cancel, out compressedBytes);
            bytesWrittenToStream += compressedBytes;

            unsafe
            {
                // Pass new bytes through deflater and write them too:
                fixed (byte* bufferPtr = data.Data)
                {
                    if (data.AvailableRead != 0)
                    {
                        *&bufferPtr += data.Pos;
                        _deflater!.SetInput(bufferPtr, data.AvailableRead);
                        data.Read(data.AvailableRead); //assume read at this point
                    }
                    writeDeflaterOutput(cancel, out compressedBytes);
                    bytesWrittenToStream += compressedBytes;
                    _wroteBytes = true;
                }
            }
        }

        internal override void OnFlush(CancellableTask cancel, out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            if (IsCompress)
            {
                if (_wroteBytes && !_flushed)
                {
                    int compressedBytes;
                    // Process any bytes left:
                    writeDeflaterOutput(cancel, out compressedBytes);
                    bytesWrittenToStream += compressedBytes;

                    Debug.Assert(_deflater != null && _buffer != null);
                    // Pull out any bytes left inside deflater:
                    bool flushSuccessful;
                    do
                    {
                        flushSuccessful = _deflater!.Flush(_buffer!, out compressedBytes);
                        if (flushSuccessful)
                        {
                            BaseStream.Write(_buffer.Data, 0, compressedBytes);
                            _buffer.Read(compressedBytes);
                            bytesWrittenToStream += compressedBytes;
                        }
                        Debug.Assert(flushSuccessful == compressedBytes > 0);
                    } while (flushSuccessful);
                    _flushed = true;
                }
            }
        }

        protected override void OnDispose(out int bytesWrittenToStream)
        {
            try //purge
            {
                bytesWrittenToStream = 0;

                if (BaseStream == null || !IsCompress)
                    return;

                Debug.Assert(_deflater != null && _buffer != null);
                // Some deflaters (e.g. ZLib) write more than zero bytes for zero byte inputs.
                // This round-trips and we should be ok with this, but our legacy managed deflater
                // always wrote zero output for zero input and upstack code (e.g. ZipArchiveEntry)
                // took dependencies on it. Thus, make sure to only "flush" when we actually had
                // some input:
                if (_wroteBytes)
                {
                    // Process any bytes left
                    try
                    {
                        if (!_flushed)
                            writeDeflaterOutput(new CancellableTask(), out bytesWrittenToStream);

                        // Pull out any bytes left inside deflater:
                        bool finished;
                        do
                        {
                            int compressedBytes;
                            finished = _deflater!.Finish(_buffer!, out compressedBytes);

                            if (compressedBytes > 0)
                            {
                                BaseStream.Write(_buffer.Data, _buffer.Pos, compressedBytes);
                                _buffer.Read(compressedBytes);
                            }
                        } while (!finished);
                    }
                    catch { }
                }
                else
                {
                    // In case of zero length buffer, we still need to clean up the native created stream before
                    // the object get _disposed because eventually ZLibNative.ReleaseHandle will get called during
                    // the dispose operation and although it frees the stream but it return error code because the
                    // stream state was still marked as in use. The symptoms of this problem will not be seen except
                    // if running any diagnostic tools which check for disposing safe handle objects
                    bool finished;
                    try
                    {
                        do
                        {
                            finished = _deflater!.Finish(_buffer!, out _);
                        } while (!finished);
                    }
                    catch { }
                }

            }
            finally
            {
                // Close the underlying stream even if PurgeBuffers threw.
                // Stream.Close() may throw here (may or may not be due to the same error).
                // In this case, we still need to clean up internal resources, hence the inner finally blocks.
                try { _deflater?.Dispose(); } catch { }
                try { _inflater?.Dispose(); } catch { }
                try { _buffer?.Dispose(); } catch { }
                _deflater = null;
                _inflater = null;
            }
        }

        private bool inflatorIsFinished =>
            // If the stream is finished then we have a few potential cases here:
            // 1. DeflateStream => return
            // 2. GZipStream that is finished but may have an additional GZipStream appended => feed more input
            // 3. GZipStream that is finished and appended with garbage => return
            _inflater!.Finished() &&
            (!_inflater.IsGzipStream() || !_inflater.NeedsInput());

        private void writeDeflaterOutput(CancellableTask cancel, out int bytesWritten)
        {
            Debug.Assert(_deflater != null && _buffer != null);
            bytesWritten = 0;
            while (!_deflater!.NeedsInput())
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                int compressedBytes = _deflater!.GetDeflateOutput(_buffer!);
                if (compressedBytes > 0)
                {
                    BaseStream.Write(_buffer.Data, 0, compressedBytes);
                    _buffer.Read(compressedBytes);
                    bytesWritten += compressedBytes;
                }
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// Provides a stream for Deflate (and ZLib/DeflateNg) compression and decompression, supporting both block and stream operations.
    /// </summary>
    public class DeflateStream : CompressionStream
    {
        private DeflateDecoder? _inflater;
        private DeflateEncoder? _deflater;
        private CompressionBuffer _buffer;
        private bool _wroteBytes;

        /// <summary>
        /// Gets the input buffer size for Deflate operations.
        /// </summary>
        internal override int BufferSizeInput => 0x200000;

        /// <summary>
        /// Gets the output buffer size for Deflate operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Gets the number of bytes buffered internally by the ZLib inflater/deflater engine.
        /// Returns the available input bytes remaining in the native ZLib stream that have been read from the base stream
        /// but not yet processed by the compression engine. Essential for accurate stream position correction when 
        /// GrindCore overreads to fill buffers, allowing precise calculation of unused bytes for stream rewinding.
        /// </summary>
        protected override int InternalBufferedBytes => (_inflater?.AvailableInput ?? _deflater?.AvailableInput ?? 0);

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflateStream"/> class with the specified stream and options, using the default window bits.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        public DeflateStream(Stream stream, CompressionOptions options) : this(stream, CompressionAlgorithm.DeflateNg, options, Interop.ZLib.Deflate_DefaultWindowBits)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflateStream"/> class with the specified stream, options, window bits, and optional uncompressed size.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="defaultAlgorithm">The default algorithm, used when options.Version is not set to override it.</param>
        /// <param name="options">The compression options to use.</param>
        /// <param name="windowBits">The window bits to use for Deflate.</param>
        /// <param name="uncompressedSize">The expected uncompressed size, or -1 if unknown.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        internal DeflateStream(Stream stream, CompressionAlgorithm defaultAlgorithm, CompressionOptions options, int windowBits, long uncompressedSize = -1) : base(true, stream, defaultAlgorithm, options)
        {
            if (!IsCompress)
            {
                this.BufferSizeOutput = 8192;
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
                _inflater = new DeflateDecoder(base.Version, windowBits, uncompressedSize);
            }
            else
            {
                this.BufferSizeOutput = this.BufferThreshold;
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
                _deflater = new DeflateEncoder(base.Version, CompressionType, windowBits);
            }
        }

        /// <summary>
        /// Reads and decompresses data from the underlying stream into the provided buffer.
        /// </summary>
        /// <param name="data">The buffer to read decompressed data into.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesReadFromStream">The number of bytes read from the underlying stream.</param>
        /// <param name="length"> The maximum number of bytes to read.If 0, the method will fill the buffer if possible.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        /// <exception cref="InvalidDataException">Thrown if the input stream is truncated or contains invalid data.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream, int length = 0)
        {
            bytesReadFromStream = 0;
            int bytesRead;
            while (true)
            {
                cancel.ThrowIfCancellationRequested();

                // Try to decompress any data from the inflater into the caller's buffer.
                // If we're able to decompress any bytes, or if decompression is completed, we're done.
                bytesRead = _inflater!.DecodeData(data, length);
                if (bytesRead != 0 || inflatorIsFinished)
                    break;

                // We were unable to decompress any data. If the inflater needs additional input data to proceed, read some to populate it.
                if (_inflater.NeedsInput()) //no data left
                {
                    int available = _buffer.AvailableWrite;
                    int n = BaseRead(_buffer, _buffer.AvailableWrite);
                    if (n <= 0)
                    {
                        // - Inflater didn't return any data although a non-empty output buffer was passed by the caller.
                        // - More input is needed but there is no more input available.
                        // - Inflation is not finished yet.
                        // - Provided input wasn't completely empty
                        // In such case, we are dealing with a truncated input stream.
                        if (/*s_useStrictValidation &&*/ available != 0 && !_inflater.Finished() && _inflater.NonEmptyInput())
                            throw new InvalidDataException(SR.TruncatedData);
                        break;
                    }
                    else if (n > available)
                        throw new InvalidDataException(SR.GenericInvalidData);

                    _inflater.SetInput(_buffer);
                    bytesReadFromStream += n;
                }

                if (data.AvailableWrite == 0)
                {
                    // The caller provided a zero-byte output buffer. This is typically done in order to avoid allocating/renting
                    // a buffer until data is known to be available. We don't have perfect knowledge here, as _inflater.DecodeData
                    // will return 0 whether or not more data is required, and having input data doesn't necessarily mean it'll
                    // decompress into at least one byte of output, but it's a reasonable approximation for the 99% case. If it's
                    // wrong, it just means that a caller using zero-byte reads as a way to delay getting a buffer to use for a
                    // subsequent call may end up getting one earlier than otherwise preferred.
                    Debug.Assert(bytesRead == 0);
                    break;
                }
            }

            return bytesRead;
        }

        /// <summary>
        /// Compresses and writes data from the buffer to the underlying stream.
        /// </summary>
        /// <param name="outData">The buffer containing data to compress and write.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnWrite(CompressionBuffer outData, CancellableTask cancel, out int bytesWrittenToStream)
        {
            Debug.Assert(_deflater != null);

            bytesWrittenToStream = 0;

            // Write compressed bytes we already passed to the deflater:
            int compressedBytes;
            writeDeflaterOutput(cancel, out compressedBytes);
            bytesWrittenToStream += compressedBytes;

            if (outData.AvailableRead == 0)
                outData.Tidy();

            unsafe
            {
                // Pass new bytes through deflater and write them too:
                fixed (byte* bufferPtr = outData.Data)
                {
                    if (outData.AvailableRead != 0)
                    {
                        *&bufferPtr += outData.Pos;
                        _deflater!.SetInput(bufferPtr, outData.AvailableRead);
                        outData.Read(outData.AvailableRead);
                    }
                    writeDeflaterOutput(cancel, out compressedBytes);
                    bytesWrittenToStream += compressedBytes;
                    _wroteBytes = true;
                }
            }
        }

        /// <summary>
        /// Flushes any remaining compressed data to the underlying stream.
        /// </summary>
        /// <param name="outData">The buffer containing data to flush.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        internal override void OnFlush(CompressionBuffer outData, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;
            if (IsCompress)
            {
                cancel.ThrowIfCancellationRequested();
                OnWrite(outData, cancel, out bytesWrittenToStream);

                // Some deflaters (e.g. ZLib) write more than zero bytes for zero byte inputs.
                // This round-trips and we should be ok with this, but our legacy managed deflater
                // always wrote zero output for zero input and upstack code (e.g. ZipArchiveEntry)
                // took dependencies on it. Thus, make sure to only "flush" when we actually had some input.

                // In case of zero length output buffer, we still need to clean up the native created stream before
                // the object gets disposed because eventually ZLibNative.ReleaseHandle will get called during
                // the dispose operation and although it frees the stream, it returns an error code because the
                // stream state was still marked as in use. The symptoms of this problem will not be seen except
                // if running any diagnostic tools which check for disposing safe handle objects

                if (flush && _wroteBytes)
                {
                    int compressedBytes;
                    // Process any bytes left:
                    writeDeflaterOutput(cancel, out compressedBytes);
                    bytesWrittenToStream += compressedBytes;

                    Debug.Assert(_deflater != null);
                    // Pull out any bytes left inside deflater:
                    bool flushSuccessful;
                    do
                    {
                        cancel.ThrowIfCancellationRequested();
                        flushSuccessful = _deflater!.Flush(_buffer!, out compressedBytes);
                        if (flushSuccessful)
                        {
                            BaseWrite(_buffer, compressedBytes);
                            bytesWrittenToStream += compressedBytes;
                        }
                        Debug.Assert(flushSuccessful == compressedBytes > 0);
                    } while (flushSuccessful);
                }

                if (complete)
                {
                    // Process any bytes left
                    try
                    {
                        // Pull out any bytes left inside deflater:
                        bool finished;
                        do
                        {
                            finished = _deflater!.Finish(_buffer!, out int compressedBytes);

                            if (_wroteBytes && compressedBytes > 0)
                            {
                                BaseWrite(_buffer, compressedBytes);
                                bytesWrittenToStream += compressedBytes;
                            }
                        } while (!finished);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="DeflateStream"/>.
        /// </summary>
        protected override void OnDispose()
        {
            try { _deflater?.Dispose(); } catch { }
            try { _inflater?.Dispose(); } catch { }
            try { _buffer?.Dispose(); } catch { }
            _deflater = null;
            _inflater = null;
        }

        /// <summary>
        /// Returns true if the inflator has finished processing the stream.
        /// </summary>
        private bool inflatorIsFinished =>
            _inflater!.Finished() &&
            (!_inflater.IsGzipStream() || !_inflater.NeedsInput());

        /// <summary>
        /// Writes any available compressed data from the deflater to the underlying stream.
        /// </summary>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWritten">The number of bytes written to the underlying stream.</param>
        private void writeDeflaterOutput(CancellableTask cancel, out int bytesWritten)
        {
            Debug.Assert(_deflater != null);
            bytesWritten = 0;
            while (!_deflater!.NeedsInput())
            {
                cancel.ThrowIfCancellationRequested();

                int compressedBytes = _deflater!.EncodeData(_buffer!);
                if (compressedBytes > 0)
                {
                    BaseWrite(_buffer, compressedBytes);
                    bytesWritten += compressedBytes;
                }
            }
        }
    }
}

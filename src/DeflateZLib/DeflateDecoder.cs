using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security;
using System.IO;
using System;

using ZErrorCode = Nanook.GrindCore.Interop.ZLib.ErrorCode;
using ZFlushCode = Nanook.GrindCore.Interop.ZLib.FlushCode;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// Provides a wrapper around the ZLib decompression API for block and stream decompression.
    /// </summary>
    internal sealed class DeflateDecoder : IDisposable
    {
        private const int MinWindowBits = -15; // WindowBits must be between -8..-15 to ignore the header, 8..15 for zlib headers, 24..31 for GZip headers, or 40..47 for either Zlib or GZip
        private const int MaxWindowBits = 47;

        private bool _nonEmptyInput;                        // Whether there is any non empty input
        private bool _finished;                             // Whether the end of the stream has been reached
        private bool _isDisposed;                           // Prevents multiple disposals
        private readonly int _windowBits;                   // The WindowBits parameter passed to Inflater construction
        private ZLibNative.ZLibStreamHandle _zlibStream;    // The handle to the primary underlying zlib stream
        private GCHandle _inputBufferHandle;                // The handle to the buffer that provides input to _zlibStream
        private readonly long _uncompressedSize;
        private long _currentInflatedCount;
        private CompressionVersion _version;

        private object SyncLock => this;                    // Used to make writing to unmanaged structures atomic

        /// <summary>
        /// Initializes the DeflateDecoder with the given windowBits size and optional uncompressed size.
        /// </summary>
        /// <param name="version">The compression version to use.</param>
        /// <param name="windowBits">The window bits parameter for the inflater.</param>
        /// <param name="uncompressedSize">The expected uncompressed size, or -1 if unknown.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="windowBits"/> is out of range.</exception>
        /// <exception cref="ZLibException">Thrown if the underlying zlib stream cannot be created.</exception>
        internal DeflateDecoder(CompressionVersion version, int windowBits, long uncompressedSize = -1)
        {
            Debug.Assert(windowBits >= MinWindowBits && windowBits <= MaxWindowBits);
            _version = version;
            _finished = false;
            _nonEmptyInput = false;
            _isDisposed = false;
            _windowBits = windowBits;
            inflateInit(windowBits);
            _uncompressedSize = uncompressedSize;
        }

        /// <summary>
        /// Gets the number of bytes available for output from the underlying zlib stream.
        /// </summary>
        public int AvailableOutput => (int)_zlibStream.AvailOut;

        /// <summary>
        /// Returns true if the end of the stream has been reached.
        /// </summary>
        public bool Finished() => _finished;

        /// <summary>
        /// Inflates a single byte from the stream.
        /// </summary>
        /// <param name="b">The output byte.</param>
        /// <returns>True if a byte was read; otherwise, false.</returns>
        public unsafe bool Inflate(out byte b)
        {
            fixed (byte* bufPtr = &b)
            {
                int bytesRead = inflateVerified(bufPtr, 1);
                Debug.Assert(bytesRead == 0 || bytesRead == 1);
                return bytesRead != 0;
            }
        }

        /// <summary>
        /// Inflates data from the stream into the provided <see cref="CompressionBuffer"/>.
        /// </summary>
        /// <param name="outData">The buffer to write decompressed data to.</param>
        /// <param name="length"> The maximum number of bytes to read.If 0, the method will fill the buffer if possible.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        public unsafe int Inflate(CompressionBuffer outData, int length)
        {
            if (outData.AvailableWrite == 0)
                return 0;

            if (length == 0 || length > outData.AvailableWrite)
                length = outData.AvailableWrite;
                
            int read;
            fixed (byte* bufPtr = outData.Data)
            {
                *&bufPtr += outData.Size; // Size is writePos
                read = inflateVerified(bufPtr, length);
            }
            outData.Write(read); // update
            return read;
        }

        /// <summary>
        /// Inflates data into the provided buffer, handling uncompressed size and releasing input buffer handles as needed.
        /// </summary>
        /// <param name="bufPtr">Pointer to the output buffer.</param>
        /// <param name="length">The number of bytes to inflate.</param>
        /// <returns>The number of bytes read.</returns>
        private unsafe int inflateVerified(byte* bufPtr, int length)
        {
            try
            {
                int bytesRead = 0;
                if (_uncompressedSize == -1)
                {
                    readOutput(bufPtr, length, out bytesRead);
                }
                else
                {
                    if (_uncompressedSize > _currentInflatedCount)
                    {
                        length = (int)Math.Min(length, _uncompressedSize - _currentInflatedCount);
                        readOutput(bufPtr, length, out bytesRead);
                        _currentInflatedCount += bytesRead;
                    }
                    else
                    {
                        _finished = true;
                        _zlibStream.AvailIn = 0;
                    }
                }
                return bytesRead;
            }
            finally
            {
                // Before returning, make sure to release input buffer if necessary:
                if (0 == _zlibStream.AvailIn && IsInputBufferHandleAllocated)
                    deallocateInputBufferHandle();
            }
        }

        /// <summary>
        /// Reads output from the zlib stream into the provided buffer.
        /// </summary>
        /// <param name="bufPtr">Pointer to the output buffer.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <param name="bytesRead">The number of bytes actually read.</param>
        private unsafe void readOutput(byte* bufPtr, int length, out int bytesRead)
        {
            if (readInflateOutput(bufPtr, length, ZFlushCode.NoFlush, out bytesRead) == ZErrorCode.StreamEnd)
            {
                if (!NeedsInput() && IsGzipStream() && IsInputBufferHandleAllocated)
                    _finished = resetStreamForLeftoverInput();
                else
                    _finished = true;
            }
        }

        /// <summary>
        /// If this stream has some input leftover that hasn't been processed, checks if it is another GZip file concatenated with this one.
        /// Returns false if the leftover input is another GZip data stream.
        /// </summary>
        private unsafe bool resetStreamForLeftoverInput()
        {
            Debug.Assert(!NeedsInput());
            Debug.Assert(IsGzipStream());
            Debug.Assert(IsInputBufferHandleAllocated);

            lock (SyncLock)
            {
                IntPtr nextInPtr = _zlibStream.NextIn;
                byte* nextInPointer = (byte*)nextInPtr.ToPointer();
                uint nextAvailIn = _zlibStream.AvailIn;

                // Check the leftover bytes to see if they start with the gzip header ID bytes
                if (*nextInPointer != Interop.ZLib.GZip_Header_ID1 || (nextAvailIn > 1 && *(nextInPointer + 1) != Interop.ZLib.GZip_Header_ID2))
                    return true;

                // Dispose the existing zstream.
                _zlibStream.Dispose();

                // Create a new zstream
                inflateInit(_windowBits);

                // SetInput on the new stream to the bits remaining from the last stream
                _zlibStream.NextIn = nextInPtr;
                _zlibStream.AvailIn = nextAvailIn;
                _finished = false;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the stream is using GZip headers.
        /// </summary>
        internal bool IsGzipStream() => _windowBits >= 24 && _windowBits <= 31;

        /// <summary>
        /// Returns true if the decoder needs more input data.
        /// </summary>
        public bool NeedsInput() => _zlibStream.AvailIn == 0;

        /// <summary>
        /// Returns true if any non-empty input has been provided.
        /// </summary>
        public bool NonEmptyInput() => _nonEmptyInput;

        /// <summary>
        /// Sets the input buffer for decompression.
        /// </summary>
        /// <param name="inputBuffer">The buffer containing input data.</param>
        /// <exception cref="ArgumentException">Thrown if input is not needed or a buffer handle is already allocated.</exception>
        public unsafe void SetInput(CompressionBuffer inputBuffer)
        {
            Debug.Assert(NeedsInput(), "We have something left in previous input!");
            Debug.Assert(!IsInputBufferHandleAllocated);

            if (inputBuffer.AvailableRead == 0)
                return;

            lock (SyncLock)
            {
                _inputBufferHandle = GCHandle.Alloc(inputBuffer.Data, GCHandleType.Pinned);
                _zlibStream.NextIn = _inputBufferHandle.AddrOfPinnedObject();
                _zlibStream.AvailIn = (uint)inputBuffer.AvailableRead;
                _finished = false;
                _nonEmptyInput = true;
            }
            inputBuffer.Read(inputBuffer.AvailableRead);
        }

        /// <summary>
        /// Disposes the decoder and releases all resources.
        /// </summary>
        public void Dispose()
        {
            dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Finalizer to ensure resources are released.
        /// </summary>
        ~DeflateDecoder()
        {
            dispose(false);
        }

        /// <summary>
        /// Disposes the decoder, optionally releasing managed resources.
        /// </summary>
        /// <param name="disposing">True to release managed resources; otherwise, false.</param>
        private void dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                    _zlibStream.Dispose();

                if (IsInputBufferHandleAllocated)
                    deallocateInputBufferHandle();

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Creates the ZStream that will handle inflation.
        /// </summary>
        /// <param name="windowBits">The window bits parameter for the inflater.</param>
        /// <exception cref="ZLibException">Thrown if the underlying zlib stream cannot be created or initialized.</exception>
        private void inflateInit(int windowBits)
        {
            ZErrorCode error;
            try
            {
                error = ZLibNative.CreateZLibStreamForInflate(out _zlibStream, windowBits, _version);
            }
            catch (Exception exception) // could not load the ZLib dll
            {
                throw new ZLibException(SR.ZLibErrorDLLLoadError, exception);
            }

            switch (error)
            {
                case ZErrorCode.Ok:           // Successful initialization
                    return;

                case ZErrorCode.MemError:     // Not enough memory
                    throw new ZLibException(SR.ZLibErrorNotEnoughMemory, "inflateInit2_", (int)error, _zlibStream.GetErrorMessage());

                case ZErrorCode.VersionError: // zlib library is incompatible with the version assumed
                    throw new ZLibException(SR.ZLibErrorVersionMismatch, "inflateInit2_", (int)error, _zlibStream.GetErrorMessage());

                case ZErrorCode.StreamError:  // Parameters are invalid
                    throw new ZLibException(SR.ZLibErrorIncorrectInitParameters, "inflateInit2_", (int)error, _zlibStream.GetErrorMessage());

                default:
                    throw new ZLibException(SR.ZLibErrorUnexpected, "inflateInit2_", (int)error, _zlibStream.GetErrorMessage());
            }
        }

        /// <summary>
        /// Wrapper around the ZLib inflate function, configuring the stream appropriately.
        /// </summary>
        /// <param name="bufPtr">Pointer to the output buffer.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <param name="flushCode">The flush code to use.</param>
        /// <param name="bytesRead">The number of bytes actually read.</param>
        /// <returns>The error code returned by the inflate operation.</returns>
        private unsafe ZErrorCode readInflateOutput(byte* bufPtr, int length, ZFlushCode flushCode, out int bytesRead)
        {
            lock (SyncLock)
            {
                _zlibStream.NextOut = (nint)bufPtr;
                _zlibStream.AvailOut = (uint)length;

                ZErrorCode errC = inflate(flushCode);
                bytesRead = length - (int)_zlibStream.AvailOut;

                return errC;
            }
        }

        /// <summary>
        /// Wrapper around the ZLib inflate function.
        /// </summary>
        /// <param name="flushCode">The flush code to use.</param>
        /// <returns>The error code returned by the inflate operation.</returns>
        /// <exception cref="ZLibException">Thrown if a fatal error occurs in the underlying zlib library.</exception>
        /// <exception cref="InvalidDataException">Thrown if the input data is corrupted or invalid.</exception>
        private ZErrorCode inflate(ZFlushCode flushCode)
        {
            ZErrorCode errC;
            try
            {
                errC = _zlibStream.Inflate(flushCode);
            }
            catch (Exception cause) // could not load the Zlib DLL correctly
            {
                throw new ZLibException(SR.ZLibErrorDLLLoadError, cause);
            }
            switch (errC)
            {
                case ZErrorCode.Ok:           // progress has been made inflating
                case ZErrorCode.StreamEnd:    // The end of the input stream has been reached
                    return errC;

                case ZErrorCode.BufError:     // No room in the output buffer - inflate() can be called again with more space to continue
                    return errC;

                case ZErrorCode.MemError:     // Not enough memory to complete the operation
                    throw new ZLibException(SR.ZLibErrorNotEnoughMemory, "inflate_", (int)errC, _zlibStream.GetErrorMessage());

                case ZErrorCode.DataError:    // The input data was corrupted (input stream not conforming to the zlib format or incorrect check value)
                    throw new InvalidDataException(SR.UnsupportedCompression);

                case ZErrorCode.StreamError:  // The stream structure was inconsistent (e.g., if next_in or next_out was NULL)
                    throw new ZLibException(SR.ZLibErrorInconsistentStream, "inflate_", (int)errC, _zlibStream.GetErrorMessage());

                default:
                    throw new ZLibException(SR.ZLibErrorUnexpected, "inflate_", (int)errC, _zlibStream.GetErrorMessage());
            }
        }

        /// <summary>
        /// Frees the GCHandle being used to store the input buffer.
        /// </summary>
        private void deallocateInputBufferHandle()
        {
            Debug.Assert(IsInputBufferHandleAllocated);

            lock (SyncLock)
            {
                _zlibStream.AvailIn = 0;
                _zlibStream.NextIn = Interop.ZLib.ZNullPtr;
                _inputBufferHandle.Free();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the input buffer handle is currently allocated.
        /// </summary>
        private unsafe bool IsInputBufferHandleAllocated => _inputBufferHandle.IsAllocated;
    }
}

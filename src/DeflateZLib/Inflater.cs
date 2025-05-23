


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
    /// Provides a wrapper around the ZLib decompression API.
    /// </summary>
    internal sealed class Inflater : IDisposable
    {
        private const int MinWindowBits = -15;              // WindowBits must be between -8..-15 to ignore the header, 8..15 for
        private const int MaxWindowBits = 47;               // zlib headers, 24..31 for GZip headers, or 40..47 for either Zlib or GZip

        private bool _nonEmptyInput;                        // Whether there is any non empty input
        private bool _finished;                             // Whether the end of the stream has been reached
        private bool _isDisposed;                           // Prevents multiple disposals
        private readonly int _windowBits;                   // The WindowBits parameter passed to Inflater construction
        private ZLibNative.ZLibStreamHandle _zlibStream;    // The handle to the primary underlying zlib stream
        private GCHandle _inputBufferHandle;            // The handle to the buffer that provides input to _zlibStream
        private readonly long _uncompressedSize;
        private long _currentInflatedCount;
        private CompressionVersion _version;

        private object SyncLock => this;                    // Used to make writing to unmanaged structures atomic

        /// <summary>
        /// Initialized the Inflater with the given windowBits size
        /// </summary>
        internal Inflater(CompressionVersion version, int windowBits, long uncompressedSize = -1)
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

        public int AvailableOutput => (int)_zlibStream.AvailOut;

        /// <summary>
        /// Returns true if the end of the stream has been reached.
        /// </summary>
        public bool Finished() => _finished;

        public unsafe bool Inflate(out byte b)
        {
            fixed (byte* bufPtr = &b)
            {
                int bytesRead = inflateVerified(bufPtr, 1);
                Debug.Assert(bytesRead == 0 || bytesRead == 1);
                return bytesRead != 0;
            }
        }

        //public unsafe int inflate(byte[] bytes, int offset, int length)
        //{
        //    // If inflate is called on an invalid or unready inflater, return 0 to indicate no bytes have been read.
        //    if (length == 0)
        //        return 0;

        //    Debug.Assert(null != bytes, "Can't pass in a null output buffer!");
        //    fixed (byte* bufPtr = bytes)
        //    {
        //        return inflateVerified(bufPtr + offset, length);
        //    }
        //}

        public unsafe int Inflate(CompressionBuffer destination)
        {
            // If inflate is called on an invalid or unready inflater, return 0 to indicate no bytes have been read.
            if (destination.AvailableWrite == 0)
                return 0;

            int read;
            //fixed (byte* bufPtr = &MemoryMarshal.GetReference(destination))
            fixed (byte* bufPtr = destination.Data)
            {
                *&bufPtr += destination.Size; //Size is writePos
                read = inflateVerified(bufPtr, destination.AvailableWrite);
            }
            destination.Write(read); //update
            return read;
        }

        private unsafe int inflateVerified(byte* bufPtr, int length)
        {
            // State is valid; attempt inflation
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
        /// If this stream has some input leftover that hasn't been processed then we should
        /// check if it is another GZip file concatenated with this one.
        ///
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

                // Check the leftover bytes to see if they start with he gzip header ID bytes
                if (*nextInPointer != Interop.ZLib.GZip_Header_ID1 || nextAvailIn > 1 && *(nextInPointer + 1) != Interop.ZLib.GZip_Header_ID2)
                    return true;

                // Trash our existing zstream.
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

        internal bool IsGzipStream() => _windowBits >= 24 && _windowBits <= 31;

        public bool NeedsInput() => _zlibStream.AvailIn == 0;

        public bool NonEmptyInput() => _nonEmptyInput;

        //public void SetInput(byte[] inputBuffer, int startIndex, int count)
        //{
        //    Debug.Assert(NeedsInput(), "We have something left in previous input!");
        //    Debug.Assert(inputBuffer != null);
        //    Debug.Assert(startIndex >= 0 && count >= 0 && count + startIndex <= inputBuffer!.Length);
        //    Debug.Assert(!IsInputBufferHandleAllocated);

        //    SetInput(inputBuffer.AsMemory(startIndex, count));
        //}

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

        public void Dispose()
        {
            dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Inflater()
        {
            dispose(false);
        }

        /// <summary>
        /// Creates the ZStream that will handle inflation.
        /// </summary>
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

                case ZErrorCode.VersionError: //zlib library is incompatible with the version assumed
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
        /// Wrapper around the ZLib inflate function
        /// </summary>
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

                case ZErrorCode.StreamError:  //the stream structure was inconsistent (for example if next_in or next_out was NULL),
                    throw new ZLibException(SR.ZLibErrorInconsistentStream, "inflate_", (int)errC, _zlibStream.GetErrorMessage());

                default:
                    throw new ZLibException(SR.ZLibErrorUnexpected, "inflate_", (int)errC, _zlibStream.GetErrorMessage());
            }
        }

        /// <summary>
        /// Frees the GCHandle being used to store the input buffer
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

        private unsafe bool IsInputBufferHandleAllocated => _inputBufferHandle.IsAllocated;
    }
}

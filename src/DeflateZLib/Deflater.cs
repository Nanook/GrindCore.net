


using System.Diagnostics;
using System.Security;
using System;

using ZErrorCode = Nanook.GrindCore.Interop.ZLib.ErrorCode;
using ZFlushCode = Nanook.GrindCore.Interop.ZLib.FlushCode;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// Provides a wrapper around the ZLib compression API.
    /// </summary>
    internal sealed class Deflater : IDisposable
    {
        private readonly ZLibNative.ZLibStreamHandle _zlibStream;
        private bool _isDisposed;
        private const int minWindowBits = -15;  // WindowBits must be between -8..-15 to write no header, 8..15 for a
        private const int maxWindowBits = 31;   // zlib header, or 24..31 for a GZip header
        private CompressionVersion _version;

        // Note, DeflateStream or the deflater do not try to be thread safe.
        // The lock is just used to make writing to unmanaged structures atomic to make sure
        // that they do not get inconsistent fields that may lead to an unmanaged memory violation.
        // To prevent *managed* buffer corruption or other weird behaviour users need to synchronise
        // on the stream explicitly.
        private object SyncLock => this;

        internal Deflater(CompressionVersion version, CompressionType compressionLevel, int windowBits)
        {
            Debug.Assert(windowBits >= minWindowBits && windowBits <= maxWindowBits);
            _version = version;
            Interop.ZLib.CompressionLevel zlibCompressionLevel;
            int memLevel = Interop.ZLib.Deflate_DefaultMemLevel;

            switch (compressionLevel)
            {
                // See the note in ZLibNative.CompressionLevel for the recommended combinations.

                case CompressionType.Optimal:
                    zlibCompressionLevel = Interop.ZLib.CompressionLevel.DefaultCompression;
                    break;

                case CompressionType.Fastest:
                    zlibCompressionLevel = Interop.ZLib.CompressionLevel.BestSpeed;
                    break;

                case CompressionType.NoCompression:
                    zlibCompressionLevel = Interop.ZLib.CompressionLevel.NoCompression;
                    memLevel = Interop.ZLib.Deflate_NoCompressionMemLevel;
                    break;

                case CompressionType.SmallestSize:
                    zlibCompressionLevel = Interop.ZLib.CompressionLevel.BestCompression;
                    break;

                default:
                    zlibCompressionLevel = (Interop.ZLib.CompressionLevel)compressionLevel; // raw level int
                    break;
            }

            Interop.ZLib.CompressionStrategy strategy = Interop.ZLib.CompressionStrategy.DefaultStrategy;

            ZErrorCode errC;
            try
            {
                errC = ZLibNative.CreateZLibStreamForDeflate(out _zlibStream, zlibCompressionLevel,
                                                             windowBits, memLevel, strategy, _version);
            }
            catch (Exception cause)
            {
                throw new ZLibException(SR.ZLibErrorDLLLoadError, cause);
            }

            switch (errC)
            {
                case ZErrorCode.Ok:
                    return;

                case ZErrorCode.MemError:
                    throw new ZLibException(SR.ZLibErrorNotEnoughMemory, "deflateInit2_", (int)errC, _zlibStream.GetErrorMessage());

                case ZErrorCode.VersionError:
                    throw new ZLibException(SR.ZLibErrorVersionMismatch, "deflateInit2_", (int)errC, _zlibStream.GetErrorMessage());

                case ZErrorCode.StreamError:
                    throw new ZLibException(SR.ZLibErrorIncorrectInitParameters, "deflateInit2_", (int)errC, _zlibStream.GetErrorMessage());

                default:
                    throw new ZLibException(SR.ZLibErrorUnexpected, "deflateInit2_", (int)errC, _zlibStream.GetErrorMessage());
            }
        }

        ~Deflater()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                    _zlibStream.Dispose();

                _isDisposed = true;
            }
        }

        public bool NeedsInput() => 0 == _zlibStream.AvailIn;

        internal unsafe void SetInput(byte* inputBufferPtr, int count)
        {
            Debug.Assert(NeedsInput(), "We have something left in previous input!");
            Debug.Assert(inputBufferPtr != null);

            if (count == 0)
                return;

            lock (SyncLock)
            {
                _zlibStream.NextIn = (nint)inputBufferPtr;
                _zlibStream.AvailIn = (uint)count;
            }
        }

        internal int GetDeflateOutput(CompressionBuffer outputBuffer)
        {
            Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
            Debug.Assert(!NeedsInput(), "GetDeflateOutput should only be called after providing input");

            int bytesRead;
            ReadDeflateOutput(outputBuffer!, ZFlushCode.NoFlush, out bytesRead);
            return bytesRead;

        }

        private unsafe ZErrorCode ReadDeflateOutput(CompressionBuffer outputBuffer, ZFlushCode flushCode, out int bytesRead)
        {
            lock (SyncLock)
            {
                fixed (byte* bufPtr = &outputBuffer.Data[0])
                {
                    _zlibStream.NextOut = (nint)bufPtr;
                    _zlibStream.AvailOut = (uint)outputBuffer.AvailableWrite;

                    ZErrorCode errC = Deflate(flushCode);
                    bytesRead = outputBuffer.AvailableWrite - (int)_zlibStream.AvailOut;

                    outputBuffer.Write(bytesRead);
                    return errC;
                }
            }
        }

        internal bool Finish(CompressionBuffer outputBuffer, out int bytesRead)
        {
            Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
            Debug.Assert(outputBuffer.AvailableWrite > 0, "Can't pass in an empty output buffer!");

            ZErrorCode errC = ReadDeflateOutput(outputBuffer, ZFlushCode.Finish, out bytesRead);
            return errC == ZErrorCode.StreamEnd;
        }

        /// <summary>
        /// Returns true if there was something to flush. Otherwise False.
        /// </summary>
        internal bool Flush(CompressionBuffer outputBuffer, out int bytesRead)
        {
            Debug.Assert(null != outputBuffer, "Can't pass in a null output buffer!");
            Debug.Assert(outputBuffer.AvailableWrite > 0, "Can't pass in an empty output buffer!");
            Debug.Assert(NeedsInput(), "We have something left in previous input!");


            // Note: we require that NeedsInput() == true, i.e. that 0 == _zlibStream.AvailIn.
            // If there is still input left we should never be getting here; instead we
            // should be calling GetDeflateOutput.

            return ReadDeflateOutput(outputBuffer, ZFlushCode.SyncFlush, out bytesRead) == ZErrorCode.Ok;
        }

        private void DeallocateInputBufferHandle()
        {
            lock (SyncLock)
            {
                _zlibStream.AvailIn = 0;
                _zlibStream.NextIn = Interop.ZLib.ZNullPtr;
            }
        }

        private ZErrorCode Deflate(ZFlushCode flushCode)
        {
            ZErrorCode errC;
            try
            {
                errC = _zlibStream.Deflate(flushCode);
            }
            catch (Exception cause)
            {
                throw new ZLibException(SR.ZLibErrorDLLLoadError, cause);
            }

            switch (errC)
            {
                case ZErrorCode.Ok:
                case ZErrorCode.StreamEnd:
                    return errC;

                case ZErrorCode.BufError:
                    return errC;  // This is a recoverable error

                case ZErrorCode.StreamError:
                    throw new ZLibException(SR.ZLibErrorInconsistentStream, "deflate", (int)errC, _zlibStream.GetErrorMessage());

                default:
                    throw new ZLibException(SR.ZLibErrorUnexpected, "deflate", (int)errC, _zlibStream.GetErrorMessage());
            }
        }
    }
}

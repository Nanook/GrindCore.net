using System;
using System.Diagnostics;
using ZErrorCode = Nanook.GrindCore.Interop.ZLib.ErrorCode;
using ZFlushCode = Nanook.GrindCore.Interop.ZLib.FlushCode;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// Provides a wrapper around the ZLib compression API for block and stream compression.
    /// </summary>
    internal sealed class DeflateEncoder : IDisposable
    {
        private readonly ZLibNative.ZLibStreamHandle _zlibStream;
        private bool _isDisposed;
        private const int minWindowBits = -15;  // WindowBits must be between -8..-15 to write no header, 8..15 for a zlib header, or 24..31 for a GZip header
        private const int maxWindowBits = 31;
        private CompressionVersion _version;

        // Note: DeflateEncoder does not try to be thread safe.
        // The lock is just used to make writing to unmanaged structures atomic to avoid inconsistent fields that may lead to an unmanaged memory violation.
        // To prevent managed buffer corruption or other unexpected behavior, users must synchronize on the stream explicitly.
        private object SyncLock => this;

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflateEncoder"/> class with the specified version, compression level, and window bits.
        /// </summary>
        /// <param name="version">The compression version to use.</param>
        /// <param name="compressionLevel">The compression level to use.</param>
        /// <param name="windowBits">The window bits parameter for the deflater.</param>
        /// <exception cref="ZLibException">Thrown if the underlying zlib stream cannot be created or initialized.</exception>
        internal DeflateEncoder(CompressionVersion version, CompressionType compressionLevel, int windowBits)
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

        /// <summary>
        /// Finalizer to ensure resources are released.
        /// </summary>
        ~DeflateEncoder()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="DeflateEncoder"/>.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="DeflateEncoder"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        private void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                    _zlibStream.Dispose();

                _isDisposed = true;
            }
        }

        /// <summary>
        /// Returns true if the encoder needs more input data.
        /// </summary>
        public bool NeedsInput() => 0 == _zlibStream.AvailIn;

        /// <summary>
        /// Sets the input buffer for compression.
        /// </summary>
        /// <param name="inputBufferPtr">Pointer to the input buffer.</param>
        /// <param name="count">The number of bytes to read from the input buffer.</param>
        /// <exception cref="ArgumentException">Thrown if the encoder is not ready for new input.</exception>
        internal unsafe void SetInput(byte* inputBufferPtr, int count)
        {
            if (_zlibStream.AvailIn != 0)
                throw new ArgumentException($"_zlibStream should have a AvailIn of 0");

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

        /// <summary>
        /// Compresses data from the input buffer into the output buffer.
        /// </summary>
        /// <param name="outData">The buffer to write compressed data to.</param>
        /// <returns>The number of bytes written to the output buffer.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="outData"/> is not at the correct position.</exception>
        internal int EncodeData(CompressionBuffer outData)
        {
            Debug.Assert(null != outData, "Can't pass in a null output buffer!");
            Debug.Assert(!NeedsInput(), "GetDeflateOutput should only be called after providing input");

            int bytesRead;
            readDeflateOutput(outData!, ZFlushCode.NoFlush, out bytesRead);
            return bytesRead;
        }

        /// <summary>
        /// Reads compressed output from the encoder into the output buffer using the specified flush code.
        /// </summary>
        /// <param name="outData">The buffer to write compressed data to.</param>
        /// <param name="flushCode">The flush code to use.</param>
        /// <param name="bytesRead">The number of bytes written to the output buffer.</param>
        /// <returns>The error code returned by the deflate operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="outData"/> is not at the correct position.</exception>
        private unsafe ZErrorCode readDeflateOutput(CompressionBuffer outData, ZFlushCode flushCode, out int bytesRead)
        {
            outData.Tidy();

            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            lock (SyncLock)
            {
                fixed (byte* bufPtr = &outData.Data[0])
                {
                    _zlibStream.NextOut = (nint)bufPtr;
                    _zlibStream.AvailOut = (uint)outData.AvailableWrite;

                    ZErrorCode errC = Deflate(flushCode);
                    bytesRead = outData.AvailableWrite - (int)_zlibStream.AvailOut;

                    outData.Write(bytesRead);
                    return errC;
                }
            }
        }

        /// <summary>
        /// Finishes the compression stream and writes any remaining compressed data to the output buffer.
        /// </summary>
        /// <param name="outData">The buffer to write compressed data to.</param>
        /// <param name="bytesRead">The number of bytes written to the output buffer.</param>
        /// <returns>True if the end of the stream was reached; otherwise, false.</returns>
        internal bool Finish(CompressionBuffer outData, out int bytesRead)
        {
            Debug.Assert(null != outData, "Can't pass in a null output buffer!");
            Debug.Assert(outData.AvailableWrite > 0, "Can't pass in an empty output buffer!");

            ZErrorCode errC = readDeflateOutput(outData, ZFlushCode.Finish, out bytesRead);
            return errC == ZErrorCode.StreamEnd;
        }

        /// <summary>
        /// Flushes the encoder, writing any pending compressed data to the output buffer.
        /// </summary>
        /// <param name="outData">The buffer to write compressed data to.</param>
        /// <param name="bytesRead">The number of bytes written to the output buffer.</param>
        /// <returns>True if there was something to flush; otherwise, false.</returns>
        internal bool Flush(CompressionBuffer outData, out int bytesRead)
        {
            Debug.Assert(null != outData, "Can't pass in a null output buffer!");
            Debug.Assert(outData.AvailableWrite > 0, "Can't pass in an empty output buffer!");
            Debug.Assert(NeedsInput(), "We have something left in previous input!");

            // Note: we require that NeedsInput() == true, i.e. that 0 == _zlibStream.AvailIn.
            // If there is still input left we should never be getting here; instead we
            // should be calling GetDeflateOutput.

            return readDeflateOutput(outData, ZFlushCode.SyncFlush, out bytesRead) == ZErrorCode.Ok;
        }

        /// <summary>
        /// Deallocates the input buffer handle and resets the input pointers.
        /// </summary>
        private void DeallocateInputBufferHandle()
        {
            lock (SyncLock)
            {
                _zlibStream.AvailIn = 0;
                _zlibStream.NextIn = Interop.ZLib.ZNullPtr;
            }
        }

        /// <summary>
        /// Performs the deflate operation using the specified flush code.
        /// </summary>
        /// <param name="flushCode">The flush code to use.</param>
        /// <returns>The error code returned by the deflate operation.</returns>
        /// <exception cref="ZLibException">Thrown if a fatal error occurs in the underlying zlib library.</exception>
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

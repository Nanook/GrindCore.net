using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.BZip2;

namespace Nanook.GrindCore.BZip2
{
    /// <summary>
    /// Provides a wrapper around the BZip2 streaming compression API. bzip2's bz_stream is
    /// deliberately shaped like zlib's z_stream, so this mirrors <c>DeflateEncoder</c>'s shape
    /// (one call drives one native step; the caller loops and drains output between calls) rather
    /// than LZ4/LZMA's multi-call-per-invocation style.
    /// </summary>
    internal unsafe sealed class BZip2Encoder : IDisposable
    {
        // The context must live at a single, never-moving address for its entire lifetime -
        // libbzip2 stores the bz_stream* passed to BZ2_bzCompressInit and rejects every later call
        // whose pointer doesn't match exactly (see Interop.SZ_BZip2_v1_0_8_CompressionContext's
        // doc comment). A managed struct field can be relocated by a compacting GC between separate
        // P/Invoke calls, so the context is allocated in unmanaged memory instead.
        private readonly IntPtr _ctx;
        private bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BZip2Encoder"/> class with the specified
        /// block size and work factor.
        /// </summary>
        /// <param name="blockSize100k">Block size, 1-9 (900k*n block size).</param>
        /// <param name="workFactor">Work factor, 0-250 (0 = bzip2's own default).</param>
        /// <exception cref="Exception">Thrown if the native compression context cannot be created.</exception>
        public BZip2Encoder(int blockSize100k, int workFactor)
        {
            _ctx = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SZ_BZip2_v1_0_8_CompressionContext)));
            int result = SZ_BZip2_v1_0_8_CreateCompressionContext(_ctx, blockSize100k, workFactor);
            if (result != BZ_OK)
            {
                Marshal.FreeHGlobal(_ctx);
                throw new Exception($"Failed to create BZip2 compression context (error {result})");
            }
        }

        /// <summary>
        /// Finalizer to ensure resources are released.
        /// </summary>
        ~BZip2Encoder()
        {
            dispose(false);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="BZip2Encoder"/>.
        /// </summary>
        public void Dispose()
        {
            dispose(true);
            GC.SuppressFinalize(this);
        }

        private void dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                SZ_BZip2_v1_0_8_FreeCompressionContext(_ctx);
                Marshal.FreeHGlobal(_ctx);
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Returns true if there is no available input to feed the encoder.
        /// </summary>
        public bool NeedsInput(CompressionBuffer inData) => inData.AvailableRead == 0;

        /// <summary>
        /// Feeds available input to the encoder (BZ_RUN) and writes any produced output to outData.
        /// A single native call may not consume/produce everything available; callers loop while
        /// input and output space both remain.
        /// </summary>
        /// <param name="inData">The input buffer containing data to compress.</param>
        /// <param name="outData">The output buffer to write compressed data to.</param>
        /// <returns>The number of bytes written to <paramref name="outData"/>.</returns>
        /// <exception cref="Exception">Thrown if the native compress step reports an error.</exception>
        public int EncodeData(CompressionBuffer inData, CompressionBuffer outData)
        {
            outData.Tidy();

            if (inData.AvailableRead == 0 || outData.AvailableWrite == 0)
                return 0;

            fixed (byte* inBase = inData.Data)
            fixed (byte* outBase = outData.Data)
            {
                byte* srcPtr = inBase + inData.Pos;
                byte* dstPtr = outBase + outData.Size;

                int result = SZ_BZip2_v1_0_8_CompressStream(
                    _ctx, dstPtr, (UIntPtr)outData.AvailableWrite,
                    srcPtr, (UIntPtr)inData.AvailableRead, BZ_RUN,
                    out long inSize, out long outSize);

                if (result < 0)
                    throw new Exception($"BZip2 compression failed with error code {result}");

                if (inSize > 0)
                    inData.Read((int)inSize);
                if (outSize > 0)
                    outData.Write((int)outSize);

                return (int)outSize;
            }
        }

        /// <summary>
        /// Drives one BZ_FLUSH step - a block-boundary flush that leaves the stream open for
        /// further BZ_RUN input, unlike <see cref="Finish"/>. Returns true once bzip2 signals the
        /// flush has fully completed (i.e. the stream is ready to accept BZ_RUN input again).
        /// </summary>
        /// <param name="outData">The buffer to write flushed data to.</param>
        /// <param name="bytesWritten">The number of bytes written to <paramref name="outData"/>.</param>
        /// <returns>True if the flush completed; false if more calls are required.</returns>
        public bool Flush(CompressionBuffer outData, out int bytesWritten) => step(outData, BZ_FLUSH, BZ_RUN_OK, out bytesWritten);

        /// <summary>
        /// Drives one BZ_FINISH step, ending the stream. Once called, no further input may be fed.
        /// Returns true once bzip2 signals BZ_STREAM_END.
        /// </summary>
        /// <param name="outData">The buffer to write finalized data to.</param>
        /// <param name="bytesWritten">The number of bytes written to <paramref name="outData"/>.</param>
        /// <returns>True if the stream has been finalized; false if more calls are required.</returns>
        public bool Finish(CompressionBuffer outData, out int bytesWritten) => step(outData, BZ_FINISH, BZ_STREAM_END, out bytesWritten);

        private bool step(CompressionBuffer outData, int action, int doneResult, out int bytesWritten)
        {
            outData.Tidy();

            fixed (byte* outBase = outData.Data)
            {
                byte* dstPtr = outBase + outData.Size;

                int result = SZ_BZip2_v1_0_8_CompressStream(
                    _ctx, dstPtr, (UIntPtr)outData.AvailableWrite,
                    null, (UIntPtr)0, action,
                    out long inSize, out long outSize);

                if (result < 0)
                    throw new Exception($"BZip2 {(action == BZ_FINISH ? "finish" : "flush")} failed with error code {result}");

                bytesWritten = (int)outSize;
                if (bytesWritten > 0)
                    outData.Write(bytesWritten);

                return result == doneResult;
            }
        }
    }
}

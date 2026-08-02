using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides an encoder for Zstandard (ZStd) seekable compressed data.
    /// Seekable format allows random access decompression by organizing data into
    /// independently decompressible frames with a seek table footer.
    /// </summary>
    internal unsafe class ZStdSeekableEncoder_v1_5_7 : IDisposable
    {
        private SZ_ZStd_v1_5_7_SeekableCStream _context;
        private byte[] _outputBuffer;
        private GCHandle _outputPinned;
        private IntPtr _outputPtr;
        private int _compressionLevel;
        private uint _maxFrameSize;
        private bool _disposed;

        /// <summary>
        /// Gets the recommended input buffer size for ZStd seekable compression.
        /// </summary>
        public int InputBufferSize { get; private set; }

        /// <summary>
        /// Gets the recommended output buffer size for ZStd seekable compression.
        /// </summary>
        public int OutputBufferSize { get; private set; }

        /// <summary>
        /// Gets the maximum frame size for seekable compression.
        /// Smaller frames allow more granular seeking but increase overhead.
        /// </summary>
        public uint MaxFrameSize => _maxFrameSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableEncoder_v1_5_7"/> class.
        /// </summary>
        /// <param name="blockSize">The block size to use for compression buffers.</param>
        /// <param name="maxFrameSize">Maximum size of each seekable frame (default 1MB). Smaller values allow more granular seeking.</param>
        /// <param name="compressionLevel">The compression level to use (1-22, default is 3).</param>
        /// <param name="checksumFlag">Whether to include checksums in frames (1 = yes, 0 = no, default is 1).</param>
        public ZStdSeekableEncoder_v1_5_7(int blockSize, uint maxFrameSize = 1024 * 1024, int compressionLevel = 3, int checksumFlag = 1)
        {
            if (maxFrameSize == 0)
                throw new ArgumentException("maxFrameSize must be greater than zero", nameof(maxFrameSize));

            _compressionLevel = compressionLevel;
            _maxFrameSize = maxFrameSize;
            _disposed = false;

            _context = new SZ_ZStd_v1_5_7_SeekableCStream();

            fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context)
            {
                if (Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_CreateCStream(ctxPtr) < 0)
                    throw new Exception("Failed to create ZStd seekable compression stream");

                UIntPtr initResult = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_InitCStream(
                    ctxPtr,
                    _compressionLevel,
                    checksumFlag,
                    _maxFrameSize);

                if (Interop.ZStd.SZ_ZStd_v1_5_7_IsError(initResult) != 0)
                {
                    string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_7_GetErrorName(initResult)) ?? "Unknown error";
                    Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_FreeCStream(ctxPtr);
                    throw new Exception($"Failed to initialize ZStd seekable compression stream: {errorName}");
                }
            }

            // Use standard ZStd buffer sizes as guidance
            InputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_7_CStreamInSize();
            OutputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_7_CStreamOutSize();

            _outputBuffer = BufferPool.Rent(OutputBufferSize);
            _outputPinned = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
            _outputPtr = _outputPinned.AddrOfPinnedObject();
        }

        /// <summary>
        /// Encodes data from the input buffer into the output buffer using ZStd seekable compression.
        /// </summary>
        /// <param name="inData">Input buffer containing uncompressed data.</param>
        /// <param name="outData">Output buffer to receive compressed data.</param>
        /// <param name="final">True if this is the final chunk of data.</param>
        /// <param name="cancel">Cancellation token for the operation.</param>
        /// <returns>Number of bytes written to the output buffer.</returns>
        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableEncoder_v1_5_7));

            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException("inData should have a Pos of 0", nameof(inData));
            if (outData.Size != 0)
                throw new ArgumentException("outData should have a Size of 0", nameof(outData));

            int totalCompressed = 0;

            // Process input data
            while (inData.AvailableRead > 0)
            {
                cancel.ThrowIfCancellationRequested();

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context)
                {
                    long inSize = 0;
                    long outSize = 0;

                    UIntPtr result = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_CompressStream(
                        ctxPtr,
                        (void*)_outputPtr,
                        (UIntPtr)OutputBufferSize,
                        (void*)(inputPtr + inData.Pos),
                        (UIntPtr)inData.AvailableRead,
                        &inSize,
                        &outSize);

                    if (Interop.ZStd.SZ_ZStd_v1_5_7_IsError(result) != 0)
                    {
                        string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_7_GetErrorName(result)) ?? "Unknown error";
                        throw new Exception($"ZStd seekable compression failed: {errorName}");
                    }

                    int bytesRead = (int)inSize;
                    int bytesWritten = (int)outSize;

                    inData.Read(bytesRead);
                    if (bytesWritten > 0)
                    {
                        outData.Write(_outputBuffer, 0, bytesWritten);
                        totalCompressed += bytesWritten;
                    }
                }
            }

            // Finalize if this is the last chunk
            if (final)
            {
                bool done = false;
                while (!done)
                {
                    cancel.ThrowIfCancellationRequested();

                    fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context)
                    {
                        long outSize = 0;

                        UIntPtr result = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_EndStream(
                            ctxPtr,
                            (void*)_outputPtr,
                            (UIntPtr)OutputBufferSize,
                            &outSize);

                        if (Interop.ZStd.SZ_ZStd_v1_5_7_IsError(result) != 0)
                        {
                            string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_7_GetErrorName(result)) ?? "Unknown error";
                            throw new Exception($"ZStd seekable end stream failed: {errorName}");
                        }

                        int bytesWritten = (int)outSize;
                        if (bytesWritten > 0)
                        {
                            outData.Write(_outputBuffer, 0, bytesWritten);
                            totalCompressed += bytesWritten;
                        }

                        done = (result == UIntPtr.Zero);
                    }
                }
            }

            return totalCompressed;
        }

        /// <summary>
        /// Explicitly ends the current frame without ending the entire stream.
        /// Useful for creating custom frame boundaries.
        /// </summary>
        public void EndFrame(CompressionBuffer outData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableEncoder_v1_5_7));

            bool done = false;
            while (!done)
            {
                fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context)
                {
                    long outSize = 0;

                    UIntPtr result = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_EndFrame(
                        ctxPtr,
                        (void*)_outputPtr,
                        (UIntPtr)OutputBufferSize,
                        &outSize);

                    if (Interop.ZStd.SZ_ZStd_v1_5_7_IsError(result) != 0)
                    {
                        string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_7_GetErrorName(result)) ?? "Unknown error";
                        throw new Exception($"ZStd seekable end frame failed: {errorName}");
                    }

                    int bytesWritten = (int)outSize;
                    if (bytesWritten > 0)
                    {
                        outData.Write(_outputBuffer, 0, bytesWritten);
                    }

                    done = (result == UIntPtr.Zero);
                }
            }
        }

        /// <summary>
        /// Releases resources used by the encoder.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context)
                {
                    if (_context.zcs != IntPtr.Zero)
                    {
                        Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_FreeCStream(ctxPtr);
                        _context.zcs = IntPtr.Zero;
                    }
                }

                if (_outputPinned.IsAllocated)
                    _outputPinned.Free();

                if (_outputBuffer != null)
                {
                    BufferPool.Return(_outputBuffer);
                    _outputBuffer = null!;
                }

                _disposed = true;
            }
        }
    }
}


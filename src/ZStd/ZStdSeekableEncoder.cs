using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides an encoder for Zstandard (ZStd) seekable compressed data.
    /// Seekable format allows random access decompression by organizing data into
    /// independently decompressible frames with a seek table footer.
    /// This class implements the latest ZStd version (1.5.7) directly.
    /// For older versions (e.g., 1.5.2), use <see cref="ZStdSeekableEncoderV1_5_2"/>, which inherits from this class and overrides only the version-specific logic.
    /// </summary>
    internal unsafe class ZStdSeekableEncoder : IDisposable
    {
        protected SZ_ZStd_v1_5_7_SeekableCStream _context157;
        protected byte[] _outputBuffer;
        protected GCHandle _outputPinned;
        protected IntPtr _outputPtr;
        protected int _compressionLevel;
        protected uint _maxFrameSize;
        protected bool _disposed;

        /// <summary>
        /// Gets the recommended input buffer size for ZStd seekable compression.
        /// </summary>
        public int InputBufferSize { get; protected set; }

        /// <summary>
        /// Gets the recommended output buffer size for ZStd seekable compression.
        /// </summary>
        public int OutputBufferSize { get; protected set; }

        /// <summary>
        /// Gets the maximum frame size for seekable compression.
        /// </summary>
        public uint MaxFrameSize => _maxFrameSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableEncoder"/> class.
        /// </summary>
        /// <param name="blockSize">The block size to use for compression buffers.</param>
        /// <param name="maxFrameSize">Maximum size of each seekable frame.</param>
        /// <param name="compressionLevel">The compression level to use (1-22, default is 3).</param>
        /// <param name="checksumFlag">Whether to include checksums in frames (1 = yes, 0 = no, default is 1).</param>
        public ZStdSeekableEncoder(int blockSize, uint maxFrameSize = 1024 * 1024, int compressionLevel = 3, int checksumFlag = 1)
        {
            if (maxFrameSize == 0)
                throw new ArgumentException("maxFrameSize must be greater than zero", nameof(maxFrameSize));

            _compressionLevel = compressionLevel;
            _maxFrameSize = maxFrameSize;
            _disposed = false;

            _context157 = new SZ_ZStd_v1_5_7_SeekableCStream();

            fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context157)
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

            InputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_7_CStreamInSize();
            OutputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_7_CStreamOutSize();

            _outputBuffer = BufferPool.Rent(OutputBufferSize);
            _outputPinned = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
            _outputPtr = _outputPinned.AddrOfPinnedObject();
        }

        /// <summary>
        /// Protected constructor for derived classes (v1.5.2) to bypass base initialization.
        /// </summary>
        protected ZStdSeekableEncoder()
        {
        }

        /// <summary>
        /// Encodes data from the input buffer into the output buffer using ZStd seekable compression.
        /// </summary>
        public virtual long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableEncoder));

            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException("inData should have a Pos of 0", nameof(inData));
            if (outData.Size != 0)
                throw new ArgumentException("outData should have a Size of 0", nameof(outData));

            int totalCompressed = 0;

            while (inData.AvailableRead > 0)
            {
                cancel.ThrowIfCancellationRequested();

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context157)
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

            if (final)
            {
                bool done = false;
                while (!done)
                {
                    cancel.ThrowIfCancellationRequested();

                    fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context157)
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
        /// </summary>
        public virtual void EndFrame(CompressionBuffer outData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableEncoder));

            bool done = false;
            while (!done)
            {
                fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context157)
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
        public virtual void Dispose()
        {
            if (!_disposed)
            {
                fixed (SZ_ZStd_v1_5_7_SeekableCStream* ctxPtr = &_context157)
                {
                    if (_context157.zcs != IntPtr.Zero)
                    {
                        Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_FreeCStream(ctxPtr);
                        _context157.zcs = IntPtr.Zero;
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

    /// <summary>
    /// Provides a v1.5.2 encoder for Zstandard (ZStd) seekable compressed data.
    /// Inherits from <see cref="ZStdSeekableEncoder"/> and overrides version-specific logic.
    /// </summary>
    internal unsafe class ZStdSeekableEncoderV1_5_2 : ZStdSeekableEncoder
    {
        private SZ_ZStd_v1_5_2_SeekableCStream _context152;

        public ZStdSeekableEncoderV1_5_2(int blockSize, uint maxFrameSize = 1024 * 1024, int compressionLevel = 3, int checksumFlag = 1)
        {
            if (maxFrameSize == 0)
                throw new ArgumentException("maxFrameSize must be greater than zero", nameof(maxFrameSize));

            _compressionLevel = compressionLevel;
            _maxFrameSize = maxFrameSize;
            _disposed = false;

            _context152 = new SZ_ZStd_v1_5_2_SeekableCStream();

            fixed (SZ_ZStd_v1_5_2_SeekableCStream* ctxPtr = &_context152)
            {
                if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_CreateCStream(ctxPtr) < 0)
                    throw new Exception("Failed to create ZStd seekable compression stream");

                UIntPtr initResult = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_InitCStream(
                    ctxPtr,
                    _compressionLevel,
                    checksumFlag,
                    _maxFrameSize);

                if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(initResult) != 0)
                {
                    string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(initResult)) ?? "Unknown error";
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_FreeCStream(ctxPtr);
                    throw new Exception($"Failed to initialize ZStd seekable compression stream: {errorName}");
                }
            }

            InputBufferSize = (int)Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CStreamInSize();
            OutputBufferSize = (int)Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CStreamOutSize();

            _outputBuffer = BufferPool.Rent(OutputBufferSize);
            _outputPinned = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
            _outputPtr = _outputPinned.AddrOfPinnedObject();
        }

        public override long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableEncoderV1_5_2));

            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException("inData should have a Pos of 0", nameof(inData));
            if (outData.Size != 0)
                throw new ArgumentException("outData should have a Size of 0", nameof(outData));

            int totalCompressed = 0;

            while (inData.AvailableRead > 0)
            {
                cancel.ThrowIfCancellationRequested();

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_ZStd_v1_5_2_SeekableCStream* ctxPtr = &_context152)
                {
                    long inSize = 0;
                    long outSize = 0;

                    UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_CompressStream(
                        ctxPtr,
                        (void*)_outputPtr,
                        (UIntPtr)OutputBufferSize,
                        (void*)(inputPtr + inData.Pos),
                        (UIntPtr)inData.AvailableRead,
                        &inSize,
                        &outSize);

                    if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                    {
                        string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result)) ?? "Unknown error";
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

            if (final)
            {
                bool done = false;
                while (!done)
                {
                    cancel.ThrowIfCancellationRequested();

                    fixed (SZ_ZStd_v1_5_2_SeekableCStream* ctxPtr = &_context152)
                    {
                        long outSize = 0;

                        UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_EndStream(
                            ctxPtr,
                            (void*)_outputPtr,
                            (UIntPtr)OutputBufferSize,
                            &outSize);

                        if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                        {
                            string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result)) ?? "Unknown error";
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

        public override void EndFrame(CompressionBuffer outData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableEncoderV1_5_2));

            bool done = false;
            while (!done)
            {
                fixed (SZ_ZStd_v1_5_2_SeekableCStream* ctxPtr = &_context152)
                {
                    long outSize = 0;

                    UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_EndFrame(
                        ctxPtr,
                        (void*)_outputPtr,
                        (UIntPtr)OutputBufferSize,
                        &outSize);

                    if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                    {
                        string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result)) ?? "Unknown error";
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

        public override void Dispose()
        {
            if (!_disposed)
            {
                fixed (SZ_ZStd_v1_5_2_SeekableCStream* ctxPtr = &_context152)
                {
                    if (_context152.zcs != IntPtr.Zero)
                    {
                        Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_FreeCStream(ctxPtr);
                        _context152.zcs = IntPtr.Zero;
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

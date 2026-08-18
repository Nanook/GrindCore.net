using System;
using System.IO;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a decoder for Zstandard (ZStd) seekable compressed data.
    /// Supports random access decompression using the seek table footer.
    /// This class implements the latest ZStd version (1.5.7) directly.
    /// For older versions (e.g., 1.5.2), use <see cref="ZStdSeekableDecoderV1_5_2"/>, which inherits from this class and overrides only the version-specific logic.
    /// </summary>
    internal unsafe class ZStdSeekableDecoder : IDisposable
    {
        private SZ_ZStd_v1_5_7_Seekable _context;
        protected bool _disposed;
        private Stream _sourceStream;
        private GCHandle _thisHandle;
        private GCHandle _bufferHandle;

        // Reusable read callback buffer to avoid per-call allocations
        private byte[] _readBuffer;
        private GCHandle _readBufferHandle;
        private IntPtr _readBufferPtr;
        private const int _ReadBufferSize = 128 * 1024;

        // Delegates for callbacks - must be kept alive
        private readonly ReadFunc _readFunc;
        private readonly SeekFunc _seekFunc;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadFunc(IntPtr opaque, IntPtr buffer, UIntPtr n);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SeekFunc(IntPtr opaque, long offset, int origin);

        /// <summary>
        /// Gets the number of seekable frames in the archive.
        /// </summary>
        public uint FrameCount { get; protected set; }

        /// <summary>
        /// Gets the total decompressed size of all frames.
        /// </summary>
        public ulong DecompressedSize { get; protected set; }

        /// <summary>
        /// Protected constructor for derived classes (v1.5.2) to bypass base initialization.
        /// </summary>
        protected ZStdSeekableDecoder()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableDecoder"/> class from a stream.
        /// </summary>
        public ZStdSeekableDecoder(Stream sourceStream)
        {
            if (sourceStream == null)
                throw new ArgumentNullException(nameof(sourceStream));
            if (!sourceStream.CanRead)
                throw new ArgumentException("Source stream must be readable", nameof(sourceStream));
            if (!sourceStream.CanSeek)
                throw new ArgumentException("Source stream must be seekable for random access", nameof(sourceStream));

            _sourceStream = sourceStream;
            _disposed = false;

            // Allocate and pin a reusable read buffer for native callbacks
            _readBuffer = BufferPool.Rent(_ReadBufferSize);
            _readBufferHandle = GCHandle.Alloc(_readBuffer, GCHandleType.Pinned);
            _readBufferPtr = _readBufferHandle.AddrOfPinnedObject();

            // Create delegates and pin this instance
            _readFunc = readCallback;
            _seekFunc = seekCallback;
            _thisHandle = GCHandle.Alloc(this, GCHandleType.Normal);

            IntPtr readFuncPtr = Marshal.GetFunctionPointerForDelegate(_readFunc);
            IntPtr seekFuncPtr = Marshal.GetFunctionPointerForDelegate(_seekFunc);
            IntPtr opaquePtr = GCHandle.ToIntPtr(_thisHandle);

            _context = new SZ_ZStd_v1_5_7_Seekable();

            fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
            {
                if (Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_Create(ctxPtr) < 0)
                    throw new Exception("Failed to create ZStd seekable decompression context");

                UIntPtr initResult = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_InitAdvanced(
                    ctxPtr,
                    (void*)opaquePtr,
                    readFuncPtr,
                    seekFuncPtr);

                if (Interop.ZStd.SZ_ZStd_v1_5_7_IsError(initResult) != 0)
                {
                    string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_7_GetErrorName(initResult)) ?? "Unknown error";
                    Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_Free(ctxPtr);
                    throw new Exception($"Failed to initialize ZStd seekable decompression context: {errorName}");
                }

                FrameCount = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetNumFrames(ctxPtr);
                DecompressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetDecompressedSize(ctxPtr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableDecoder"/> class from a buffer.
        /// </summary>
        public ZStdSeekableDecoder(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            _disposed = false;
            _sourceStream = null!;

            _bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            _context = new SZ_ZStd_v1_5_7_Seekable();

            try
            {
                fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
                {
                    if (Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_Create(ctxPtr) < 0)
                        throw new Exception("Failed to create ZStd seekable decompression context");

                    IntPtr bufferPtr = _bufferHandle.AddrOfPinnedObject();
                    UIntPtr initResult = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_InitBuff(
                        ctxPtr,
                        (void*)bufferPtr,
                        (UIntPtr)buffer.Length);

                    if (Interop.ZStd.SZ_ZStd_v1_5_7_IsError(initResult) != 0)
                    {
                        string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_7_GetErrorName(initResult)) ?? "Unknown error";
                        Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_Free(ctxPtr);
                        throw new Exception($"Failed to initialize ZStd seekable decompression context from buffer: {errorName}");
                    }

                    FrameCount = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetNumFrames(ctxPtr);
                    DecompressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetDecompressedSize(ctxPtr);
                }
            }
            catch
            {
                if (_bufferHandle.IsAllocated)
                    _bufferHandle.Free();
                throw;
            }
        }

        /// <summary>
        /// Decompresses data starting at a specific offset in the uncompressed stream.
        /// </summary>
        public virtual int Decompress(byte[] destination, ulong offset)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoder));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (offset >= DecompressedSize)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset exceeds decompressed size");

            fixed (byte* destPtr = destination)
            fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
            {
                UIntPtr result = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_Decompress(
                    ctxPtr,
                    (void*)destPtr,
                    (UIntPtr)destination.Length,
                    offset);

                if (Interop.ZStd.SZ_ZStd_v1_5_7_IsError(result) != 0)
                {
                    string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_7_GetErrorName(result)) ?? "Unknown error";
                    throw new Exception($"ZStd seekable decompression failed: {errorName}");
                }

                return (int)result;
            }
        }

        /// <summary>
        /// Decompresses a specific frame by index.
        /// </summary>
        public virtual int DecompressFrame(byte[] destination, uint frameIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoder));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (frameIndex >= FrameCount)
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame index exceeds frame count");

            fixed (byte* destPtr = destination)
            fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
            {
                UIntPtr result = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_DecompressFrame(
                    ctxPtr,
                    (void*)destPtr,
                    (UIntPtr)destination.Length,
                    frameIndex);

                if (Interop.ZStd.SZ_ZStd_v1_5_7_IsError(result) != 0)
                {
                    string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_7_GetErrorName(result)) ?? "Unknown error";
                    throw new Exception($"ZStd seekable frame decompression failed: {errorName}");
                }

                return (int)result;
            }
        }

        // Callback implementations
        private int readCallback(IntPtr opaque, IntPtr buffer, UIntPtr n)
        {
            try
            {
                int count = (int)n;

                if (count <= _ReadBufferSize)
                {
                    int bytesRead = _sourceStream.Read(_readBuffer, 0, count);
                    if (bytesRead > 0)
                        Marshal.Copy(_readBuffer, 0, buffer, bytesRead);
                    return bytesRead;
                }
                else
                {
                    byte[] tempBuffer = new byte[count];
                    int bytesRead = _sourceStream.Read(tempBuffer, 0, count);
                    if (bytesRead > 0)
                        Marshal.Copy(tempBuffer, 0, buffer, bytesRead);
                    return bytesRead;
                }
            }
            catch
            {
                return -1;
            }
        }

        private int seekCallback(IntPtr opaque, long offset, int origin)
        {
            try
            {
                SeekOrigin seekOrigin = origin switch
                {
                    0 => SeekOrigin.Begin,
                    1 => SeekOrigin.Current,
                    2 => SeekOrigin.End,
                    _ => throw new ArgumentException("Invalid seek origin")
                };

                _sourceStream.Seek(offset, seekOrigin);
                return 0;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Releases resources used by the decoder.
        /// </summary>
        public virtual void Dispose()
        {
            if (!_disposed)
            {
                fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
                {
                    if (_context.zs != IntPtr.Zero)
                    {
                        Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_Free(ctxPtr);
                        _context.zs = IntPtr.Zero;
                    }
                }

                if (_thisHandle.IsAllocated)
                    _thisHandle.Free();

                if (_readBufferHandle.IsAllocated)
                    _readBufferHandle.Free();

                if (_readBuffer != null)
                {
                    BufferPool.Return(_readBuffer);
                    _readBuffer = null!;
                }

                if (_bufferHandle.IsAllocated)
                    _bufferHandle.Free();

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Provides a v1.5.2 decoder for Zstandard (ZStd) seekable compressed data.
    /// Inherits from <see cref="ZStdSeekableDecoder"/> and overrides version-specific logic.
    /// </summary>
    internal unsafe class ZStdSeekableDecoderV1_5_2 : ZStdSeekableDecoder
    {
        private SZ_ZStd_v1_5_2_Seekable _context152;
        private Stream _sourceStream;
        private GCHandle _thisHandle;
        private GCHandle _bufferHandle;

        // Reusable read callback buffer
        private byte[] _readBuffer;
        private GCHandle _readBufferHandle;
        private IntPtr _readBufferPtr;
        private const int _ReadBufferSize = 128 * 1024;

        private readonly ReadFunc _readFunc;
        private readonly SeekFunc _seekFunc;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int ReadFunc(IntPtr opaque, IntPtr buffer, UIntPtr n);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SeekFunc(IntPtr opaque, long offset, int origin);

        /// <summary>
        /// Initializes a new instance from a stream.
        /// </summary>
        public ZStdSeekableDecoderV1_5_2(Stream sourceStream)
        {
            if (sourceStream == null)
                throw new ArgumentNullException(nameof(sourceStream));
            if (!sourceStream.CanRead)
                throw new ArgumentException("Source stream must be readable", nameof(sourceStream));
            if (!sourceStream.CanSeek)
                throw new ArgumentException("Source stream must be seekable for random access", nameof(sourceStream));

            _sourceStream = sourceStream;
            _disposed = false;

            _readBuffer = BufferPool.Rent(_ReadBufferSize);
            _readBufferHandle = GCHandle.Alloc(_readBuffer, GCHandleType.Pinned);
            _readBufferPtr = _readBufferHandle.AddrOfPinnedObject();

            _readFunc = readCallback;
            _seekFunc = seekCallback;
            _thisHandle = GCHandle.Alloc(this, GCHandleType.Normal);

            IntPtr readFuncPtr = Marshal.GetFunctionPointerForDelegate(_readFunc);
            IntPtr seekFuncPtr = Marshal.GetFunctionPointerForDelegate(_seekFunc);
            IntPtr opaquePtr = GCHandle.ToIntPtr(_thisHandle);

            _context152 = new SZ_ZStd_v1_5_2_Seekable();

            fixed (SZ_ZStd_v1_5_2_Seekable* ctxPtr = &_context152)
            {
                if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_Create(ctxPtr) < 0)
                    throw new Exception("Failed to create ZStd seekable decompression context");

                UIntPtr initResult = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_InitAdvanced(
                    ctxPtr,
                    (void*)opaquePtr,
                    readFuncPtr,
                    seekFuncPtr);

                if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(initResult) != 0)
                {
                    string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(initResult)) ?? "Unknown error";
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_Free(ctxPtr);
                    throw new Exception($"Failed to initialize ZStd seekable decompression context: {errorName}");
                }

                FrameCount = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_GetNumFrames(ctxPtr);
                DecompressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_GetDecompressedSize(ctxPtr);
            }
        }

        /// <summary>
        /// Initializes a new instance from a buffer.
        /// </summary>
        public ZStdSeekableDecoderV1_5_2(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            _disposed = false;
            _sourceStream = null!;

            _bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            _context152 = new SZ_ZStd_v1_5_2_Seekable();

            try
            {
                fixed (SZ_ZStd_v1_5_2_Seekable* ctxPtr = &_context152)
                {
                    if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_Create(ctxPtr) < 0)
                        throw new Exception("Failed to create ZStd seekable decompression context");

                    IntPtr bufferPtr = _bufferHandle.AddrOfPinnedObject();
                    UIntPtr initResult = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_InitBuff(
                        ctxPtr,
                        (void*)bufferPtr,
                        (UIntPtr)buffer.Length);

                    if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(initResult) != 0)
                    {
                        string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(initResult)) ?? "Unknown error";
                        Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_Free(ctxPtr);
                        throw new Exception($"Failed to initialize ZStd seekable decompression context from buffer: {errorName}");
                    }

                    FrameCount = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_GetNumFrames(ctxPtr);
                    DecompressedSize = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_GetDecompressedSize(ctxPtr);
                }
            }
            catch
            {
                if (_bufferHandle.IsAllocated)
                    _bufferHandle.Free();
                throw;
            }
        }

        public override int Decompress(byte[] destination, ulong offset)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoderV1_5_2));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (offset >= DecompressedSize)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset exceeds decompressed size");

            fixed (byte* destPtr = destination)
            fixed (SZ_ZStd_v1_5_2_Seekable* ctxPtr = &_context152)
            {
                UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_Decompress(
                    ctxPtr,
                    (void*)destPtr,
                    (UIntPtr)destination.Length,
                    offset);

                if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                {
                    string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result)) ?? "Unknown error";
                    throw new Exception($"ZStd seekable decompression failed: {errorName}");
                }

                return (int)result;
            }
        }

        public override int DecompressFrame(byte[] destination, uint frameIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoderV1_5_2));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (frameIndex >= FrameCount)
                throw new ArgumentOutOfRangeException(nameof(frameIndex), "Frame index exceeds frame count");

            fixed (byte* destPtr = destination)
            fixed (SZ_ZStd_v1_5_2_Seekable* ctxPtr = &_context152)
            {
                UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_DecompressFrame(
                    ctxPtr,
                    (void*)destPtr,
                    (UIntPtr)destination.Length,
                    frameIndex);

                if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                {
                    string errorName = Marshal.PtrToStringAnsi(Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result)) ?? "Unknown error";
                    throw new Exception($"ZStd seekable frame decompression failed: {errorName}");
                }

                return (int)result;
            }
        }

        private int readCallback(IntPtr opaque, IntPtr buffer, UIntPtr n)
        {
            try
            {
                int count = (int)n;

                if (count <= _ReadBufferSize)
                {
                    int bytesRead = _sourceStream.Read(_readBuffer, 0, count);
                    if (bytesRead > 0)
                        Marshal.Copy(_readBuffer, 0, buffer, bytesRead);
                    return bytesRead;
                }
                else
                {
                    byte[] tempBuffer = new byte[count];
                    int bytesRead = _sourceStream.Read(tempBuffer, 0, count);
                    if (bytesRead > 0)
                        Marshal.Copy(tempBuffer, 0, buffer, bytesRead);
                    return bytesRead;
                }
            }
            catch
            {
                return -1;
            }
        }

        private int seekCallback(IntPtr opaque, long offset, int origin)
        {
            try
            {
                SeekOrigin seekOrigin = origin switch
                {
                    0 => SeekOrigin.Begin,
                    1 => SeekOrigin.Current,
                    2 => SeekOrigin.End,
                    _ => throw new ArgumentException("Invalid seek origin")
                };

                _sourceStream.Seek(offset, seekOrigin);
                return 0;
            }
            catch
            {
                return -1;
            }
        }

        public override void Dispose()
        {
            if (!_disposed)
            {
                fixed (SZ_ZStd_v1_5_2_Seekable* ctxPtr = &_context152)
                {
                    if (_context152.zs != IntPtr.Zero)
                    {
                        Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_Seekable_Free(ctxPtr);
                        _context152.zs = IntPtr.Zero;
                    }
                }

                if (_thisHandle.IsAllocated)
                    _thisHandle.Free();

                if (_readBufferHandle.IsAllocated)
                    _readBufferHandle.Free();

                if (_readBuffer != null)
                {
                    BufferPool.Return(_readBuffer);
                    _readBuffer = null!;
                }

                if (_bufferHandle.IsAllocated)
                    _bufferHandle.Free();

                _disposed = true;
            }
        }
    }
}

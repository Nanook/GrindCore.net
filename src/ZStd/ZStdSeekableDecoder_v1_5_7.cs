using System;
using System.IO;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides a decoder for Zstandard (ZStd) seekable compressed data.
    /// Supports random access decompression using the seek table footer.
    /// </summary>
    internal unsafe class ZStdSeekableDecoder_v1_5_7 : IDisposable
    {
        private SZ_ZStd_v1_5_7_Seekable _context;
        private bool _disposed;
        private Stream _sourceStream;
        private GCHandle _thisHandle;
        private GCHandle _bufferHandle; // Pin buffer for buffer-based decoder

        // Reusable read callback buffer to avoid per-call allocations
        private byte[] _readBuffer;
        private GCHandle _readBufferHandle;
        private IntPtr _readBufferPtr;
        private const int _ReadBufferSize = 128 * 1024; // 128 KB - matches typical DStreamInSize

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
        public uint FrameCount { get; private set; }

        /// <summary>
        /// Gets the total decompressed size of all frames.
        /// </summary>
        public ulong DecompressedSize { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableDecoder_v1_5_7"/> class from a stream.
        /// </summary>
        /// <param name="sourceStream">The source stream containing seekable compressed data.</param>
        public ZStdSeekableDecoder_v1_5_7(Stream sourceStream)
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

            // Initialize seekable decompression context with callbacks
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

                // Query archive metadata
                FrameCount = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetNumFrames(ctxPtr);
                DecompressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetDecompressedSize(ctxPtr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdSeekableDecoder_v1_5_7"/> class from a buffer.
        /// </summary>
        /// <param name="buffer">The buffer containing seekable compressed data.</param>
        public ZStdSeekableDecoder_v1_5_7(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            _disposed = false;
            _sourceStream = null!; // No stream for buffer-based initialization

            // Pin the buffer for the lifetime of the decoder (native code keeps the pointer)
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

                    // Query archive metadata
                    FrameCount = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetNumFrames(ctxPtr);
                    DecompressedSize = Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetDecompressedSize(ctxPtr);
                }
            }
            catch
            {
                // Clean up pinned buffer on failure
                if (_bufferHandle.IsAllocated)
                    _bufferHandle.Free();
                throw;
            }
        }

        /// <summary>
        /// Decompresses data starting at a specific offset in the uncompressed stream.
        /// </summary>
        /// <param name="destination">The buffer to receive decompressed data.</param>
        /// <param name="offset">The offset in the uncompressed stream to start decompression.</param>
        /// <returns>Number of bytes decompressed.</returns>
        public int Decompress(byte[] destination, ulong offset)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoder_v1_5_7));
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
        /// <param name="destination">The buffer to receive decompressed data.</param>
        /// <param name="frameIndex">The zero-based index of the frame to decompress.</param>
        /// <returns>Number of bytes decompressed.</returns>
        public int DecompressFrame(byte[] destination, uint frameIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoder_v1_5_7));
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

        /// <summary>
        /// Gets the compressed offset of a specific frame.
        /// </summary>
        public ulong GetFrameCompressedOffset(uint frameIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoder_v1_5_7));
            if (frameIndex >= FrameCount)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));

            fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
            {
                return Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetFrameCompressedOffset(ctxPtr, frameIndex);
            }
        }

        /// <summary>
        /// Gets the decompressed offset of a specific frame.
        /// </summary>
        public ulong GetFrameDecompressedOffset(uint frameIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoder_v1_5_7));
            if (frameIndex >= FrameCount)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));

            fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
            {
                return Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetFrameDecompressedOffset(ctxPtr, frameIndex);
            }
        }

        /// <summary>
        /// Gets the compressed size of a specific frame.
        /// </summary>
        public ulong GetFrameCompressedSize(uint frameIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoder_v1_5_7));
            if (frameIndex >= FrameCount)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));

            fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
            {
                return (ulong)Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetFrameCompressedSize(ctxPtr, frameIndex);
            }
        }

        /// <summary>
        /// Gets the decompressed size of a specific frame.
        /// </summary>
        public ulong GetFrameDecompressedSize(uint frameIndex)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ZStdSeekableDecoder_v1_5_7));
            if (frameIndex >= FrameCount)
                throw new ArgumentOutOfRangeException(nameof(frameIndex));

            fixed (SZ_ZStd_v1_5_7_Seekable* ctxPtr = &_context)
            {
                return (ulong)Interop.ZStd.SZ_ZStd_v1_5_7_Seekable_GetFrameDecompressedSize(ctxPtr, frameIndex);
            }
        }

        // Callback implementations
        private int readCallback(IntPtr opaque, IntPtr buffer, UIntPtr n)
        {
            try
            {
                int count = (int)n;

                // Use the pre-allocated pinned buffer when the request fits
                if (count <= _ReadBufferSize)
                {
                    int bytesRead = _sourceStream.Read(_readBuffer, 0, count);
                    if (bytesRead > 0)
                        Marshal.Copy(_readBuffer, 0, buffer, bytesRead);
                    return bytesRead;
                }
                else
                {
                    // Rare case: request exceeds our buffer - fall back to a temporary allocation
                    byte[] tempBuffer = new byte[count];
                    int bytesRead = _sourceStream.Read(tempBuffer, 0, count);
                    if (bytesRead > 0)
                        Marshal.Copy(tempBuffer, 0, buffer, bytesRead);
                    return bytesRead;
                }
            }
            catch
            {
                return -1; // Error
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
                return 0; // Success
            }
            catch
            {
                return -1; // Error
            }
        }

        /// <summary>
        /// Releases resources used by the decoder.
        /// </summary>
        public void Dispose()
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
                {
                    _thisHandle.Free();
                }

                if (_readBufferHandle.IsAllocated)
                {
                    _readBufferHandle.Free();
                }

                if (_readBuffer != null)
                {
                    BufferPool.Return(_readBuffer);
                    _readBuffer = null!;
                }

                if (_bufferHandle.IsAllocated)
                {
                    _bufferHandle.Free();
                }

                _disposed = true;
            }
        }
    }
}


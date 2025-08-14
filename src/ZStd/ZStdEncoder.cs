using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides an encoder for Zstandard (ZStd) compressed data, supporting streaming compression.
    /// </summary>
    internal unsafe class ZStdEncoder : IDisposable
    {
        private SZ_ZStd_v1_5_6_CompressionContext _ctx156;
        private SZ_ZStd_v1_5_2_CompressionContext _ctx152;
        private readonly int _versionIndex;
        private byte[] _outputBuffer;
        private GCHandle _outputPinned;
        private IntPtr _outputPtr;
        private int _compressionLevel;

        /// <summary>
        /// Gets the recommended input buffer size for ZStd compression.
        /// </summary>
        public int InputBufferSize { get; }

        /// <summary>
        /// Gets the recommended output buffer size for ZStd compression.
        /// </summary>
        public int OutputBufferSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdEncoder"/> class with the specified block size and compression level.
        /// </summary>
        /// <param name="blockSize">The block size to use for compression.</param>
        /// <param name="compressionLevel">The compression level to use (default is 3).</param>
        /// <param name="version">The compression version to use (null or 0 = latest, 1 = v1.5.2).</param>
        /// <exception cref="Exception">Thrown if the compression context cannot be created or configured.</exception>
        public ZStdEncoder(int blockSize, int compressionLevel = 3, CompressionVersion? version = null)
        {
            _compressionLevel = compressionLevel;
            _versionIndex = version?.Index ?? 0;

            if (_versionIndex == 0)
            {
                _ctx156 = new SZ_ZStd_v1_5_6_CompressionContext();

                fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_ctx156)
                {
                    if (Interop.ZStd.SZ_ZStd_v1_5_6_CreateCompressionContext(ctxPtr) < 0)
                        throw new Exception("Failed to create Zstd v1.5.6 compression context");

                    Interop.ZStd.SZ_ZStd_v1_5_6_SetCompressionLevel(ctxPtr, _compressionLevel);
                    Interop.ZStd.SZ_ZStd_v1_5_6_SetBlockSize(ctxPtr, (nuint)blockSize);
                }

                InputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_6_CStreamInSize();
                OutputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_6_CStreamOutSize();
            }
            else // v1.5.2
            {
                _ctx152 = new SZ_ZStd_v1_5_2_CompressionContext();

                fixed (SZ_ZStd_v1_5_2_CompressionContext* ctxPtr = &_ctx152)
                {
                    if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateCompressionContext(ctxPtr) < 0)
                        throw new Exception("Failed to create Zstd v1.5.2 compression context");

                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_SetCompressionLevel(ctxPtr, _compressionLevel);
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_SetBlockSize(ctxPtr, (nuint)blockSize);
                }

                InputBufferSize = (int)Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CStreamInSize();
                OutputBufferSize = (int)Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CStreamOutSize();
            }

            // Allocate and pin buffers
            _outputBuffer = BufferPool.Rent(OutputBufferSize);
            _outputPinned = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
            _outputPtr = _outputPinned.AddrOfPinnedObject();
        }

        /// <summary>
        /// Encodes data from the input buffer into the output buffer using ZStd compression.
        /// </summary>
        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            int totalCompressed = 0;

            while (inData.AvailableRead > 0 || final)
            {
                cancel.ThrowIfCancellationRequested();

                int srcCapacity = Math.Min(inData.AvailableRead, InputBufferSize);
                long inSize;
                long outSize;

                if (_versionIndex == 0)
                {
                    fixed (byte* inputPtr = inData.Data)
                    fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_ctx156)
                    {
                        *&inputPtr += inData.Pos;

                        UIntPtr toFlush = Interop.ZStd.SZ_ZStd_v1_5_6_CompressStream(
                            ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize,
                            inputPtr, (UIntPtr)srcCapacity,
                            out inSize, out outSize);

                        if (toFlush != UIntPtr.Zero)
                            throw new Exception("TODO: ZStd compression has more to flush");

                        inData.Read((int)inSize);
                        outData.Write(_outputBuffer, 0, (int)outSize);
                        totalCompressed += (int)outSize;
                        final = false;
                    }
                }
                else // v1.5.2
                {
                    fixed (byte* inputPtr = inData.Data)
                    fixed (SZ_ZStd_v1_5_2_CompressionContext* ctxPtr = &_ctx152)
                    {
                        *&inputPtr += inData.Pos;

                        UIntPtr toFlush = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CompressStream(
                            ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize,
                            inputPtr, (UIntPtr)srcCapacity,
                            out inSize, out outSize);

                        if (toFlush != UIntPtr.Zero)
                            throw new Exception("TODO: ZStd compression has more to flush");

                        inData.Read((int)inSize);
                        outData.Write(_outputBuffer, 0, (int)outSize);
                        totalCompressed += (int)outSize;
                        final = false;
                    }
                }
            }

            return totalCompressed;
        }

        /// <summary>
        /// Flushes any remaining compressed data to the output buffer and finalizes the compression stream.
        /// </summary>
        public long Flush(CompressionBuffer outData)
        {
            long flushedSize = 0;
            long endSize;
            byte[] buff = new byte[1];
            long inSize = 0;

            if (_versionIndex == 0)
            {
                fixed (byte* inputPtr = buff)
                fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_ctx156)
                {
                    UIntPtr res = Interop.ZStd.SZ_ZStd_v1_5_6_FlushStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out flushedSize);

                    if (res != UIntPtr.Zero)
                        throw new Exception("ZStd flush failed");

                    outData.Write(_outputBuffer, 0, (int)flushedSize);

                    res = Interop.ZStd.SZ_ZStd_v1_5_6_EndStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out endSize);

                    if (res != UIntPtr.Zero)
                        throw new Exception("Failed to finalize Zstd compression");

                    outData.Write(_outputBuffer, 0, (int)endSize);

                    return flushedSize + endSize;
                }
            }
            else // v1.5.2
            {
                fixed (byte* inputPtr = buff)
                fixed (SZ_ZStd_v1_5_2_CompressionContext* ctxPtr = &_ctx152)
                {
                    UIntPtr res = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FlushStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out flushedSize);

                    if (res != UIntPtr.Zero)
                        throw new Exception("ZStd flush failed");

                    outData.Write(_outputBuffer, 0, (int)flushedSize);

                    res = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_EndStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out endSize);

                    if (res != UIntPtr.Zero)
                        throw new Exception("Failed to finalize Zstd compression");

                    outData.Write(_outputBuffer, 0, (int)endSize);

                    return flushedSize + endSize;
                }
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="ZStdEncoder"/>.
        /// </summary>
        public void Dispose()
        {
            if (_versionIndex == 0)
            {
                fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_ctx156)
                {
                    Interop.ZStd.SZ_ZStd_v1_5_6_FreeCompressionContext(ctxPtr);
                }
            }
            else
            {
                fixed (SZ_ZStd_v1_5_2_CompressionContext* ctxPtr = &_ctx152)
                {
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeCompressionContext(ctxPtr);
                }
            }

            if (_outputPinned.IsAllocated)
                try { _outputPinned.Free(); } catch { }

            BufferPool.Return(_outputBuffer);
        }
    }
}
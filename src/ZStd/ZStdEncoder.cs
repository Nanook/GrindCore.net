using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.ZStd;

namespace Nanook.GrindCore.ZStd
{
    internal unsafe class ZStdEncoder : IDisposable
    {
        private SZ_ZStd_v1_5_6_CompressionContext _context;
        private byte[] _outputBuffer;
        private GCHandle _outputPinned;
        private IntPtr _outputPtr;
        private int _compressionLevel;

        public int InputBufferSize { get; }
        public int OutputBufferSize { get; }

        public ZStdEncoder(int blockSize, int compressionLevel = 3)
        {
            _compressionLevel = compressionLevel;

            // Get recommended buffer sizes

            _context = new SZ_ZStd_v1_5_6_CompressionContext();

            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
            {
                if (SZ_ZStd_v1_5_6_CreateCompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create Zstd compression context");

                int ret = SZ_ZStd_v1_5_6_SetCompressionLevel(ctxPtr, _compressionLevel);
                int ret2 = SZ_ZStd_v1_5_6_SetBlockSize(ctxPtr, (nuint)blockSize);
            }

            InputBufferSize = (int)SZ_ZStd_v1_5_6_CStreamInSize();
            OutputBufferSize = (int)SZ_ZStd_v1_5_6_CStreamOutSize();

            // Allocate and pin buffers
            _outputBuffer = BufferPool.Rent(OutputBufferSize);

            _outputPinned = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
            _outputPtr = _outputPinned.AddrOfPinnedObject();
        }

        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
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

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
                {
                    *&inputPtr += inData.Pos;

                    UIntPtr toFlush = SZ_ZStd_v1_5_6_CompressStream(
                        ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize,
                        inputPtr, (UIntPtr)srcCapacity,
                        out inSize, out outSize);

                    if (toFlush != UIntPtr.Zero)
                        throw new Exception("TODO: ZStd compression has more to flush");

                    inData.Read((int)inSize);
                    outData.Write(_outputBuffer, 0, (int)outSize);
                    totalCompressed += (int)outSize;
                }
            }

            return totalCompressed;
        }

        public long Flush(CompressionBuffer outData)
        {
            long flushedSize;
            long endSize;

            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
            {
                UIntPtr res = SZ_ZStd_v1_5_6_FlushStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, out flushedSize);

                if (res != UIntPtr.Zero)
                    throw new Exception("ZStd flush failed");

                outData.Write(_outputBuffer, 0, (int)flushedSize);

                res = SZ_ZStd_v1_5_6_EndStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, out endSize);
                if (res != UIntPtr.Zero)
                    throw new Exception("Failed to finalize Zstd compression");

                outData.Write(_outputBuffer, 0, (int)endSize);

                return flushedSize + endSize;
            }
        }

        public void Dispose()
        {
            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
            {
                SZ_ZStd_v1_5_6_FreeCompressionContext(ctxPtr);
            }

            if (_outputPinned.IsAllocated)
                try { _outputPinned.Free(); } catch { }

            BufferPool.Return(_outputBuffer);
        }
    }
}
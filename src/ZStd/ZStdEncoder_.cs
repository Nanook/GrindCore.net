//using System;
//using System.Runtime.InteropServices;
//using static Nanook.GrindCore.Interop;
//using static Nanook.GrindCore.Interop.ZStd;

//namespace Nanook.GrindCore.ZStd
//{
//    internal unsafe class ZStdEncoder : IDisposable
//    {
//        private SZ_ZStd_v1_5_6_CompressionContext _context;
//        private byte[] _inData;
//        private GCHandle _bufferPinned;
//        private IntPtr _bufferPtr;
//        private bool _headerWritten;
//        private int _compressionLevel;

//        public int BlockSize { get; }

//        public ZStdEncoder(int blockSize, int compressionLevel = 3)
//        {
//            BlockSize = blockSize;
//            _compressionLevel = compressionLevel;

//            _context = new SZ_ZStd_v1_5_6_CompressionContext();

//            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
//            {
//                if (SZ_ZStd_v1_5_6_CreateCompressionContext(ctxPtr) < 0)
//                    throw new Exception("Failed to create Zstd compression context");
//            }

//            _inData = BufferPool.Rent(BlockSize);
//            if (_inData == null)
//                throw new Exception("Failed to allocate buffer for Zstd compression");

//            _bufferPinned = GCHandle.Alloc(_inData, GCHandleType.Pinned);
//            _bufferPtr = _bufferPinned.AddrOfPinnedObject();

//            _headerWritten = false;
//        }

//        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
//        {
//            int totalCompressed = 0;

//            while (inData.AvailableRead > 0 || final)
//            {
//                cancel.ThrowIfCancellationRequested();

//                int inputSize = Math.Min(inData.AvailableRead, BlockSize);

//                fixed (byte* inputPtr = inData.Data)
//                fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
//                {
//                    *&inputPtr += inData.Pos;
//                    UIntPtr compressedSize = SZ_ZStd_v1_5_6_CompressBlock(
//                        ctxPtr, _bufferPtr, (UIntPtr)_inData.Length,
//                        inputPtr, (UIntPtr)inputSize, _compressionLevel);

//                    if (compressedSize == UIntPtr.Zero)
//                        throw new Exception("Zstd compression failed");

//                    inData.Read(inputSize);
//                    outData.Write(_inData, 0, (int)compressedSize);
//                    totalCompressed += (int)compressedSize;
//                }
//            }

//            return totalCompressed;
//        }

//        public long Flush(CompressionBuffer outData)
//        {
//            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
//            {
//                UIntPtr flushedSize = SZ_ZStd_v1_5_6_FlushStream(ctxPtr, _bufferPtr, (UIntPtr)_inData.Length);
//                if ((uint)flushedSize < 0)
//                    throw new Exception("Zstd flush failed");

//                outData.Write(_inData, 0, (int)flushedSize);

//                UIntPtr endSize = UIntPtr.Zero; // SZ_ZStd_v1_5_6_EndStream(ctxPtr, _bufferPtr, (UIntPtr)_inData.Length);
//                if ((uint)endSize < 0)
//                    throw new Exception("Failed to finalize Zstd compression");

//                outData.Write(_inData, 0, (int)endSize);

//                return (long)((ulong)flushedSize + (ulong)endSize);
//            }
//        }

//        public void Dispose()
//        {
//            fixed (SZ_ZStd_v1_5_6_CompressionContext* ctxPtr = &_context)
//            {
//                SZ_ZStd_v1_5_6_FreeCompressionContext(ctxPtr);
//            }

//            if (_bufferPinned.IsAllocated)
//                _bufferPinned.Free();

//            BufferPool.Return(_inData);
//        }
//    }
//}
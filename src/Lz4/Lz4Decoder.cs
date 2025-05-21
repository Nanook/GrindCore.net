using System;
using System.Drawing;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lz4;

namespace Nanook.GrindCore.Lz4
{
    internal unsafe class Lz4Decoder : IDisposable
    {
        private SZ_Lz4F_v1_9_4_DecompressionContext _context;
        private byte[] _buffer;
        private GCHandle _bufferPinned;
        private IntPtr _bufferPtr;
        private bool _headerRead;

        public Lz4Decoder(int blockSize)
        {
            _context = new SZ_Lz4F_v1_9_4_DecompressionContext();

            fixed (SZ_Lz4F_v1_9_4_DecompressionContext* ctxPtr = &_context)
            {
                if (SZ_Lz4F_v1_9_4_CreateDecompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create LZ4 Frame decompression context");
            }

            _buffer = BufferPool.Rent(blockSize); // Ensure a sufficiently sized buffer
            if (_buffer == null)
                throw new Exception("Failed to allocate buffer for LZ4 decompression");

            _bufferPinned = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _bufferPtr = _bufferPinned.AddrOfPinnedObject();

            _headerRead = false;
        }

        public long DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData)
        {
            readSz = 0;
            int initOutSz = outData.Size;

            LZ4F_frameInfo_t frameInfo = new LZ4F_frameInfo_t();

            if (!_headerRead)
            {
                GCHandle frameInfoHandle = GCHandle.Alloc(frameInfo, GCHandleType.Pinned);
                IntPtr frameInfoPtr = frameInfoHandle.AddrOfPinnedObject();

                ulong srcSize = (ulong)inData.AvailableRead;

                fixed (SZ_Lz4F_v1_9_4_DecompressionContext* ctxPtr = &_context)
                fixed (byte* srcPtr = inData.Data)
                {
                    *&srcPtr += inData.Pos;
                    ulong frameInfoSize = SZ_Lz4F_v1_9_4_GetFrameInfo(ctxPtr, frameInfoPtr, srcPtr, ref srcSize);

                    if ((long)frameInfoSize < 0)
                        throw new Exception($"LZ4 Frame info error: {frameInfoSize}");
                    inData.Read((int)srcSize);
                    readSz += (int)srcSize;
                }

                frameInfoHandle.Free();
            }

            ulong outSz = (ulong)outData.AvailableWrite;
            ulong inSz = (ulong)inData.AvailableRead;

            fixed (byte* inPtr = inData.Data)
            fixed (byte* outPtr = outData.Data)
            fixed (SZ_Lz4F_v1_9_4_DecompressionContext* ctxPtr = &_context)
            {
                *&inPtr += inData.Pos;

                ulong decompressedSize = SZ_Lz4F_v1_9_4_Decompress(ctxPtr, outPtr, ref outSz, inPtr, ref inSz, IntPtr.Zero);

                if (decompressedSize < 0)
                    throw new Exception($"LZ4 Frame decompression failed with error code {decompressedSize}");

                inData.Read((int)inSz);
                outData.Write((int)outSz);
                readSz += (int)inSz;
            }

            return outData.Size - initOutSz;
        }

        public void Dispose()
        {
            fixed (SZ_Lz4F_v1_9_4_DecompressionContext* ctxPtr = &_context)
            {
                SZ_Lz4F_v1_9_4_FreeDecompressionContext(ctxPtr);
            }

            if (_bufferPinned.IsAllocated)
                _bufferPinned.Free();
            BufferPool.Return(_buffer);
        }
    }
}
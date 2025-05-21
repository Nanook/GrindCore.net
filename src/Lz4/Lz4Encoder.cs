using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lz4;

namespace Nanook.GrindCore.Lz4
{
    internal unsafe class Lz4Encoder : IDisposable
    {
        private SZ_Lz4F_v1_9_4_CompressionContext _context;
        private byte[] _buffer;
        private GCHandle _bufferPinned;
        private IntPtr _bufferPtr;
        private LZ4F_preferences_t _preferences;
        private bool _headerWritten;

        public int BlockSize { get; }

        public Lz4Encoder(int blockSize, int compressionLevel = 0)
        {
            BlockSize = blockSize;

            _context = new SZ_Lz4F_v1_9_4_CompressionContext();

            fixed (SZ_Lz4F_v1_9_4_CompressionContext* ctxPtr = &_context)
            {
                if (SZ_Lz4F_v1_9_4_CreateCompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create LZ4 Frame compression context");
            }
            // Properly allocate _preferences and pin memory
            _preferences = new LZ4F_preferences_t
            {
                frameInfo = new LZ4F_frameInfo_t
                {
                    //blockSizeID = LZ4F_blockSizeID_t.LZ4F_max4MB,
                    blockMode = LZ4F_blockMode_t.LZ4F_blockLinked,
                    contentChecksumFlag = LZ4F_contentChecksum_t.LZ4F_contentChecksumEnabled
                },
                compressionLevel = compressionLevel
            };

            _buffer = BufferPool.Rent((int)SZ_Lz4F_v1_9_4_CompressFrameBound((ulong)BlockSize, IntPtr.Zero));
            if (_buffer == null)
                throw new Exception("Failed to allocate buffer for LZ4 compression");

            _bufferPinned = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _bufferPtr = _bufferPinned.AddrOfPinnedObject();

            _headerWritten = false;
        }

        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            int totalCompressed = 0;

            // Write frame header if not already written
            if (!_headerWritten)
            {
                ulong headerSize;
                fixed (LZ4F_preferences_t* prefsPtr = &_preferences)
                fixed (SZ_Lz4F_v1_9_4_CompressionContext* ctxPtr = &_context)
                {
                    headerSize = SZ_Lz4F_v1_9_4_CompressBegin(
                    ctxPtr, _bufferPtr, (ulong)_buffer.Length, prefsPtr);
                }

                if (headerSize < 0)
                    throw new Exception("Failed to write LZ4 frame header");

                totalCompressed += (int)headerSize;
                outData.Write(_buffer, 0, (int)headerSize);
                _headerWritten = true;
            }

            while (inData.AvailableRead > 0 || final)
            {
                cancel.ThrowIfCancellationRequested();

                int inputSize = Math.Min(inData.AvailableRead, BlockSize);

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_Lz4F_v1_9_4_CompressionContext* ctxPtr = &_context)
                {
                    *&inputPtr += inData.Pos;
                    ulong compressedSize = SZ_Lz4F_v1_9_4_CompressUpdate(
                        ctxPtr, _bufferPtr, (ulong)_buffer.Length,
                        inputPtr, (ulong)inputSize, IntPtr.Zero);

                    if (compressedSize < 0)
                        throw new Exception($"LZ4 Frame compression failed with error code {compressedSize}");

                    inData.Read(inputSize);
                    outData.Write(_buffer, 0, (int)compressedSize);
                    totalCompressed += (int)compressedSize;
                    final = false;
                }
            }

            return totalCompressed;
        }

        public long Flush(CompressionBuffer outData, bool flush, bool complete)
        {
            fixed (SZ_Lz4F_v1_9_4_CompressionContext* ctxPtr = &_context)
            {
                ulong flushedSize = 0;
                ulong endSize = 0;

                if (flush)
                {
                    flushedSize = SZ_Lz4F_v1_9_4_Flush(ctxPtr, _bufferPtr, (ulong)_buffer.Length, IntPtr.Zero);
                    if (flushedSize < 0)
                        throw new Exception("LZ4 Frame flush failed");
                    outData.Write(_buffer, 0, (int)flushedSize);
                }

                if (complete)
                {
                    endSize = SZ_Lz4F_v1_9_4_CompressEnd(ctxPtr, _bufferPtr, (ulong)_buffer.Length, IntPtr.Zero);
                    if (endSize <= 0)
                        throw new Exception("Failed to finalize LZ4 Frame compression");

                    outData.Write(_buffer, 0, (int)endSize);
                }

                return (long)(flushedSize + endSize);
            }
        }

        public void Dispose()
        {
            fixed (SZ_Lz4F_v1_9_4_CompressionContext* ctxPtr = &_context)
            {
                //if (_compressionContextPtr != IntPtr.Zero)
                SZ_Lz4F_v1_9_4_FreeCompressionContext(ctxPtr);
            }

            if (_bufferPinned.IsAllocated)
                _bufferPinned.Free();

            BufferPool.Return(_buffer);
        }
    }
}
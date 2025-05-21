using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop.Lz4;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.Lz4
{
    internal unsafe class Lz4EncoderBlock : IDisposable
    {
        private SZ_Lz4_v1_9_4_Stream _stream;
        private byte[] _buffer;
        private GCHandle _bufferPinned;
        private IntPtr _bufferPtr;
        private int _acceleration;

        public int BlockSize { get; }

        public Lz4EncoderBlock(int acceleration = 1)
        {
            _stream = new SZ_Lz4_v1_9_4_Stream();
            _acceleration = acceleration;

            if (SZ_Lz4_v1_9_4_Init(ref _stream) != SZ_Lz4_v1_9_4_OK)
                throw new Exception("Failed to initialize LZ4 stream");

            BlockSize = SZ_Lz4_v1_9_4_CompressBound(4096);
            _buffer = BufferPool.Rent(BlockSize + 128);
            _bufferPinned = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

            // Store the pinned buffer pointer
            _bufferPtr = _bufferPinned.AddrOfPinnedObject();
        }

        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            int totalCompressed = 0;

            while (inData.AvailableRead >= BlockSize || (final && inData.AvailableRead != 0))
            {
                cancel.ThrowIfCancellationRequested();

                int inputSize = Math.Min(inData.AvailableRead, BlockSize);

                fixed (byte* inputPtr = inData.Data)
                {
                    *&inputPtr += inData.Pos;
                    int compressedSize = SZ_Lz4_v1_9_4_CompressFastContinue(ref _stream, inputPtr, _bufferPtr, inputSize, _buffer.Length, _acceleration);

                    if (compressedSize <= 0)
                        throw new Exception($"LZ4 compression failed (error code {compressedSize})");

                    inData.Read(inputSize);
                    outData.Write(_buffer, 0, compressedSize);
                    totalCompressed += compressedSize;
                }
            }

            inData.Tidy();

            return totalCompressed;
        }

        public void Dispose()
        {
            SZ_Lz4_v1_9_4_End(ref _stream);
            if (_bufferPinned.IsAllocated)
                _bufferPinned.Free();
            BufferPool.Return(_buffer);
        }
    }
}
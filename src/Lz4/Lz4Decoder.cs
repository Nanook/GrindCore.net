using System;
using System.Drawing;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lz4;

namespace Nanook.GrindCore.Lz4
{
    /// <summary>
    /// Provides a decoder for LZ4 frame-compressed data, supporting block-based decompression.
    /// </summary>
    internal unsafe class Lz4Decoder : IDisposable
    {
        private SZ_Lz4F_v1_10_0_DecompressionContext _context;
        private byte[] _buffer;
        private GCHandle _bufferPinned;
        private IntPtr _bufferPtr;
        private bool _headerRead;

        /// <summary>
        /// Initializes a new instance of the <see cref="Lz4Decoder"/> class with the specified block size.
        /// </summary>
        /// <param name="blockSize">The size of the buffer to allocate for decompression.</param>
        /// <exception cref="Exception">Thrown if the decompression context or buffer cannot be allocated.</exception>
        public Lz4Decoder(int blockSize)
        {
            _context = new SZ_Lz4F_v1_10_0_DecompressionContext();

            fixed (SZ_Lz4F_v1_10_0_DecompressionContext* ctxPtr = &_context)
            {
                if (SZ_Lz4F_v1_10_0_CreateDecompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create LZ4 Frame decompression context");
            }

            _buffer = BufferPool.Rent(blockSize); // Ensure a sufficiently sized buffer
            if (_buffer == null)
                throw new Exception("Failed to allocate buffer for LZ4 decompression");

            _bufferPinned = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _bufferPtr = _bufferPinned.AddrOfPinnedObject();

            _headerRead = false;
        }

        /// <summary>
        /// Decodes LZ4 frame-compressed data from the input buffer into the output buffer.
        /// </summary>
        /// <param name="inData">The input buffer containing compressed data.</param>
        /// <param name="readSz">Outputs the number of bytes read from the input buffer.</param>
        /// <param name="outData">The output buffer to write decompressed data to.</param>
        /// <returns>The number of bytes written to the output buffer.</returns>
        /// <exception cref="Exception">Thrown if frame info or decompression fails.</exception>
        public long DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData)
        {
            outData.Tidy(); //ensure all the space is at the end making _buffer.AvailableWrite safe for interop

            readSz = 0;
            int initOutSz = outData.Size;

            LZ4F_frameInfo_t frameInfo = new LZ4F_frameInfo_t();

            if (!_headerRead)
            {
                GCHandle frameInfoHandle = GCHandle.Alloc(frameInfo, GCHandleType.Pinned);
                IntPtr frameInfoPtr = frameInfoHandle.AddrOfPinnedObject();

                UIntPtr srcSize = (UIntPtr)inData.AvailableRead;

                fixed (SZ_Lz4F_v1_10_0_DecompressionContext* ctxPtr = &_context)
                fixed (byte* srcPtr = inData.Data)
                {
                    *&srcPtr += inData.Pos;
                    UIntPtr frameInfoSize = SZ_Lz4F_v1_10_0_GetFrameInfo(ctxPtr, frameInfoPtr, srcPtr, ref srcSize);

                    long frameInfoSizeL = (long)frameInfoSize.ToUInt64();
                    if (frameInfoSizeL < 0)
                        throw new Exception($"LZ4 Frame info error: {frameInfoSize.ToUInt64()}");
                    inData.Read((int)srcSize.ToUInt64());
                    readSz += (int)srcSize.ToUInt64();
                }

                frameInfoHandle.Free();
            }

            UIntPtr outSz = (UIntPtr)outData.AvailableWrite;
            UIntPtr inSz = (UIntPtr)inData.AvailableRead;

            fixed (byte* inPtr = inData.Data)
            fixed (byte* outPtr = outData.Data)
            fixed (SZ_Lz4F_v1_10_0_DecompressionContext* ctxPtr = &_context)
            {
                *&inPtr += inData.Pos;

                UIntPtr decompressedSize = SZ_Lz4F_v1_10_0_Decompress(ctxPtr, outPtr, ref outSz, inPtr, ref inSz, IntPtr.Zero);

                long decompressedSizeL = (long)decompressedSize.ToUInt64();
                if (decompressedSizeL < 0)
                    throw new Exception($"LZ4 Frame decompression failed with error code {decompressedSize.ToUInt64()}");

                inData.Read((int)inSz.ToUInt64());
                outData.Write((int)outSz.ToUInt64());
                readSz += (int)inSz.ToUInt64();
            }

            return outData.Size - initOutSz;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="Lz4Decoder"/>.
        /// </summary>
        public void Dispose()
        {
            fixed (SZ_Lz4F_v1_10_0_DecompressionContext* ctxPtr = &_context)
            {
                SZ_Lz4F_v1_10_0_FreeDecompressionContext(ctxPtr);
            }

            if (_bufferPinned.IsAllocated)
                _bufferPinned.Free();
            BufferPool.Return(_buffer);
        }
    }
}

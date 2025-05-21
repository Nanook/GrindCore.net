//using System;
//using System.IO;
//using System.Runtime.InteropServices;
//using System.Xml.Linq;
//using static Nanook.GrindCore.Interop;
//using static Nanook.GrindCore.Interop.Lz4;

//namespace Nanook.GrindCore.Lz4
//{
//    public unsafe class Lz4Stream : Stream
//    {
//        private SZ_Lz4_v1_9_4_Stream lz4Stream;
//        private Stream baseStream;
//        private bool isCompressing;
//        private byte[] _outBuffer;
//        private GCHandle _outBufferPinned;
//        private int _bufferSize = 8192; // Adjust size for streaming efficiency

//        public Lz4Stream(Stream stream, bool compressing)
//        {
//            baseStream = stream;
//            isCompressing = compressing;
//            _outBuffer = BufferPool.Rent(_bufferSize);
//            _outBufferPinned = GCHandle.Alloc(_outBuffer, GCHandleType.Pinned);

//            if (SZ_Lz4_v1_9_4_Init(ref lz4Stream) != 0)
//                throw new InvalidOperationException("Failed to initialize LZ4 stream.");
//        }

//        public override void Write(byte[] srcBuffer, int offset, int count)
//        {
//            if (!isCompressing)
//                throw new InvalidOperationException("Stream is in decompression mode.");

//            int compressedSize;

//            fixed (byte* inPtr = srcBuffer)
//            {
//                *&inPtr += offset;

//                compressedSize = SZ_Lz4_v1_9_4_CompressFastContinue(
//                ref lz4Stream,
//                inPtr,
//                _outBufferPinned.AddrOfPinnedObject(),
//                count,
//                _bufferSize,
//                1);
//            }

//            if (compressedSize > 0)
//                baseStream.Write(_outBuffer, 0, compressedSize);
//        }

//        public override int Read(byte[] dstBuffer, int offset, int count)
//        {
//            if (isCompressing)
//                throw new InvalidOperationException("Stream is in compression mode.");

//            byte[] compressedBuffer = new byte[count];
//            int bytesRead = baseStream.Read(compressedBuffer, 0, compressedBuffer.Length);

//            if (bytesRead == 0)
//                return 0;

//            fixed (byte* inPtr = compressedBuffer)
//            {
//                int ret = SZ_Lz4_v1_9_4_DecompressSafeContinue(
//                    ref lz4Stream,
//                    inPtr,
//                    _outBufferPinned.AddrOfPinnedObject(),
//                    bytesRead,
//                    _bufferSize);

//                Array.Copy(_outBuffer, 0, dstBuffer, 0, ret);
//                return ret;
//            }
//        }

//        public override void Flush() => baseStream.Flush();

//        public override bool CanRead => !isCompressing;
//        public override bool CanWrite => isCompressing;
//        public override bool CanSeek => false;
//        public override long Length => throw new NotSupportedException();
//        public override long Position
//        {
//            get => throw new NotSupportedException();
//            set => throw new NotSupportedException();
//        }
//        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
//        public override void SetLength(long value) => throw new NotSupportedException();

//        protected override void Dispose(bool disposing)
//        {
//            SZ_Lz4_v1_9_4_End(ref lz4Stream);
//            if (_outBufferPinned.IsAllocated)
//                _outBufferPinned.Free();
//            BufferPool.Return(_outBuffer);

//            base.Dispose(disposing);
//        }
//    }
//}
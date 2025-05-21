//using System;
//using System.Runtime.InteropServices;
//using static Nanook.GrindCore.Interop.Lz4;
//using static Nanook.GrindCore.Interop;

//namespace Nanook.GrindCore.Lz4
//{
//    internal unsafe class Lz4Decoder : IDisposable
//    {
//        private SZ_Lz4_v1_9_4_Stream _stream;
//        private int _blockSize;

//        public Lz4Decoder()
//        {
//            _stream = new SZ_Lz4_v1_9_4_Stream();
//            if (SZ_Lz4_v1_9_4_Init(ref _stream) != SZ_Lz4_v1_9_4_OK)
//                throw new Exception("Failed to initialize LZ4 stream");
//            _blockSize = SZ_Lz4_v1_9_4_CompressBound(4096);
//        }

//        public int DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData)
//        {
//            ulong outSz = (ulong)outData.AvailableWrite;
//            ulong inSz = (ulong)inData.AvailableRead;

//            fixed (byte* outPtr = outData.Data)
//            fixed (byte* inPtr = inData.Data)
//            {
//                *&outPtr += outData.Size;
//                *&inPtr += inData.Pos;

//                int decompressedSize = SZ_Lz4_v1_9_4_DecompressSafeContinue(ref _stream, inPtr, outPtr, (int)inSz, (int)outSz);
//                if (decompressedSize <= 0)
//                    throw new Exception($"LZ4 Decompression Error (error code {decompressedSize})");

//                readSz = (int)inSz;
//                inData.Read(readSz);
//                outData.Write(decompressedSize);

//                return decompressedSize;
//            }
//        }

//        public void Dispose()
//        {
//            SZ_Lz4_v1_9_4_End(ref _stream);
//        }
//    }
//}
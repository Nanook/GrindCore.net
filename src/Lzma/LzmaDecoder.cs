using System;
using static Nanook.GrindCore.Lzma.Interop.Lzma;
using static Nanook.GrindCore.Lzma.Interop;

namespace Nanook.GrindCore.Lzma
{
    internal unsafe class LzmaDecoder : IDisposable
    {
        private CLzmaDec _decCtx;

        public byte[] Properties { get; }

        public LzmaDecoder(byte[] properties)
        {
            Properties = properties;

            int res;

            _decCtx = new CLzmaDec();
            fixed (byte* outPtr = properties)
            {
                res = S7_Lzma_v24_07_Dec_Allocate(ref _decCtx, outPtr, (uint)properties.Length);
                S7_Lzma_v24_07_Dec_Init(ref _decCtx);
            }
            if (res != 0)
                throw new Exception($"Allocate Error {res}");
        }

        public int DecodeData(byte[] inData, long inOffset, ref long inSize, byte[] outData, long outOffset, long outSize, out int status)
        {
            ulong outSz = (ulong)outSize;
            ulong inSz = (ulong)inSize;

            fixed (byte* outPtr = &outData[outOffset])
            fixed (byte* inPtr = &inData[inOffset])
            fixed (int* statusPtr = &status)
            {
                int res = S7_Lzma_v24_07_Dec_DecodeToBuf(ref _decCtx, outPtr, &outSz, inPtr, &inSz, 0, statusPtr);
                if (res != 0)
                    throw new Exception($"Decode Error {res}");

                inSize = (long)inSz;
                return (int)outSz;
            }
        }


        public void Dispose()
        {
            S7_Lzma_v24_07_Dec_Free(ref _decCtx);
        }

    }
}
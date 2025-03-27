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
                res = Interop.Lzma.S7_Lzma_v24_07_Dec_Allocate(ref _decCtx, outPtr, (uint)properties.Length);
                S7_Lzma_v24_07_Dec_Init(ref _decCtx);
            }
            if (res != 0)
                throw new Exception($"Allocate Error {res}");
        }

        public int DecodeData(byte[] inData, int inOffset, ref int inSize, DataBlock outDataBlock, out int status)
        {
            // Get properties from DataBlock
            ulong outSz = (ulong)outDataBlock.Length;
            ulong inSz = (ulong)inSize;

            fixed (byte* outPtr = &outDataBlock.Data[outDataBlock.Offset]) // Pin memory for the output buffer
            fixed (byte* inPtr = &inData[inOffset]) // Pin memory for the input buffer
            fixed (int* statusPtr = &status) // Pin memory for the status
            {
                // Call the C interop function
                int res = S7_Lzma_v24_07_Dec_DecodeToBuf(ref _decCtx, outPtr, &outSz, inPtr, &inSz, 0, statusPtr);
                if (res != 0)
                    throw new Exception($"Decode Error {res}");

                inSize = (int)inSz; // Update inSize with the size consumed
                return (int)outSz; // Return the size of the output
            }
        }

        public void Dispose()
        {
            S7_Lzma_v24_07_Dec_Free(ref _decCtx);
        }

    }
}
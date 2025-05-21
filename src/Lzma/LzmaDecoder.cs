using System;
using static Nanook.GrindCore.Interop.Lzma;
using static Nanook.GrindCore.Interop;

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

        public int DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData, out int status)
        {
            // Get properties from DataBlock
            ulong outSz = (ulong)outData.AvailableWrite;
            ulong inSz = (ulong)inData.AvailableRead;

            fixed (byte* outPtr = outData.Data) // Pin memory for the output inData
            fixed (byte* inPtr = inData.Data) // Pin memory for the input inData
            fixed (int* statusPtr = &status) // Pin memory for the status
            {
                *&outPtr += outData.Size; //writePos is size
                *&inPtr += inData.Pos;
                // Call the C interop function
                int res = S7_Lzma_v24_07_Dec_DecodeToBuf(ref _decCtx, outPtr, &outSz, inPtr, &inSz, 0, statusPtr);
                if (res != 0)
                    throw new Exception($"Decode Error {res}");

                readSz = (int)inSz; // Update inSize with the size consumed
                inData.Read(readSz);
                outData.Write((int)outSz);
                return (int)outSz; // Return the size of the output
            }
        }

        public void Dispose()
        {
            S7_Lzma_v24_07_Dec_Free(ref _decCtx);
        }

    }
}
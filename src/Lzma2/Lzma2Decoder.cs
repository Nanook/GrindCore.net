using System;
using static Nanook.GrindCore.Interop.Lzma;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.Lzma
{
    internal struct Lzma2BlockInfo
    {
        public bool IsTerminator;
        public bool IsControlBlock;
        public bool InitState;
        public bool InitProp;
        public byte Prop;
        public int UncompressedSize;
        public int CompressedSize;
        public int CompressedHeaderSize;
        public int BlockSize => CompressedSize + CompressedHeaderSize;
        public bool NewBlock => IsControlBlock && InitState && InitProp;
    }

    internal unsafe class Lzma2Decoder : IDisposable
    {
        private CLzma2Dec _decCtx;

        public byte Properties { get; }

        public Lzma2Decoder(byte properties)
        {
            Properties = properties;
            createDecoder(properties);
        }

        private void createDecoder(byte props)
        {
            _decCtx = new CLzma2Dec() { decoder = new CLzmaDec() };
            int res = S7_Lzma2_v24_07_Dec_Allocate(ref _decCtx, props);

            if (res != 0)
                throw new Exception($"Allocate Error {res}");
        }

        public Lzma2BlockInfo ReadSubBlockInfo(byte[] inData, ulong inOffset)
        {
            if (inOffset >= (ulong)inData.LongLength)
                throw new ArgumentOutOfRangeException(nameof(inOffset), "Position is outside the bounds of the data array.");

            byte b = inData[inOffset];
            bool isControlBlock = (b & 0b10000000) != 0;
            bool initProp =       (b & 0b01000000) != 0 && isControlBlock;
            bool initState =      (b & 0b00100000) != 0 && isControlBlock;

            return new Lzma2BlockInfo()
            {
                IsTerminator = b == 0,
                IsControlBlock = isControlBlock,
                InitProp = initProp,
                InitState = initState,
                Prop = initProp ? inData[inOffset + 5] : (byte)0,
                UncompressedSize = isControlBlock || b == 1 ? ((b & 0x1F) << 16) + (inData[inOffset + 1] << 8) + inData[inOffset + 2] + 1 : 0,
                CompressedSize = isControlBlock ? (inData[inOffset + 3] << 8) + inData[inOffset + 4] + 1 : 0,
                CompressedHeaderSize = isControlBlock ? (initProp ? 6 : 5) : 0
            };

        }

        public void SetProps(byte props)
        {
            S7_Lzma2_v24_07_Dec_Free(ref _decCtx);
            createDecoder(props);
        }

        public void SetState()
        {
            S7_Lzma2_v24_07_Dec_Init(ref _decCtx);
        }

        public int DecodeData(CompressionBuffer inData, ref int inSize, CompressionBuffer outData, int outSize, out int status)
        {
            ulong inSz = (ulong)inSize;
            ulong outSz = (ulong)outSize;
            fixed (byte* outPtr = outData.Data)
            fixed (byte* inPtr = inData.Data)
            fixed (int* statusPtr = &status)
            {
                *&outPtr += outData.Size; //writePos is Size
                *&inPtr += inData.Pos;
                int res = S7_Lzma2_v24_07_Dec_DecodeToBuf(ref _decCtx, outPtr, &outSz, inPtr, &inSz, 0, statusPtr);
                if (res != 0)
                    throw new Exception($"Decode Error {res}");

                outData.Write((int)outSz);
                inData.Read((int)inSz);
                inSize = (int)inSz;
                return (int)outSz;
            }
        }


        public void Dispose()
        {
            S7_Lzma2_v24_07_Dec_Free(ref _decCtx);
        }

    }
}
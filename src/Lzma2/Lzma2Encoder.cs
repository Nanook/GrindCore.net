using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Lzma.Interop.Lzma;
using static Nanook.GrindCore.Lzma.Interop;
using System.Diagnostics;

namespace Nanook.GrindCore.Lzma
{
    // This lzma2 encoder does not support true SOLID mode. The C code requires a blocks or streams to perform its encoding.
    // Internally, LZMA2 Stream mode starts encoding a block in SOLID mode and loops reading the stream and encoding until the block is complete.
    // In C# we want to supply data in a Stream.Write() style, so LZMA buffers must be used. A pseudo solid mode is to sacrifice memory for larger buffers.

    internal unsafe class Lzma2Encoder : IDisposable
    {
        private IntPtr _encoder;

        public byte Properties { get; }
        public ulong BytesIn { get; private set; }
        public ulong BytesOut { get; private set; }
        public ulong BytesFullSize { get; private set; }

        private CLzma2Enc getState()
        {
            return Marshal.PtrToStructure<CLzma2Enc>(_encoder);
        }

        public Lzma2Encoder(int level = 5, int threads = 1, ulong blockSize = 0, uint dictSize = 0, int wordSize = 0)
        {
            _encoder = S7_Lzma2_v24_07_Enc_Create();
            BytesFullSize = blockSize;
            if (_encoder == IntPtr.Zero)
                throw new Exception("Failed to create LZMA2 encoder.");

            // encoder already has props, replace them. Blank lc, lp etc to ensure they're recalculated from the level
            CLzma2EncProps props = new CLzma2EncProps();
            props.lzmaProps.level = level;
            props.lzmaProps.dictSize = props.lzmaProps.mc = dictSize;
            props.lzmaProps.lc = props.lzmaProps.lp = props.lzmaProps.pb = props.lzmaProps.algo = props.lzmaProps.fb = props.lzmaProps.btMode = props.lzmaProps.numHashBytes = props.lzmaProps.numThreads = -1;
            props.lzmaProps.numHashBytes = 0;
            props.lzmaProps.writeEndMark = 0; // BytesFullSize == 0 ? 0u : 1u;
            props.lzmaProps.affinity = 0;
            props.lzmaProps.numThreads = threads;

            props.lzmaProps.fb = wordSize; //default is 32 in ui

            props.lzmaProps.reduceSize = ulong.MaxValue; //this is the full filesize - -1 means set to blocksize if blocksize not -1(solid)|0(auto) && blocksize<filesize
            props.blockSize = blockSize; // ulong.MaxValue; //-1=solid 0=auto
            props.numBlockThreads_Max = threads;
            props.numBlockThreads_Reduced = threads; //force > 1 to allow Code with multi block
            props.numTotalThreads = threads;

            // Use a fixed statement to pass the struct to the function
            int res = S7_Lzma2_v24_07_Enc_SetProps(_encoder, ref props);
            if (res != 0)
                throw new Exception($"Failed to set LZMA2 encoder config {res}");

            this.Properties = S7_Lzma2_v24_07_Enc_WriteProperties(_encoder);

            //var enc = getState();

        }

        public int EncodeData(byte[] inData, int inOffset, int inSize, byte[] outData, int outOffset, int outSize)
        {
            return (int)EncodeData(inData, (ulong)inOffset, (ulong)inSize, outData, (ulong)outOffset, (ulong)outSize);
        }

        public ulong EncodeData(byte[] inData, ulong inOffset, ulong inSize, byte[] outData, ulong outOffset, ulong outSize)
        {
            fixed (byte* outPtr = &outData[outOffset])
            fixed (byte* inPtr = &inData[inOffset])
            {
                int res = S7_Lzma2_v24_07_Enc_Encode2(_encoder, outPtr, &outSize, inPtr, inSize, IntPtr.Zero);
                if (res != 0)
                    throw new Exception($"Encode Error {res}");
            }

            var enc = getState();

            this.BytesIn += inSize;
            this.BytesOut += outSize;

            return outSize;
        }


        public void Dispose()
        {
            if (_encoder != IntPtr.Zero)
            {
                S7_Lzma2_v24_07_Enc_Destroy(_encoder);
                _encoder = IntPtr.Zero;
            }
        }

    }
}
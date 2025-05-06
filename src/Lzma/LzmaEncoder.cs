using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Lzma.Interop.Lzma;
using static Nanook.GrindCore.Lzma.Interop;
using System.Linq;

namespace Nanook.GrindCore.Lzma
{
    internal unsafe class LzmaEncoder : IDisposable
    {
        private IntPtr _encoder;
        private CBufferInStream _inStream;
        private byte[] _inBuffer;
        private GCHandle _inBufferPinned;

        public byte[] Properties { get; }
        public int BlockSize { get; }

        public LzmaEncoder(int level = 5, uint dictSize = 0, int wordSize = 0)
        {
            CLzmaEncProps props = new CLzmaEncProps();

            S7_Lzma_v24_07_EncProps_Init(ref props);

            props.level = level;
            props.dictSize = props.mc = dictSize;
            props.lc = props.lp = props.pb = props.algo = props.fb = props.btMode = props.numHashBytes = props.numThreads = -1;
            props.numHashBytes = 0;
            props.writeEndMark = 0; // BytesFullSize == 0 ? 0u : 1u;
            props.affinity = 0;
            props.numThreads = 1;

            props.fb = wordSize; //default is 32 in ui

            props.reduceSize = ulong.MaxValue; //this is the full filesize - -1 means set to blocksize if blocksize not -1(solid)|0(auto) && blocksize<filesize

            _encoder = S7_Lzma_v24_07_Enc_Create();

            int res = S7_Lzma_v24_07_Enc_SetProps(_encoder, ref props); //normalises properties
            if (res != 0)
                throw new Exception($"Failed to set LZMA2 encoder config {res}");

            byte[] p = new byte[0x10];
            ulong sz = (ulong)p.Length;

            fixed (byte* inPtr = p)
                S7_Lzma_v24_07_Enc_WriteProperties(_encoder, inPtr, &sz);
            this.Properties = p.Take((int)sz).ToArray();

            uint bufferSize = 0;
            uint dSize = 0;

            res = S7_Lzma_v24_07_Enc_LzmaCodeMultiCallPrepare(_encoder, &bufferSize, &dSize, 0);
            if (res != 0)
                throw new Exception($"Failed to set LZMA2 encoder config {res}");

            this.BlockSize = (int)bufferSize;
            bufferSize += 0x8; // only needs 1 extra byte to ensure the end is not reached. Just allign to 8

            _inBuffer = BufferPool.Rent((int)bufferSize);
            _inBufferPinned = GCHandle.Alloc(_inBuffer, GCHandleType.Pinned);
            _inStream = new CBufferInStream() { buffer = _inBufferPinned.AddrOfPinnedObject(), size = bufferSize };

            //var s = getState();
        }

        public ulong EncodeTest(byte[] inData, long inSize, byte[] outData, long outSize)
        {
            GCHandle inBufferPinned = GCHandle.Alloc(inData, GCHandleType.Pinned);
            CBufferInStream inStream = new CBufferInStream() { buffer = inBufferPinned.AddrOfPinnedObject(), size = (ulong)inSize, remaining = (ulong)inSize };
            ulong sz = (ulong)outSize;
            fixed (byte* inPtr = inData)
            fixed (byte* outPtr = outData)
            {
                int res = S7_Lzma_v24_07_Enc_EncodeTest(_encoder, outPtr, &sz, ref inStream);
            }
            inBufferPinned.Free();
            return sz;
        }

        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            uint available = 0;
            int total = 0;
            bool finalfinal = false;

            int res = 0;
            ulong outSz = 0;
            int outTotal = 0;

            while (inData.AvailableRead != 0 || (final && !finalfinal))
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                if (_inStream.pos == _inStream.size)
                    _inStream.pos = 0; // wrap around

                int p = (int)((_inStream.pos + _inStream.remaining) % _inStream.size);

                int sz = (int)Math.Min((ulong)inData.AvailableRead, Math.Min(_inStream.size - _inStream.remaining, (ulong)this.BlockSize));

                int endSz = (int)(_inStream.size - (ulong)p);
                inData.Read(_inBuffer, (int)p, (int)Math.Min(sz, endSz));

                // copy data at start of circular buffer
                if (sz > endSz)
                    inData.Read(_inBuffer, 0, (int)(sz - endSz));

                total += sz;
                _inStream.remaining += (ulong)sz;

                finalfinal = final && inData.AvailableRead == 0;

                if (!finalfinal && _inStream.remaining < (ulong)this.BlockSize)
                    break;

                outSz = (ulong)(outData.AvailableWrite);

                fixed (byte* outPtr = outData.Data)
                {
                    *&outPtr += outData.Size; //writePos is Size
                    res = S7_Lzma_v24_07_Enc_LzmaCodeMultiCall(_encoder, outPtr, &outSz, ref _inStream, this.BlockSize, &available, finalfinal ? 1 : 0);
                    outTotal += (int)outSz;
                }
                outData.Write((int)outSz);

                if (res != 0)
                    throw new Exception($"Encode Error {res}");
            }

            return outTotal;
        }

        public void Dispose()
        {
            if (_encoder != IntPtr.Zero)
            {
                S7_Lzma_v24_07_Enc_Destroy(_encoder);
                _encoder = IntPtr.Zero;
            }
            if (_inBufferPinned.IsAllocated)
                _inBufferPinned.Free();
            BufferPool.Return(_inBuffer);
        }

    }
}
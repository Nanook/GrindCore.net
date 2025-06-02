using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop.Lzma;
using static Nanook.GrindCore.Interop;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Security.Cryptography;

namespace Nanook.GrindCore.Lzma
{
    // This lzma2 encoder does not support true SOLID mode. The C code requires a blocks or streams to perform its encoding.
    // Internally, LZMA2 Stream mode starts encoding a block in SOLID mode and loops reading the stream and encoding until the block is complete.
    // In C# we want to supply inData in a Stream.Write() style, so LZMA buffers must be used. A pseudo solid mode is to sacrifice memory for larger buffers.

    internal unsafe class Lzma2Encoder : IDisposable
    {
        private const int LZMA2_BLOCK_SIZE = 1 << 21; // size of unpacked blocks - important for multicall solid encoding
        private IntPtr _encoder;

        private CBufferInStream _inStream;

        private byte[] _inBuffer;
        private GCHandle _inBufferPinned;

        private bool _solid;
        private long _blkTotal;
        private bool _blockComplete;

        public byte Properties { get; }
        public long BlockSize { get; }

        public Lzma2Encoder(int level = 5, int threads = 1, long blockSize = -1, int dictSize = 0, int wordSize = 0, int minBufferSize = 0)
        {
            if (threads <= 0)
                threads = 1;

            _encoder = SZ_Lzma2_v24_07_Enc_Create();
            if (_encoder == IntPtr.Zero)
                throw new Exception("Failed to create LZMA2 encoder.");

            // encoder already has props, replace them. Blank lc, lp etc to ensure they're recalculated from the level
            CLzma2EncProps props = new CLzma2EncProps();
            //init
            props.lzmaProps.level = 5;
            props.lzmaProps.dictSize = props.lzmaProps.mc = 0;
            props.lzmaProps.reduceSize = ulong.MaxValue; //this is the full filesize - -1 means set to blocksize if blocksize not -1(solid)|0(auto) && blocksize<filesize
            props.lzmaProps.lc = props.lzmaProps.lp = props.lzmaProps.pb = props.lzmaProps.algo = props.lzmaProps.fb = props.lzmaProps.btMode = props.lzmaProps.numHashBytes = props.lzmaProps.numThreads = -1;
            props.lzmaProps.numHashBytes = 0;
            props.lzmaProps.writeEndMark = 0; // BytesFullSize == 0 ? 0u : 1u;
            props.lzmaProps.affinity = 0;
            props.blockSize = 0; //-1=solid 0=auto
            props.numBlockThreads_Max = -1;
            props.numBlockThreads_Reduced = -1; //force > 1 to allow Code with multi block
            props.numTotalThreads = -1;

            //config
            props.lzmaProps.level = level;
            props.lzmaProps.numThreads = -1;
            props.numBlockThreads_Max = threads;
            props.numBlockThreads_Reduced = -1; //force > 1 to allow Code with multi block
            props.numTotalThreads = threads;
            if (threads == 1 || blockSize == -1)
                props.blockSize = ulong.MaxValue;
            else if (blockSize == 0 && minBufferSize > 0)
                props.blockSize = (ulong)minBufferSize / (ulong)threads;
            else
                props.blockSize = (ulong)blockSize / (ulong)threads; // -1=solid 0=auto - auto switches to solid if props.lzmaProps.numThreads <= 1 - else 
            //props.lzmaProps.reduceSize = -1; //this is the full filesize - -1 means set to blocksize if blocksize not -1(solid)|0(auto) && blocksize<filesize
            //props.lzmaProps.fb = wordSize; //default is 32 in ui

            _solid = props.blockSize == ulong.MaxValue; // -1
            this.BlockSize = _solid && blockSize == 0 ? -1 : blockSize; //auto(0) is solid

            // Use a fixed statement to pass the struct to the function
            int res = SZ_Lzma2_v24_07_Enc_SetProps(_encoder, ref props);

#if NET6_0_OR_GREATER
            //CLzma2Enc myStruct = Marshal.PtrToStructure<CLzma2Enc>(_encoder);
#endif
            if (res != 0)
                throw new Exception($"Failed to set LZMA2 encoder config {res}");

            this.Properties = SZ_Lzma2_v24_07_Enc_WriteProperties(_encoder);
            //_limitedInStream = new CLimitedSeqInStream();

            long bufferSize = (_solid || this.BlockSize > int.MaxValue ? 0x400000L : this.BlockSize) + 0x8; // only needs 1 extra byte to ensure the end is not reached. Just allign to 8

            _inBuffer = BufferPool.Rent(bufferSize); //plus a bit to make sure we don't reach the end
            _inBufferPinned = GCHandle.Alloc(_inBuffer, GCHandleType.Pinned);

            _inStream = new CBufferInStream() { buffer = _inBufferPinned.AddrOfPinnedObject(), size = (ulong)bufferSize };
            _blkTotal = 0;

            SZ_Lzma2_v24_07_Enc_EncodeMultiCallPrepare(_encoder);
        }

        public int EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            if (_solid)
                return encodeDataSolid(inData, outData, final, cancel);
            else
                return encodeDataMt(inData, outData, final, cancel);
        }

        private int encodeDataMt(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            ulong outSz = (ulong)outData.AvailableWrite;
            uint available = (uint)inData.AvailableRead;
            fixed (byte* outPtr = outData.Data)
            fixed (byte* inPtr = inData.Data)
            {
                *&outPtr += outData.Size;
                *&inPtr += inData.Pos;
                // setting indata forces SZ_Lzma2_v24_07_Enc_Encode2 mode with multithreading support
                int res = SZ_Lzma2_v24_07_Enc_Encode2(_encoder, outPtr, &outSz, inPtr, (ulong)inData.AvailableRead, IntPtr.Zero);

                outSz--; //remove the null
                outData.Write((int)outSz);

                if (res != 0)
                    throw new Exception($"Encode Error {res}");
            }

            inData.Read(inData.AvailableRead);

            return (int)outSz;
        }

        private int encodeDataSolid(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            long inTotal = 0;
            int res = 0;
            ulong outSz = 0;
            int outTotal = 0;
            bool finalfinal = false;
            bool blkFinal = false;

            while (inData.AvailableRead != 0 || final)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                if (_inStream.pos == _inStream.size)
                    _inStream.pos = 0; // wrap around

                int p = (int)((_inStream.pos + _inStream.remaining) % _inStream.size);

                int sz = (int)Math.Min(inData.AvailableRead, (long)Math.Min(_inStream.size - _inStream.remaining, (ulong)this.BlockSize - (ulong)_blkTotal)); //-1 is max value here

                int endSz = (int)(_inStream.size - (ulong)p);
                inData.Read(_inBuffer, (int)p, (int)Math.Min(sz, endSz));

                // copy inData at start of circular _outBuffer
                if (sz > endSz)
                    inData.Read(_inBuffer, 0, (int)(sz - endSz));

                inTotal += sz;
                _blkTotal += sz;
                _inStream.remaining += (ulong)sz;

                finalfinal = final && inData.AvailableRead == 0 && _inStream.remaining == 0;
                blkFinal = this.BlockSize == _blkTotal;

                if (!final && !blkFinal && _inStream.remaining < _inStream.size) //need more inData 
                    break;

                long inSz = (long)_inStream.remaining;

                fixed (byte* outPtr = outData.Data)
                {
                    do
                    {
                        outSz = (ulong)outData.AvailableWrite;
                        byte* outPtr2 = *&outPtr + outData.Size; //writePos is Size
                        _blockComplete = finalfinal || blkFinal;
                        res = SZ_Lzma2_v24_07_Enc_EncodeMultiCall(_encoder, outPtr2, &outSz, ref _inStream, 0u, _blockComplete ? 1u : 0u);
                        outTotal += (int)outSz;
                        outData.Write((int)outSz);
                    } while (outSz != 0 && (finalfinal || blkFinal));

                    if (blkFinal && !finalfinal)
                    {
                        SZ_Lzma2_v24_07_Enc_EncodeMultiCallPrepare(_encoder);
                        _blkTotal = 0;
                    }
                }

                if (inSz == 0 && outSz == 0) //nothing left
                    break;

                if (res != 0)
                    throw new Exception($"Encode Error {res}");
            }

            return outTotal;
        }

        public void Dispose()
        {
            if (!_blockComplete)
            {
                byte[] dummy = new byte[0];
                ulong zero = 0;
                fixed (byte* d = dummy) //close things
                    SZ_Lzma2_v24_07_Enc_EncodeMultiCall(_encoder, d, &zero, ref _inStream, 0u, 1u);
            }
            if (_encoder != IntPtr.Zero)
            {
                SZ_Lzma2_v24_07_Enc_Destroy(_encoder);
                _encoder = IntPtr.Zero;
            }
            if (_inBufferPinned.IsAllocated)
                _inBufferPinned.Free();
            BufferPool.Return(_inBuffer);

        }

    }
}
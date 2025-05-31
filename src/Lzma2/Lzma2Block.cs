using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lzma;

namespace Nanook.GrindCore.Lzma
{
    public class Lzma2Block : CompressionBlock
    {
        private CLzma2EncProps _props;
        private byte _properties; // Store LZMA2 encoded property byte

        public override int RequiredCompressOutputSize { get; }

        public Lzma2Block(CompressionAlgorithm algorithm, CompressionOptions options) : base(algorithm, options)
        {
            int blockSize = (int)options.BlockSize!;
            int level = (int)options.Type;
            int threads = options.ThreadCount ?? 1;
            CLzma2EncProps props = new CLzma2EncProps();
            S7_Lzma2_v24_07_Enc_Construct(ref _props);
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

            _props = props;
            // Normalize properties
            S7_Lzma2_v24_07_Enc_Normalize(ref _props);

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x20; ; // Adjust for overhead
        }

        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                IntPtr encoder = S7_Lzma2_v24_07_Enc_Create();
                S7_Lzma2_v24_07_Enc_SetProps(encoder, ref _props);
                S7_Lzma2_v24_07_Enc_SetDataSize(encoder, (ulong)srcData.Length);

                // Retrieve LZMA2 encoded property byte
                _properties = S7_Lzma2_v24_07_Enc_WriteProperties(encoder);

                ulong compressedSize = (ulong)dstData.Length;
                int result = S7_Lzma2_v24_07_Enc_Encode2(
                    encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, IntPtr.Zero);

                S7_Lzma2_v24_07_Enc_Destroy(encoder);

                if (result != 0)
                    throw new InvalidOperationException("LZMA2 Block Compression failed.");

                return (int)compressedSize;
            }
        }

        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                ulong srcSize = (ulong)srcData.Length;
                ulong decompressedSize = (ulong)dstData.Length;
                int status = 0;

                int result = S7_Lzma2_v24_07_Decode(
                    dstPtr, &decompressedSize, srcPtr, &srcSize, _properties, 1, &status);

                if (result != 0)
                    throw new InvalidOperationException("LZMA2 Block Decompression failed.");

                return (int)decompressedSize;
            }
        }
    }
}
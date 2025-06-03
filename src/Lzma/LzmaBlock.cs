using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lzma;

namespace Nanook.GrindCore.Lzma
{
    public class LzmaBlock : CompressionBlock
    {
        private CLzmaEncProps _props;
        private byte[] _properties; // Store the encoded LZMA properties

        public override int RequiredCompressOutputSize { get; }
        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.Lzma;

        public LzmaBlock(CompressionOptions options) : base(options)
        {
            int blockSize = (int)options.BlockSize!;
            _props = new CLzmaEncProps();
            SZ_Lzma_v24_07_EncProps_Init(ref _props);
            _props.level = (int)this.CompressionType;
            _props.dictSize = (uint)options.BlockSize!;
            SZ_Lzma_v24_07_EncProps_Normalize(ref _props);

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x10; // Adjust for overhead
        }

        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                IntPtr encoder = SZ_Lzma_v24_07_Enc_Create();
                SZ_Lzma_v24_07_Enc_SetProps(encoder, ref _props);
                SZ_Lzma_v24_07_Enc_SetDataSize(encoder, (ulong)srcData.Length);

                // Retrieve encoded properties
                _properties = new byte[5];
                ulong propsSize = (ulong)_properties.Length;
                fixed (byte* propPtr = _properties)
                {
                    SZ_Lzma_v24_07_Enc_WriteProperties(encoder, propPtr, &propsSize);
                }

                ulong compressedSize = (ulong)dstData.Length;
                int result = SZ_Lzma_v24_07_Enc_MemEncode(
                    encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, 0, IntPtr.Zero);

                SZ_Lzma_v24_07_Enc_Destroy(encoder);

                if (result != 0)
                    throw new InvalidOperationException("LZMA Block Compression failed.");

                return (int)compressedSize;
            }
        }

        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            fixed (byte* propPtr = _properties)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                ulong srcSize = (ulong)srcData.Length;
                ulong decompressedSize = (ulong)dstData.Length;
                int status = 0;

                int result = SZ_Lzma_v24_07_Dec_LzmaDecode(
                    dstPtr, &decompressedSize, srcPtr, &srcSize, propPtr, (uint)_properties.Length, 1, &status);

                if (result != 0)
                    throw new InvalidOperationException("LZMA Block Decompression failed.");

                return (int)decompressedSize;
            }
        }
    }
}
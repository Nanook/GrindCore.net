using System;
using System.Linq;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lzma;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Provides a block-based implementation of the LZMA compression algorithm.
    /// </summary>
    public class LzmaBlock : CompressionBlock
    {
        private CLzmaEncProps _props;

        /// <summary>
        /// Gets the required output buffer size for compression, as determined by the LZMA algorithm.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LzmaBlock"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        public LzmaBlock(CompressionOptions options) : base(CompressionAlgorithm.Lzma, options)
        {
            int blockSize = (int)options.BlockSize!;
            this.Properties = options.InitProperties;
            _props = new CLzmaEncProps();
            SZ_Lzma_v24_07_EncProps_Init(ref _props);
            _props.level = (int)this.CompressionType;
            _props.dictSize = (uint)options.BlockSize!;
            SZ_Lzma_v24_07_EncProps_Normalize(ref _props);

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x10; // Adjust for overhead
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using LZMA.
        /// </summary>
        /// <param name="srcData">The source data block to compress.</param>
        /// <param name="dstData">The destination data block to write compressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if compression fails.</exception>
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
                byte[] p = BufferPool.Rent(0x10);
                ulong sz = (ulong)p.Length;

                fixed (byte* inPtr = p)
                    SZ_Lzma_v24_07_Enc_WriteProperties(encoder, inPtr, &sz);
                this.Properties = p.Take((int)sz).ToArray();
                BufferPool.Return(p);

                ulong compressedSize = (ulong)dstData.Length;
                int result = SZ_Lzma_v24_07_Enc_MemEncode(
                    encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, 0, IntPtr.Zero);

                SZ_Lzma_v24_07_Enc_Destroy(encoder);

                if (result != 0)
                    throw new InvalidOperationException("LZMA Block Compression failed.");

                return (int)compressedSize;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using LZMA.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if decompression fails.</exception>
        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            if (this.Properties == null || this.Properties.Length != 5)
                throw new InvalidOperationException("LZMA2 Properties must be set and contain 5 bytes before decompression.");

            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            fixed (byte* propPtr = this.Properties)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                ulong srcSize = (ulong)srcData.Length;
                ulong decompressedSize = (ulong)dstData.Length;
                int status = 0;

                int result = SZ_Lzma_v24_07_Dec_LzmaDecode(
                    dstPtr, &decompressedSize, srcPtr, &srcSize, propPtr, (uint)this.Properties.Length, 1, &status);

                if (result != 0)
                    throw new InvalidOperationException("LZMA Block Decompression failed.");

                return (int)decompressedSize;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="LzmaBlock"/>.
        /// </summary>
        internal override void OnDispose()
        {
        }
    }
}


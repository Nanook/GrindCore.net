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
            SZ_Lzma_v25_01_EncProps_Init(ref _props);
            _props.level = (int)this.CompressionType;
            _props.dictSize = (uint)options.BlockSize!;
            SZ_Lzma_v25_01_EncProps_Normalize(ref _props);

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x10; // Adjust for overhead
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using LZMA.
        /// </summary>
        /// <param name="srcData">The source data block to compress.</param>
        /// <param name="dstData">The destination data block to write compressed data to.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal unsafe override CompressionResultCode OnCompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                IntPtr encoder = SZ_Lzma_v25_01_Enc_Create();
                SZ_Lzma_v25_01_Enc_SetProps(encoder, ref _props);
                SZ_Lzma_v25_01_Enc_SetDataSize(encoder, (ulong)srcData.Length);

                // Retrieve encoded properties
                byte[] p = BufferPool.Rent(0x10);
                ulong sz = (ulong)p.Length;

                fixed (byte* inPtr = p)
                    SZ_Lzma_v25_01_Enc_WriteProperties(encoder, inPtr, &sz);
                this.Properties = p.Take((int)sz).ToArray();
                BufferPool.Return(p);

                ulong compressedSize = (ulong)dstCount;
                int result = SZ_Lzma_v25_01_Enc_MemEncode(
                    encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, 0, IntPtr.Zero);

                SZ_Lzma_v25_01_Enc_Destroy(encoder);

                if (result != 0)
                {
                    dstCount = 0;
                    return mapResult(result);
                }

                dstCount = (int)compressedSize;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using LZMA.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal unsafe override CompressionResultCode OnDecompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            if (this.Properties == null || this.Properties.Length != 5)
            {
                dstCount = 0;
                return CompressionResultCode.InvalidParameter;
            }

            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            fixed (byte* propPtr = this.Properties)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                ulong srcSize = (ulong)srcData.Length;
                ulong decompressedSize = (ulong)dstCount;
                int status = 0;

                int result = SZ_Lzma_v25_01_Dec_LzmaDecode(
                    dstPtr, &decompressedSize, srcPtr, &srcSize, propPtr, (uint)this.Properties.Length, 1, &status);

                if (result == 6 && decompressedSize < (ulong)dstCount)
                    result = 0; // Allow for truncated input if we decompressed less than expected

                if (result != 0)
                {
                    dstCount = 0;
                    return mapResult(result);
                }

                dstCount = (int)decompressedSize;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="LzmaBlock"/>.
        /// </summary>
        internal override void OnDispose()
        {
        }

        private static CompressionResultCode mapResult(int code)
        {
            return code switch
            {
                0 => CompressionResultCode.Success, // SZ_OK
                -1 => CompressionResultCode.InvalidData, // SZ_ERROR_DATA
                -2 => CompressionResultCode.Error, // SZ_ERROR_MEM
                -3 => CompressionResultCode.InvalidData, // SZ_ERROR_CRC
                -4 => CompressionResultCode.NotSupported, // SZ_ERROR_UNSUPPORTED
                -5 => CompressionResultCode.InvalidParameter, // SZ_ERROR_PARAM
                -6 => CompressionResultCode.Error, // SZ_ERROR_INPUT_EOF
                -7 => CompressionResultCode.InsufficientBuffer, // SZ_ERROR_OUTPUT_EOF
                -8 => CompressionResultCode.Error, // SZ_ERROR_READ
                -9 => CompressionResultCode.Error, // SZ_ERROR_WRITE
                -10 => CompressionResultCode.Error, // SZ_ERROR_PROGRESS
                -11 => CompressionResultCode.Error, // SZ_ERROR_FAIL
                -12 => CompressionResultCode.Error, // SZ_ERROR_THREAD
                _ => CompressionResultCode.Error
            };
        }
    }
}


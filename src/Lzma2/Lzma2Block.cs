using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lzma;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Provides a block-based implementation of the LZMA2 compression algorithm.
    /// </summary>
    public class Lzma2Block : CompressionBlock
    {
        private CLzma2EncProps _props;

        /// <summary>
        /// Gets the required output buffer size for compression, as determined by the LZMA2 algorithm.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lzma2Block"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        public Lzma2Block(CompressionOptions options) : base(CompressionAlgorithm.Lzma2, options)
        {
            int blockSize = (int)options.BlockSize!;
            int level = (int)this.CompressionType;
            int threads = options.ThreadCount ?? 1;
            this.Properties = options.InitProperties;
            CLzma2EncProps props = new CLzma2EncProps();
            SZ_Lzma2_v25_01_Enc_Construct(ref _props);
            //init
            props.lzmaProps.level = 5;
            props.lzmaProps.dictSize = props.lzmaProps.mc = 0;
            props.lzmaProps.reduceSize = ulong.MaxValue;
            props.lzmaProps.lc = props.lzmaProps.lp = props.lzmaProps.pb = props.lzmaProps.algo = props.lzmaProps.fb = props.lzmaProps.btMode = props.lzmaProps.numHashBytes = props.lzmaProps.numThreads = -1;
            props.lzmaProps.numHashBytes = 0;
            props.lzmaProps.writeEndMark = 0;
            props.lzmaProps.affinity = 0;
            props.blockSize = 0;
            props.numBlockThreads_Max = -1;
            props.numBlockThreads_Reduced = -1;
            props.numTotalThreads = -1;

            //config
            props.lzmaProps.level = level;
            props.lzmaProps.numThreads = -1;
            props.numBlockThreads_Max = threads;
            props.numBlockThreads_Reduced = -1;
            props.numTotalThreads = threads;

            _props = props;
            // Normalize properties
            SZ_Lzma2_v25_01_Enc_Normalize(ref _props);

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x20; // Adjust for overhead
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using LZMA2.
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

                IntPtr encoder = SZ_Lzma2_v25_01_Enc_Create();
                SZ_Lzma2_v25_01_Enc_SetProps(encoder, ref _props);
                SZ_Lzma2_v25_01_Enc_SetDataSize(encoder, (ulong)srcData.Length);

                // Retrieve LZMA2 encoded property byte
                this.Properties = new byte[] { (byte)SZ_Lzma2_v25_01_Enc_WriteProperties(encoder) };

                ulong compressedSize = (ulong)dstCount;
                int result = SZ_Lzma2_v25_01_Enc_Encode2(
                    encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, IntPtr.Zero);

                SZ_Lzma2_v25_01_Enc_Destroy(encoder);

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
        /// Decompresses the source data block into the destination data block using LZMA2.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal unsafe override CompressionResultCode OnDecompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            if (this.Properties == null || this.Properties.Length == 0)
            {
                dstCount = 0;
                return CompressionResultCode.InvalidParameter;
            }

            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                ulong srcSize = (ulong)srcData.Length;
                ulong decompressedSize = (ulong)dstCount;
                int status = 0;

                int result = SZ_Lzma2_v25_01_Decode(
                    dstPtr, &decompressedSize, srcPtr, &srcSize, this.Properties[0], 1, &status);

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
        /// Releases any resources used by the <see cref="Lzma2Block"/>.
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

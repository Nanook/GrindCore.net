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
            SZ_Lzma2_v24_07_Enc_Construct(ref _props);
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
            SZ_Lzma2_v24_07_Enc_Normalize(ref _props);

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x20; // Adjust for overhead
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using LZMA2.
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

                IntPtr encoder = SZ_Lzma2_v24_07_Enc_Create();
                SZ_Lzma2_v24_07_Enc_SetProps(encoder, ref _props);
                SZ_Lzma2_v24_07_Enc_SetDataSize(encoder, (ulong)srcData.Length);

                // Retrieve LZMA2 encoded property byte
                this.Properties = new byte[] { (byte)SZ_Lzma2_v24_07_Enc_WriteProperties(encoder) };

                ulong compressedSize = (ulong)dstData.Length;
                int result = SZ_Lzma2_v24_07_Enc_Encode2(
                    encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, IntPtr.Zero);

                SZ_Lzma2_v24_07_Enc_Destroy(encoder);

                if (result != 0)
                    throw new InvalidOperationException("LZMA2 Block Compression failed.");

                return (int)compressedSize;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using LZMA2.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if decompression fails.</exception>
        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            if (this.Properties == null || this.Properties.Length == 0)
                throw new InvalidOperationException("LZMA2 Properties must be set and contain at least one byte before decompression.");

            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                ulong srcSize = (ulong)srcData.Length;
                ulong decompressedSize = (ulong)dstData.Length;
                int status = 0;

                int result = SZ_Lzma2_v24_07_Decode(
                    dstPtr, &decompressedSize, srcPtr, &srcSize, this.Properties[0], 1, &status);

                if (result != 0)
                    throw new InvalidOperationException("LZMA2 Block Decompression failed.");

                return (int)decompressedSize;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="Lzma2Block"/>.
        /// </summary>
        internal override void OnDispose()
        {
        }
    }
}

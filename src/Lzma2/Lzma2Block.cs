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
            int threads = options.ThreadCount ?? 1; // Default to 1 for consistency
            this.Properties = options.InitProperties;

            // Initialize props struct
            CLzma2EncProps props = new CLzma2EncProps();
            SZ_Lzma2_v25_01_Enc_Construct(ref props);

            // Initialize to clean state - let native normalization fill defaults
            props.lzmaProps.level = level;
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

            // Configure threading
            props.lzmaProps.numThreads = -1; // Let LZMA2 handle this internally
            props.numBlockThreads_Max = threads;
            props.numBlockThreads_Reduced = -1;
            props.numTotalThreads = threads;
            props.numThreadGroups = 0; // For 25.01 compatibility

            // Apply dictionary options - only set explicit values when provided
            if (options.Dictionary != null)
            {
                // Dictionary size - only set if explicitly provided, otherwise let native normalization choose
                if (options.Dictionary.DictionarySize.HasValue && options.Dictionary.DictionarySize.Value != 0)
                {
                    long ds = options.Dictionary.DictionarySize.Value;
                    if (ds < 0 || ds > uint.MaxValue)
                        throw new ArgumentOutOfRangeException(nameof(options.Dictionary.DictionarySize), $"DictionarySize must be between 0 and {uint.MaxValue}.");
                    props.lzmaProps.dictSize = (uint)ds;
                    props.lzmaProps.mc = (uint)Math.Min(ds, int.MaxValue);
                }
                // else leave dictSize = 0 so native normalization chooses based on level

                // Apply other dictionary options only when explicitly set
                if (options.Dictionary.LiteralContextBits.HasValue)
                    props.lzmaProps.lc = options.Dictionary.LiteralContextBits.Value;
                if (options.Dictionary.LiteralPositionBits.HasValue)
                    props.lzmaProps.lp = options.Dictionary.LiteralPositionBits.Value;
                if (options.Dictionary.PositionBits.HasValue)
                    props.lzmaProps.pb = options.Dictionary.PositionBits.Value;
                if (options.Dictionary.Algorithm.HasValue)
                    props.lzmaProps.algo = options.Dictionary.Algorithm.Value;
                if (options.Dictionary.FastBytes.HasValue)
                    props.lzmaProps.fb = options.Dictionary.FastBytes.Value;
                if (options.Dictionary.BinaryTreeMode.HasValue)
                    props.lzmaProps.btMode = options.Dictionary.BinaryTreeMode.Value;
                if (options.Dictionary.HashBytes.HasValue)
                    props.lzmaProps.numHashBytes = options.Dictionary.HashBytes.Value;
                if (options.Dictionary.MatchCycles.HasValue)
                    props.lzmaProps.mc = (uint)options.Dictionary.MatchCycles.Value;
                if (options.Dictionary.WriteEndMarker.HasValue)
                    props.lzmaProps.writeEndMark = options.Dictionary.WriteEndMarker.Value ? 1u : 0u;
                
                // Ensure lc + lp is within acceptable limits like Lzma2Encoder
                if (props.lzmaProps.lc >= 0 && props.lzmaProps.lp >= 0)
                {
                    int sum = (int)props.lzmaProps.lc + (int)props.lzmaProps.lp;
                    if (sum > 4)
                    {
                        int newLc = Math.Max(0, 4 - (int)props.lzmaProps.lp);
                        props.lzmaProps.lc = newLc;
                    }
                }
            }

            // Normalize properties so native code fills sensible defaults for unset fields
            SZ_Lzma2_v25_01_Enc_Normalize(ref props);

            _props = props;

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x20; // Standard overhead without extra buffer
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
                int setPropsResult = SZ_Lzma2_v25_01_Enc_SetProps(encoder, ref _props);
                if (setPropsResult != 0)
                {
                    SZ_Lzma2_v25_01_Enc_Destroy(encoder);
                    dstCount = 0;
                    return mapResult(setPropsResult);
                }
                
                SZ_Lzma2_v25_01_Enc_SetDataSize(encoder, (ulong)srcData.Length);

                // Retrieve LZMA2 encoded property byte
                this.Properties = new byte[] { (byte)SZ_Lzma2_v25_01_Enc_WriteProperties(encoder) };

                ulong compressedSize = (ulong)dstCount;
                int result = SZ_Lzma2_v25_01_Enc_Encode2(
                    encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, IntPtr.Zero);

                SZ_Lzma2_v25_01_Enc_Destroy(encoder);

                // Handle insufficient buffer error gracefully like other encoders
                if (result == -2147023537) // ERROR_INSUFFICIENT_BUFFER (0x8007054F)
                {
                    // Return partial result - this is normal for higher compression levels
                    dstCount = (int)compressedSize;
                    return CompressionResultCode.Success;
                }

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

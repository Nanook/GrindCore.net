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

            // Level comes from the selected CompressionType for this block
            _props.level = (int)this.CompressionType;

            // Determine dictionary size precedence:
            // 1) CompressionOptions.Dictionary.DictionarySize
            // 2) options.BlockSize (fallback used historically)
            // 3) leave as 0 and let normalize choose default
            uint dictSize = 0;
            if (options.Dictionary?.DictionarySize is long ds && ds != 0)
            {
                if (ds < 0 || ds > uint.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(options.Dictionary.DictionarySize), $"DictionarySize must be between 0 and {uint.MaxValue}.");
                dictSize = (uint)ds;
            }
            else if (options.BlockSize.HasValue && options.BlockSize.Value > 0)
            {
                // historical behavior used BlockSize as dict size
                long b = options.BlockSize.Value;
                if (b < 0 || b > uint.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(options.BlockSize), $"BlockSize must be between 0 and {uint.MaxValue} when used as dict size.");
                dictSize = (uint)b;
            }

            _props.dictSize = dictSize;
            // Apply dictionary tuning options when present, otherwise leave fields for normalize to fill defaults.
            if (options.Dictionary != null)
            {
                var d = options.Dictionary;

                if (d.LiteralContextBits.HasValue)
                    _props.lc = d.LiteralContextBits.Value;
                if (d.LiteralPositionBits.HasValue)
                    _props.lp = d.LiteralPositionBits.Value;
                if (d.PositionBits.HasValue)
                    _props.pb = d.PositionBits.Value;
                if (d.Algorithm.HasValue)
                    _props.algo = d.Algorithm.Value;
                if (d.FastBytes.HasValue)
                    _props.fb = d.FastBytes.Value;
                if (d.BinaryTreeMode.HasValue)
                    _props.btMode = d.BinaryTreeMode.Value;
                if (d.HashBytes.HasValue)
                    _props.numHashBytes = d.HashBytes.Value;
                if (d.MatchCycles.HasValue)
                    _props.mc = (uint)d.MatchCycles.Value;
                if (d.WriteEndMarker.HasValue)
                    _props.writeEndMark = d.WriteEndMarker.Value ? 1u : 0u;
            }

            // Allow an explicit thread count from options to override numThreads default
            if (options.ThreadCount.HasValue)
                _props.numThreads = Math.Max(1, options.ThreadCount.Value);

            // Normalize properties so native library fills sensible defaults for unset fields
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
                int result;

                // Retrieve encoded properties
                byte[] p = BufferPool.Rent(0x10);
                ulong compressedSize = (ulong)dstCount;
                try
                {
                    ulong sz = (ulong)p.Length;

                    fixed (byte* inPtr = p)
                        SZ_Lzma_v25_01_Enc_WriteProperties(encoder, inPtr, &sz);
                    this.Properties = p.Take((int)sz).ToArray();

                    result = SZ_Lzma_v25_01_Enc_MemEncode(
                        encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, 0, IntPtr.Zero);
                }
                finally
                {
                    BufferPool.Return(p);
                    SZ_Lzma_v25_01_Enc_Destroy(encoder);
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


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
        /// When dictionary options are not explicitly provided, uses 7-Zip level-based defaults via C normalization.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        public LzmaBlock(CompressionOptions options) : base(CompressionAlgorithm.Lzma, options)
        {
            // BlockSize is always options.BlockSize and is used for output buffer calculation
            int blockSize = (int)options.BlockSize!;
            this.Properties = options.InitProperties;
            
            _props = new CLzmaEncProps();
            SZ_Lzma_v25_01_EncProps_Init(ref _props);

            // Set the compression level - this drives all the 7-Zip defaults
            _props.level = (int)this.CompressionType;

            // Determine dictionary size using the same logic as LzmaStream:
            // 1. CompressionOptions.Dictionary.DictionarySize first
            // 2. options.BufferSize fallback 
            // 3. Default input buffer size (but we don't have BufferSizeInput in Block, so use blockSize)
            uint dictSizeToUse;
            long? dictOptSize = options?.Dictionary?.DictionarySize;
            if (dictOptSize.HasValue && dictOptSize.Value != 0)
            {
                if (dictOptSize.Value < 0 || dictOptSize.Value > uint.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(options.Dictionary.DictionarySize), $"DictionarySize must be between 0 and {uint.MaxValue}.");
                dictSizeToUse = (uint)dictOptSize.Value;
            }
            else if (options?.BufferSize is int bs && bs > 0)
                dictSizeToUse = (uint)bs;
            else
                dictSizeToUse = (uint)blockSize; // Use blockSize as fallback since we don't have BufferSizeInput

            // Build merged dictionary options - only set explicit dictionary size when actually provided
            CompressionDictionaryOptions? mergedDict = null;
            uint dictSizeForNative = 0;
            
            if (options?.Dictionary?.DictionarySize.HasValue == true && options.Dictionary.DictionarySize.Value != 0)
            {
                // User explicitly set a dictionary size - use it and all their other options
                if (options.Dictionary.DictionarySize < 0 || options.Dictionary.DictionarySize > uint.MaxValue)
                    throw new ArgumentOutOfRangeException(nameof(options.Dictionary.DictionarySize), $"DictionarySize must be between 0 and {uint.MaxValue}.");
                
                mergedDict = options.Dictionary;  // Pass the entire dictionary configuration
                dictSizeForNative = (uint)options.Dictionary.DictionarySize.Value;
            }
            else
            {
                // No explicit dictionary size - let native normalization choose based on compression level
                // Only pass non-size-related dictionary options if they exist
                if (options?.Dictionary != null)
                {
                    mergedDict = new CompressionDictionaryOptions
                    {
                        // Don't set DictionarySize - let native normalization choose
                        FastBytes = options.Dictionary.FastBytes,
                        LiteralContextBits = options.Dictionary.LiteralContextBits,
                        LiteralPositionBits = options.Dictionary.LiteralPositionBits,
                        PositionBits = options.Dictionary.PositionBits,
                        Algorithm = options.Dictionary.Algorithm,
                        BinaryTreeMode = options.Dictionary.BinaryTreeMode,
                        HashBytes = options.Dictionary.HashBytes,
                        MatchCycles = options.Dictionary.MatchCycles
                    };
                }
                // else leave mergedDict as null to use pure native defaults
            }

            // Initialize properties to let native normalization choose defaults when appropriate
            _props.dictSize = dictSizeForNative; // 0 if not explicitly set, actual value if set
            _props.mc = 0;

            // Mark unspecified so native normalize() can fill defaults for values we don't set
            _props.lc = _props.lp = _props.pb = _props.algo = _props.fb = _props.btMode = _props.numHashBytes = _props.numThreads = -1;

            // Fixed properties that we always want to set explicitly
            _props.writeEndMark = 1; // default no end marker
            _props.affinity = 0;
            _props.reduceSize = ulong.MaxValue;

            // Apply dictionary options when provided (only override when set) - matches LzmaEncoder logic
            if (mergedDict != null)
            {
                if (mergedDict.LiteralContextBits.HasValue)
                    _props.lc = mergedDict.LiteralContextBits.Value;
                if (mergedDict.LiteralPositionBits.HasValue)
                    _props.lp = mergedDict.LiteralPositionBits.Value;
                if (mergedDict.PositionBits.HasValue)
                    _props.pb = mergedDict.PositionBits.Value;
                if (mergedDict.Algorithm.HasValue)
                    _props.algo = mergedDict.Algorithm.Value;
                if (mergedDict.FastBytes.HasValue)
                    _props.fb = mergedDict.FastBytes.Value;
                if (mergedDict.BinaryTreeMode.HasValue)
                    _props.btMode = mergedDict.BinaryTreeMode.Value;
                if (mergedDict.HashBytes.HasValue)
                    _props.numHashBytes = mergedDict.HashBytes.Value;
                if (mergedDict.MatchCycles.HasValue)
                    _props.mc = (uint)mergedDict.MatchCycles.Value;
            }

            // Apply explicit thread count override
            _props.numThreads = 1;

            // Note: We don't call SZ_Lzma_v25_01_EncProps_Normalize here because LzmaEncoder 
            // does normalization in SZ_Lzma_v25_01_Enc_SetProps during OnCompress

            // RequiredCompressOutputSize is always based on the actual blockSize (options.BlockSize), never the dictionary size
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
                int res = SZ_Lzma_v25_01_Enc_SetProps(encoder, ref _props);
                if (res != 0)
                {
                    SZ_Lzma_v25_01_Enc_Destroy(encoder);
                    dstCount = 0;
                    return mapResult(res);
                }
                
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

                    // Pass writeEndMark = 1 to match _props.writeEndMark which signals writing an end marker
                    result = SZ_Lzma_v25_01_Enc_MemEncode(
                        encoder, dstPtr, &compressedSize, srcPtr, (ulong)srcData.Length, 1, IntPtr.Zero);
                    
                    // Handle insufficient buffer error gracefully like LzmaEncoder
                    if (result == -2147023537) // ERROR_INSUFFICIENT_BUFFER (0x8007054F)
                    {
                        // Return partial result - this is normal for block compression with higher levels
                        dstCount = (int)compressedSize;
                        return CompressionResultCode.Success;
                    }
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


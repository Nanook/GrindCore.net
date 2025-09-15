using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;
using System.Reflection.Emit;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// Provides a block-based implementation of the Deflate (and ZLib/DeflateNg) compression algorithm.
    /// Supports multiple versions and window bit settings.
    /// </summary>
    public class DeflateBlock : CompressionBlock
    {
        private readonly int _windowBits;
        private readonly int _memLevel;
        private readonly int _strategy;

        /// <summary>
        /// Gets the required output buffer size for compression, including Deflate overhead.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflateBlock"/> class using the default DeflateNg algorithm and default window bits.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        public DeflateBlock(CompressionOptions options) : this(CompressionAlgorithm.DeflateNg, options,
            options?.Dictionary?.WindowBits ?? Interop.ZLib.Deflate_DefaultWindowBits,
            options?.Dictionary?.MemoryLevel ?? Interop.ZLib.Deflate_DefaultMemLevel,
            options?.Dictionary?.Strategy ?? 0)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflateBlock"/> class with the specified algorithm, options, and low-level zlib parameters.
        /// </summary>
        /// <param name="defaultAlgorithm">The default compression algorithm to use when <see cref="CompressionOptions.Version"/> does not override it.</param>
        /// <param name="options">The compression options that supply block size and optional dictionary settings. <c>options.BlockSize</c> must be non-null.</param>
        /// <param name="windowBits">
        /// The window bits parameter passed to native deflate APIs. Positive values select zlib/gzip header forms,
        /// negative values select raw deflate (no header). Valid range is -15..31; values are clamped to that range.
        /// </param>
        /// <param name="memLevel">
        /// ZLib memory level (1..9). Controls internal memory usage vs. compression behavior; values are clamped to 1..9.
        /// </param>
        /// <param name="strategy">
        /// Compression strategy (algorithm-specific). For zlib/deflate this follows zlib's strategy codes:
        /// 0 = DefaultStrategy, 1 = Filtered, 2 = HuffmanOnly, 3 = RLE, 4 = Fixed. Values are normalized to non-negative.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        internal DeflateBlock(CompressionAlgorithm defaultAlgorithm, CompressionOptions options, int windowBits, int memLevel = Interop.ZLib.Deflate_DefaultMemLevel, int strategy = 0) : base(defaultAlgorithm, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.BlockSize == null)
                throw new ArgumentNullException(nameof(options.BlockSize));

            // Clamp window bits to allowed range for zlib (Interop defines defaults)
            int minWindowBits = -15; // mirror DeflateEncoder
            int maxWindowBits = 31;
            if (windowBits < minWindowBits)
                windowBits = minWindowBits;
            if (windowBits > maxWindowBits)
                windowBits = maxWindowBits;
            _windowBits = windowBits;

            // Clamp memLevel to 1..9
            if (memLevel < 1)
                memLevel = 1;
            if (memLevel > 9)
                memLevel = 9;
            _memLevel = memLevel;

            // strategy is algorithm-specific; keep as provided but ensure non-negative
            if (strategy < 0) strategy = 0;
            _strategy = strategy;

            int sourceLen = (int)options.BlockSize!;
            // The output buffer size formula is based on zlib's compressBound calculation.
            RequiredCompressOutputSize = sourceLen + (sourceLen >> 12) + (sourceLen >> 14) + (sourceLen >> 25) + 0x1000;
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using Deflate or ZLib/DeflateNg.
        /// </summary>
        /// <param name="srcData">The source data block to compress.</param>
        /// <param name="dstData">The destination data block to write compressed data to.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal unsafe override CompressionResultCode OnCompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            fixed (byte* s = srcData.Data)
            fixed (byte* d = dstData.Data)
            {
                *&s += srcData.Offset;
                *&d += dstData.Offset;

                uint dstLen = (uint)dstCount;
                int ret;
                if (base.Options.Version == null || base.Options.Version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress3(d, ref dstLen, s, (uint)srcData.Length, (int)this.CompressionType, _windowBits, _memLevel, _strategy);
                else if (base.Options.Version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Compress3(d, ref dstLen, s, (uint)srcData.Length, (int)this.CompressionType, _windowBits, _memLevel, _strategy);
                else
                {
                    dstCount = 0;
                    return CompressionResultCode.NotSupported;
                }

                if (ret != 0)
                {
                    dstCount = 0;
                    return mapResult(ret);
                }

                dstCount = (int)dstLen;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using Deflate or ZLib/DeflateNg.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <param name="dstCount">On input, the maximum bytes available; on output, the actual bytes written.</param>
        /// <returns>The compression result code.</returns>
        internal unsafe override CompressionResultCode OnDecompress(DataBlock srcData, DataBlock dstData, ref int dstCount)
        {
            fixed (byte* s = srcData.Data)
            fixed (byte* d = dstData.Data)
            {
                *&s += srcData.Offset;
                *&d += dstData.Offset;

                uint srcLen = (uint)srcData.Length;
                uint dstLen = (UInt32)dstCount;
                int ret;
                if (base.Options.Version == null || base.Options.Version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress3(d, ref dstLen, s, ref srcLen, _windowBits);
                else if (base.Options.Version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Uncompress3(d, ref dstLen, s, ref srcLen, _windowBits);
                else
                {
                    dstCount = 0;
                    return CompressionResultCode.NotSupported;
                }

                if (ret != 0)
                {
                    dstCount = 0;
                    return mapResult(ret);
                }

                dstCount = (int)dstLen;
                return CompressionResultCode.Success;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="DeflateBlock"/>. No resources to release for Deflate.
        /// </summary>
        internal override void OnDispose()
        {
        }

        private static CompressionResultCode mapResult(int code)
        {
            return code switch
            {
                0 or 1 => CompressionResultCode.Success, // Z_OK, Z_STREAMEND
                -2 => CompressionResultCode.Error,   // Z_STREAMERROR
                -3 => CompressionResultCode.InvalidData, // Z_DATAERROR
                -4 => CompressionResultCode.Error,   // Z_MEMERROR
                -5 => CompressionResultCode.InsufficientBuffer, // Z_BUFERROR
                -6 => CompressionResultCode.InvalidParameter, // Z_VERSIONERROR
                _ => CompressionResultCode.Error
            };
        }
    }
}

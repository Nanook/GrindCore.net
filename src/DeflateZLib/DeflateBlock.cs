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

        /// <summary>
        /// Gets the required output buffer size for compression, including Deflate overhead.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflateBlock"/> class using the default DeflateNg algorithm and default window bits.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        public DeflateBlock(CompressionOptions options) : this(CompressionAlgorithm.DeflateNg, options, Interop.ZLib.Deflate_DefaultWindowBits)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeflateBlock"/> class with the specified algorithm, options, and window bits.
        /// </summary>
        /// <param name="defaultAlgorithm">The default compression algorithm.</param>
        /// <param name="options">The compression options to use.</param>
        /// <param name="windowBits">The window bits to use for Deflate.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        internal DeflateBlock(CompressionAlgorithm defaultAlgorithm, CompressionOptions options, int windowBits) : base(defaultAlgorithm, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.BlockSize == null)
                throw new ArgumentNullException(nameof(options.BlockSize));

            _windowBits = windowBits;
            int sourceLen = (int)options.BlockSize!;
            // The output buffer size formula is based on zlib's compressBound calculation.
            RequiredCompressOutputSize = sourceLen + (sourceLen >> 12) + (sourceLen >> 14) + (sourceLen >> 25) + 13;
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using Deflate or ZLib/DeflateNg.
        /// </summary>
        /// <param name="srcData">The source data block to compress.</param>
        /// <param name="dstData">The destination data block to write compressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* s = srcData.Data)
            fixed (byte* d = dstData.Data)
            {
                *&s += srcData.Offset;
                *&d += dstData.Offset;

                uint dstLen = (uint)dstData.Length;
                int ret;
                if (base.Options.Version == null || base.Options.Version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress3(d, ref dstLen, s, (uint)srcData.Length, (int)this.CompressionType, _windowBits, 9, 0);
                else if (base.Options.Version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Compress3(d, ref dstLen, s, (uint)srcData.Length, (int)this.CompressionType, _windowBits, 9, 0);
                else
                    throw new Exception($"{base.Options.Version.Algorithm} version {base.Options.Version.Version} is not supported");
                return (int)dstLen;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using Deflate or ZLib/DeflateNg.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* s = srcData.Data)
            fixed (byte* d = dstData.Data)
            {
                *&s += srcData.Offset;
                *&d += dstData.Offset;

                uint srcLen = (uint)srcData.Length;
                uint dstLen = (uint)dstData.Length;
                int ret;
                if (base.Options.Version == null || base.Options.Version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress3(d, ref dstLen, s, ref srcLen, _windowBits);
                else if (base.Options.Version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Uncompress3(d, ref dstLen, s, ref srcLen, _windowBits);
                else
                    throw new Exception($"{base.Options.Version.Algorithm} version {base.Options.Version.Version} is not supported");
                return (int)dstLen;
            }
        }

        /// <summary>
        /// Releases any resources used by the <see cref="DeflateBlock"/>. No resources to release for Deflate.
        /// </summary>
        internal override void OnDispose()
        {
        }
    }
}

using Nanook.GrindCore.Brotli;
using Nanook.GrindCore.Copy;
using Nanook.GrindCore.DeflateZLib;
using Nanook.GrindCore.FastLzma2;
using Nanook.GrindCore.Lz4;
using Nanook.GrindCore.Lzma;
using Nanook.GrindCore.ZStd;
using System;
using System.Collections.Generic;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Factory class for creating <see cref="CompressionBlock"/> instances for various compression algorithms.
    /// </summary>
    public class CompressionBlockFactory
    {
        private static readonly Dictionary<CompressionAlgorithm, Func<CompressionOptions, CompressionBlock>> _BlockCreators = new Dictionary<CompressionAlgorithm, Func<CompressionOptions, CompressionBlock>>()
        {
            { CompressionAlgorithm.Copy, (options) => new CopyBlock(options) },
            { CompressionAlgorithm.Brotli, (options) => new BrotliBlock(options) },
            { CompressionAlgorithm.Deflate, (options) => new DeflateBlock(options) },
            { CompressionAlgorithm.DeflateNg, (options) => new DeflateBlock(options) },
            { CompressionAlgorithm.FastLzma2, (options) => new FastLzma2Block(options) },
            { CompressionAlgorithm.GZip, (options) => new GZipBlock(options) },
            { CompressionAlgorithm.GZipNg, (options) => new GZipBlock(options) },
            { CompressionAlgorithm.Lz4, (options) => new Lz4Block(options) },
            { CompressionAlgorithm.Lzma, (options) => new LzmaBlock(options) },
            { CompressionAlgorithm.Lzma2, (options) => new Lzma2Block(options) },
            { CompressionAlgorithm.ZLib, (options) => new ZLibBlock(options) },
            { CompressionAlgorithm.ZLibNg, (options) => new ZLibBlock(options) },
            { CompressionAlgorithm.ZStd, (options) => new ZStdBlock(options) }
        };

        /// <summary>
        /// Creates a <see cref="CompressionBlock"/> for the specified algorithm and options.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use.</param>
        /// <param name="options">The compression options to use.</param>
        /// <returns>A <see cref="CompressionBlock"/> instance for the specified algorithm.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the algorithm is not supported.</exception>
        public static CompressionBlock Create(CompressionAlgorithm algorithm, CompressionOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            var s = create(algorithm, options);
            return s;
        }

        /// <summary>
        /// Creates a <see cref="CompressionBlock"/> for the specified algorithm, compression type, block size, and version.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use.</param>
        /// <param name="type">The compression type to use.</param>
        /// <param name="blockSize">The block size to use.</param>
        /// <param name="leaveOpen">Whether to leave the underlying stream open after use. Defaults to false.</param>
        /// <param name="version">The compression version to use, or null for default.</param>
        /// <returns>A <see cref="CompressionBlock"/> instance for the specified algorithm.</returns>
        /// <exception cref="ArgumentException">Thrown if the algorithm is not supported.</exception>
        public static CompressionBlock Create(CompressionAlgorithm algorithm, CompressionType type, int blockSize, bool leaveOpen = false, CompressionVersion? version = null)
        {
            var s = create(algorithm, new CompressionOptions() { Type = type, LeaveOpen = leaveOpen, BlockSize = blockSize, Version = version });
            return s;
        }

        /// <summary>
        /// Internal helper to create a <see cref="CompressionBlock"/> for the specified algorithm and options.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use.</param>
        /// <param name="options">The compression options to use.</param>
        /// <returns>A <see cref="CompressionBlock"/> instance for the specified algorithm.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the algorithm is not supported.</exception>
        private static CompressionBlock create(CompressionAlgorithm algorithm, CompressionOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (_BlockCreators.TryGetValue(algorithm, out var creator))
                return creator(options);

            throw new ArgumentException("Unsupported stream algorithm", nameof(algorithm));
        }
    }
}

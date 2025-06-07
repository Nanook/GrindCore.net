using Nanook.GrindCore.Brotli;
using Nanook.GrindCore.DeflateZLib;
using Nanook.GrindCore.FastLzma2;
using Nanook.GrindCore.GZip;
using Nanook.GrindCore.Lz4;
using Nanook.GrindCore.Lzma;
using Nanook.GrindCore.ZLib;
using Nanook.GrindCore.ZStd;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Factory class for creating <see cref="CompressionStream"/> instances for various compression algorithms.
    /// </summary>
    public class CompressionStreamFactory
    {
        private static readonly Dictionary<CompressionAlgorithm, Func<Stream, CompressionOptions, CompressionStream>> streamCreators = new Dictionary<CompressionAlgorithm, Func<Stream, CompressionOptions, CompressionStream>>()
        {
            { CompressionAlgorithm.Copy, (stream, options) => new CopyStream(stream, options) },
            { CompressionAlgorithm.Brotli, (stream, options) => new BrotliStream(stream, options) },
            { CompressionAlgorithm.Deflate, (stream, options) => new DeflateStream(stream, options) },
            { CompressionAlgorithm.DeflateNg, (stream, options) => new DeflateStream(stream, options) },
            { CompressionAlgorithm.FastLzma2, (stream, options) => new FastLzma2Stream(stream, options) },
            { CompressionAlgorithm.GZip, (stream, options) => new GZipStream(stream, options) },
            { CompressionAlgorithm.GZipNg, (stream, options) => new GZipStream(stream, options) },
            { CompressionAlgorithm.Lz4, (stream, options) => new Lz4Stream(stream, options) },
            { CompressionAlgorithm.Lzma, (stream, options) => new LzmaStream(stream, options) },
            { CompressionAlgorithm.Lzma2, (stream, options) => new Lzma2Stream(stream, options) },
            { CompressionAlgorithm.ZLib, (stream, options) => new ZLibStream(stream, options) },
            { CompressionAlgorithm.ZLibNg, (stream, options) => new ZLibStream(stream, options) },
            { CompressionAlgorithm.ZStd, (stream, options) => new ZStdStream(stream, options) }
        };

        /// <summary>
        /// Creates a <see cref="CompressionStream"/> for the specified algorithm, stream, and options.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use.</param>
        /// <param name="stream">The base stream to wrap.</param>
        /// <param name="options">The compression options to use.</param>
        /// <returns>A <see cref="CompressionStream"/> instance for the specified algorithm.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the algorithm is not supported.</exception>
        public static CompressionStream Create(CompressionAlgorithm algorithm, Stream stream, CompressionOptions options)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            var s = create(algorithm, stream, options);
            return s;
        }

        /// <summary>
        /// Creates a <see cref="CompressionStream"/> for the specified algorithm, stream, compression type, and version.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use.</param>
        /// <param name="stream">The base stream to wrap.</param>
        /// <param name="type">The compression type to use.</param>
        /// <param name="leaveOpen">Whether to leave the underlying stream open after use. Defaults to false.</param>
        /// <param name="version">The compression version to use, or null for default.</param>
        /// <returns>A <see cref="CompressionStream"/> instance for the specified algorithm.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the algorithm is not supported.</exception>
        public static CompressionStream Create(CompressionAlgorithm algorithm, Stream stream, CompressionType type, bool leaveOpen = false, CompressionVersion? version = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            var s = create(algorithm, stream, new CompressionOptions() { Type = type, LeaveOpen = leaveOpen, Version = version });
            return s;
        }

        /// <summary>
        /// Internal helper to create a <see cref="CompressionStream"/> for the specified algorithm, stream, and options.
        /// </summary>
        /// <param name="algorithm">The compression algorithm to use.</param>
        /// <param name="stream">The base stream to wrap.</param>
        /// <param name="options">The compression options to use.</param>
        /// <returns>A <see cref="CompressionStream"/> instance for the specified algorithm.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the algorithm is not supported.</exception>
        private static CompressionStream create(CompressionAlgorithm algorithm, Stream stream, CompressionOptions options)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (streamCreators.TryGetValue(algorithm, out var creator))
                return creator(stream, options);

            throw new ArgumentException("Unsupported stream algorithm", nameof(algorithm));
        }
    }
}

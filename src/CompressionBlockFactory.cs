using Nanook.GrindCore.Brotli;
using Nanook.GrindCore.Copy;
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

    public class CompressionBlockFactory
    {
        private static readonly Dictionary<CompressionAlgorithm, Func<CompressionAlgorithm, CompressionOptions, CompressionBlock>> blockCreators = new Dictionary<CompressionAlgorithm, Func<CompressionAlgorithm, CompressionOptions, CompressionBlock>>()
        {
            { CompressionAlgorithm.Copy, (algorithm, options) => new CopyBlock(algorithm, options) },
            //{ CompressionAlgorithm.GZip, (stream, options) => new GZipBlock(algorithm, options) },
            { CompressionAlgorithm.ZLib, (algorithm, options) => new ZLibBlock(algorithm, options) },
            { CompressionAlgorithm.Deflate, (algorithm, options) => new DeflateBlock(algorithm, options) },
            //{ CompressionAlgorithm.GZipNg, (algorithm, options) => new GZipBlock(algorithm, options) },
            { CompressionAlgorithm.ZLibNg, (algorithm, options) => new ZLibBlock(algorithm, options) },
            { CompressionAlgorithm.DeflateNg, (algorithm, options) => new DeflateBlock(algorithm, options) },
            { CompressionAlgorithm.Brotli, (algorithm, options) => new BrotliBlock(algorithm, options) },
            { CompressionAlgorithm.Lzma, (algorithm, options) => new LzmaBlock(algorithm, options) },
            { CompressionAlgorithm.Lzma2, (algorithm, options) => new Lzma2Block(algorithm, options) },
            { CompressionAlgorithm.FastLzma2, (algorithm, options) => new FastLzma2Block(algorithm, options) },
            { CompressionAlgorithm.Lz4, (algorithm, options) => new Lz4Block(algorithm, options) },
            { CompressionAlgorithm.ZStd, (algorithm, options) => new ZStdBlock(algorithm, options) }
        };

        public static CompressionBlock Create(CompressionAlgorithm algorithm, CompressionOptions options)
        {
            var s = create(algorithm, options);
            return s;
        }

        public static CompressionBlock Create(CompressionAlgorithm algorithm, CompressionType type, int blockSize, bool leaveOpen = false, CompressionVersion? version = null)
        {
            var s = create(algorithm, new CompressionOptions() { Type = type, LeaveOpen = leaveOpen, BlockSize = blockSize, Version = version });
            return s;
        }

        private static CompressionBlock create(CompressionAlgorithm algorithm, CompressionOptions options)
        {
            if (blockCreators.TryGetValue(algorithm, out var creator))
                return creator(algorithm, options);

            throw new ArgumentException("Unsupported stream algorithm", nameof(algorithm));
        }
    }
}

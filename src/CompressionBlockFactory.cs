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
        private static readonly Dictionary<CompressionAlgorithm, Func<CompressionOptions, CompressionBlock>> blockCreators = new Dictionary<CompressionAlgorithm, Func<CompressionOptions, CompressionBlock>>()
        {
            { CompressionAlgorithm.Copy, (options) => new CopyBlock(options) },
            //{ CompressionAlgorithm.GZip, (options) => new GZipBlock(options) },
            { CompressionAlgorithm.ZLib, (options) => new ZLibBlock(options) },
            { CompressionAlgorithm.Deflate, (options) => new DeflateBlock(options) },
            //{ CompressionAlgorithm.GZipNg, (options) => new GZipBlock(options) },
            { CompressionAlgorithm.ZLibNg, (options) => new ZLibBlock(options) },
            { CompressionAlgorithm.DeflateNg, (options) => new DeflateBlock(options) },
            { CompressionAlgorithm.Brotli, (options) => new BrotliBlock(options) },
            { CompressionAlgorithm.Lzma, (options) => new LzmaBlock(options) },
            { CompressionAlgorithm.Lzma2, (options) => new Lzma2Block(options) },
            { CompressionAlgorithm.FastLzma2, (options) => new FastLzma2Block(options) },
            { CompressionAlgorithm.Lz4, (options) => new Lz4Block(options) },
            { CompressionAlgorithm.ZStd, (options) => new ZStdBlock(options) }
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
                return creator(options);

            throw new ArgumentException("Unsupported stream algorithm", nameof(algorithm));
        }
    }
}

using Nanook.GrindCore.Brotli;
using Nanook.GrindCore.DeflateZLib;
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
    public enum CompressionAlgorithm
    {
        Copy,
        GZip,
        ZLib,
        Deflate,
        GZipNg,
        ZLibNg,
        DeflateNg,
        Brotli,
        Lzma,
        Lzma2,
        FastLzma2,
        Lz4,
        ZStd
    }

    public class CompressionStreamFactory
    {
        private static readonly Dictionary<CompressionAlgorithm, Func<Stream, CompressionOptions, CompressionStream>> streamCreators = new Dictionary<CompressionAlgorithm, Func<Stream, CompressionOptions, CompressionStream>>()
        {
            { CompressionAlgorithm.Copy, (stream, options) => new CopyStream(stream, options) },
            { CompressionAlgorithm.GZip, (stream, options) => new GZipStream(stream, options) },
            { CompressionAlgorithm.ZLib, (stream, options) => new ZLibStream(stream, options) },
            { CompressionAlgorithm.Deflate, (stream, options) => new DeflateStream(stream, options) },
            { CompressionAlgorithm.GZipNg, (stream, options) => new GZipStream(stream, options) },
            { CompressionAlgorithm.ZLibNg, (stream, options) => new ZLibStream(stream, options) },
            { CompressionAlgorithm.DeflateNg, (stream, options) => new DeflateStream(stream, options) },
            { CompressionAlgorithm.Brotli, (stream, options) => new BrotliStream(stream, options) },
            { CompressionAlgorithm.Lzma, (stream, options) => new LzmaStream(stream, options) },
            { CompressionAlgorithm.Lzma2, (stream, options) => new Lzma2Stream(stream, options) },
            { CompressionAlgorithm.FastLzma2, (stream, options) => new FastLzma2Stream(stream, options) },
            { CompressionAlgorithm.Lz4, (stream, options) => new Lz4Stream(stream, options) },
            { CompressionAlgorithm.ZStd, (stream, options) => new ZStdStream(stream, options) }
        };

        public static CompressionStream Create(CompressionAlgorithm algorithm, Stream stream, CompressionOptions options)
        {
            var s = create(algorithm, stream, options);
            return s;
        }

        public static CompressionStream Create(CompressionAlgorithm algorithm, Stream stream, CompressionType type, bool leaveOpen = false, CompressionVersion? version = null)
        {
            var s = create(algorithm, stream, new CompressionOptions() { Type = type, LeaveOpen = leaveOpen, Version = version });
            return s;
        }

        private static CompressionStream create(CompressionAlgorithm algorithm, Stream stream, CompressionOptions options)
        {
            if (streamCreators.TryGetValue(algorithm, out var creator))
                return creator(stream, options);

            throw new ArgumentException("Unsupported stream algorithm", nameof(algorithm));
        }

        public static byte[] Process(CompressionAlgorithm algorithm, byte[] data, CompressionOptions options)
        {
            return Process(algorithm, data, options, out _);
        }
        public static byte[] Process(CompressionAlgorithm algorithm, byte[] data, CompressionOptions options, out byte[]? properties)
        {
            if (options.Type != CompressionType.Decompress)
            {
                using (var outputStream = new MemoryStream())
                {
                    using (var compressionStream = Create(algorithm, outputStream, options))
                    {
                        compressionStream.Write(data, 0, data.Length);
                        properties = compressionStream.Properties;
                        compressionStream.Complete();
                    }
                    return outputStream.ToArray();
                }
            }
            else
            {
                using (var outputStream = new MemoryStream())
                {
                    properties = null;
                    using (var inputStream = new MemoryStream(data))
                    using (var decompressionStream = Create(algorithm, inputStream, options))
                        decompressionStream.CopyTo(outputStream);
                    return outputStream.ToArray();
                }
            }
        }
    }
}

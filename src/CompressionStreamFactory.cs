using Nanook.GrindCore.Brotli;
using Nanook.GrindCore.DeflateZLib;
using Nanook.GrindCore.GZip;
using Nanook.GrindCore.Lzma;
using Nanook.GrindCore.ZLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;

namespace Nanook.GrindCore
{
    public enum CompressionAlgorithm
    {
        GZip,
        ZLib,
        Deflate,
        GZipNg,
        ZLibNg,
        DeflateNg,
        Brotli,
        Lzma,
        Lzma2,
        FastLzma2
    }

    public class CompressionStreamFactory
    {
        private static readonly Dictionary<CompressionAlgorithm, Func<Stream, CompressionType, bool, CompressionVersion?, Stream>> streamCreators = new Dictionary<CompressionAlgorithm, Func<Stream, CompressionType, bool, CompressionVersion?, Stream>>()
        {
            { CompressionAlgorithm.GZip, (stream, type, leaveOpen, version) => new GZipStream(stream, type, leaveOpen, version ?? CompressionVersion.ZLibLatest()) },
            { CompressionAlgorithm.ZLib, (stream, type, leaveOpen, version) => new ZLibStream(stream, type, leaveOpen,version ?? CompressionVersion.ZLibLatest()) },
            { CompressionAlgorithm.Deflate, (stream, type, leaveOpen, version) => new DeflateStream(stream, type, leaveOpen, version ?? CompressionVersion.ZLibLatest()) },
            { CompressionAlgorithm.GZipNg, (stream, type, leaveOpen, version) => new GZipStream(stream, type, leaveOpen, version ?? CompressionVersion.ZLibNgLatest()) },
            { CompressionAlgorithm.ZLibNg, (stream, type, leaveOpen, version) => new ZLibStream(stream, type, leaveOpen, version ?? CompressionVersion.ZLibNgLatest()) },
            { CompressionAlgorithm.DeflateNg, (stream, type, leaveOpen, version) => new DeflateStream(stream, type, leaveOpen, version ?? CompressionVersion.ZLibNgLatest()) },
            { CompressionAlgorithm.Brotli, (stream, type, leaveOpen, version) => new BrotliStream(stream, type, leaveOpen, version ??  CompressionVersion.BrotliLatest()) },
            { CompressionAlgorithm.Lzma, (stream, type, leaveOpen, version) => new LzmaStream(stream, type, leaveOpen, version ?? CompressionVersion.LzmaLatest()) },
            { CompressionAlgorithm.Lzma2, (stream, type, leaveOpen, version) => new Lzma2Stream(stream, type, leaveOpen, version ?? CompressionVersion.Lzma2Latest()) },
            { CompressionAlgorithm.FastLzma2, (stream, type, leaveOpen, version) => new FastLzma2Stream(stream, type, leaveOpen, new CompressionParameters(4), version ?? CompressionVersion.FastLzma2Latest()) }
        };

        public static Stream Create(CompressionAlgorithm algorithm, Stream stream, CompressionType type, bool leaveOpen = false, CompressionVersion? version = null)
        {
            var s = create(algorithm, stream, type, leaveOpen, version);
            return s;
        }

        private static Stream create(CompressionAlgorithm algorithm, Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null)
        {
            if (streamCreators.TryGetValue(algorithm, out var creator))
                return creator(stream, type, leaveOpen, version);

            throw new ArgumentException("Unsupported stream algorithm", nameof(algorithm));
        }

        public static byte[] Compress(CompressionAlgorithm algorithm, Stream inputStream, CompressionType type, bool leaveOpen = false, CompressionVersion? version = null)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var compressionStream = Create(algorithm, outputStream, type, leaveOpen, version))
                    inputStream.CopyTo(compressionStream);
                return outputStream.ToArray();
            }
        }

        public static byte[] Decompress(CompressionAlgorithm type, Stream inputStream, bool leaveOpen = false, CompressionVersion? version = null)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var decompressionStream = Create(type, inputStream, CompressionType.Decompress, leaveOpen, version))
                    decompressionStream.CopyTo(outputStream);
                return outputStream.ToArray();
            }
        }

        public static byte[] Compress(CompressionAlgorithm type, byte[] data, CompressionType level, bool leaveOpen = false, CompressionVersion? version = null)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var compressionStream = Create(type, outputStream, level, leaveOpen, version))
                {
                    compressionStream.Write(data, 0, data.Length);
                }
                return outputStream.ToArray();
            }
        }

        public static byte[] Decompress(CompressionAlgorithm type, byte[] data, bool leaveOpen = false, CompressionVersion? version = null)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var inputStream = new MemoryStream(data))
                using (var decompressionStream = Create(type, inputStream, CompressionType.Decompress, leaveOpen, version))
                {
                    decompressionStream.CopyTo(outputStream);
                }
                return outputStream.ToArray();
            }
        }
    }
}

using Nanook.GrindCore.Brotli;
using Nanook.GrindCore.DeflateZLib;
using Nanook.GrindCore.GZip;
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
        Brotli
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
            { CompressionAlgorithm.Brotli, (stream, type, leaveOpen, version) => new BrotliStream(stream, type, leaveOpen,version ??  CompressionVersion.BrotliLatest()) }
        };

        public static Stream Create(CompressionAlgorithm algorithm, Stream stream, CompressionType type, bool leaveOpen = false, CompressionVersion? version = null)
        {
            return create(algorithm, stream, type, leaveOpen, version);
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

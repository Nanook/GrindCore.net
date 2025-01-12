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
    public enum CompressionStreamType
    {
        GZip,
        ZLib,
        Deflate,
        Brotli
    }

    public class CompressionStreamFactory
    {
        private static readonly Dictionary<CompressionStreamType, Func<Stream, CompressionMode, CompressionLevel, bool, Stream>> streamCreators = new Dictionary<CompressionStreamType, Func<Stream, CompressionMode, CompressionLevel, bool, Stream>>()
        {
            { CompressionStreamType.GZip, (stream, mode, level, leaveOpen) => mode == CompressionMode.Compress ? new GZipStream(stream, level, leaveOpen) : new GZipStream(stream, mode, leaveOpen) },
            { CompressionStreamType.ZLib, (stream, mode, level, leaveOpen) => mode == CompressionMode.Compress ? new ZLibStream(stream, level, leaveOpen) : new ZLibStream(stream, mode, leaveOpen) },
            { CompressionStreamType.Deflate, (stream, mode, level, leaveOpen) => mode == CompressionMode.Compress ? new DeflateStream(stream, level, leaveOpen) : new DeflateStream(stream, mode, leaveOpen) },
            { CompressionStreamType.Brotli, (stream, mode, level, leaveOpen) => mode == CompressionMode.Compress ? new BrotliStream(stream, level, leaveOpen) : new BrotliStream(stream, mode, leaveOpen) }
        };

        public static Stream Create(CompressionStreamType type, Stream stream, CompressionLevel level, bool leaveOpen = false)
        {
            return create(type, stream, CompressionMode.Compress, level, leaveOpen);
        }

        public static Stream Create(CompressionStreamType type, Stream stream, CompressionMode mode, bool leaveOpen = false)
        {
            return create(type, stream, mode, CompressionLevel.Optimal, leaveOpen);
        }

        private static Stream create(CompressionStreamType type, Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            if (streamCreators.TryGetValue(type, out var creator))
                return creator(stream, mode, level, leaveOpen);

            throw new ArgumentException("Unsupported stream type", nameof(type));
        }

        public static byte[] Compress(CompressionStreamType type, Stream inputStream, CompressionLevel level, bool leaveOpen = false)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var compressionStream = Create(type, outputStream, level, leaveOpen))
                {
                    inputStream.CopyTo(compressionStream);
                }
                return outputStream.ToArray();
            }
        }

        public static byte[] Decompress(CompressionStreamType type, Stream inputStream, bool leaveOpen = false)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var decompressionStream = Create(type, inputStream, CompressionMode.Decompress, leaveOpen))
                {
                    decompressionStream.CopyTo(outputStream);
                }
                return outputStream.ToArray();
            }
        }

        public static byte[] Compress(CompressionStreamType type, byte[] data, CompressionLevel level, bool leaveOpen = false)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var compressionStream = Create(type, outputStream, level, leaveOpen))
                {
                    compressionStream.Write(data, 0, data.Length);
                }
                return outputStream.ToArray();
            }
        }

        public static byte[] Decompress(CompressionStreamType type, byte[] data, bool leaveOpen = false)
        {
            using (var outputStream = new MemoryStream())
            {
                using (var inputStream = new MemoryStream(data))
                using (var decompressionStream = Create(type, inputStream, CompressionMode.Decompress, leaveOpen))
                {
                    decompressionStream.CopyTo(outputStream);
                }
                return outputStream.ToArray();
            }
        }
    }
}

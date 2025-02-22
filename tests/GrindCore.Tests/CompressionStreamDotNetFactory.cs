using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection.Emit;

namespace Nanook.GrindCore
{
    public class CompressionStreamDotNetFactory
    {
        private static readonly Dictionary<CompressionAlgorithm, Func<Stream, CompressionMode, CompressionLevel, bool, Stream>> streamCreators = new Dictionary<CompressionAlgorithm, Func<Stream, CompressionMode, CompressionLevel, bool, Stream>>()
        {
            { CompressionAlgorithm.GZip, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new GZipStream(stream, mode, leaveOpen) : new GZipStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.ZLib, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new ZLibStream(stream, mode, leaveOpen) : new ZLibStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.Deflate, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new DeflateStream(stream, mode, leaveOpen) : new DeflateStream(stream, level) },
            { CompressionAlgorithm.GZipNg, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new GZipStream(stream, mode, leaveOpen) : new GZipStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.ZLibNg, (stream, mode, level, leaveOpen) =>mode == CompressionMode.Decompress ? new ZLibStream(stream, mode, leaveOpen) :  new ZLibStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.DeflateNg, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new DeflateStream(stream, mode, leaveOpen) : new DeflateStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.Brotli, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new BrotliStream(stream, mode, leaveOpen) : new BrotliStream(stream, level, leaveOpen) }
        };

        public static Stream Create(CompressionAlgorithm algorithm, Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen = false)
        {
            return create(algorithm, stream, mode, level, leaveOpen);
        }

        private static Stream create(CompressionAlgorithm algorithm, Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            if (streamCreators.TryGetValue(algorithm, out var creator))
                return creator(stream, mode, level, leaveOpen);

            throw new ArgumentException("Unsupported stream algorithm", nameof(algorithm));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Compression options that apply to all Compression Stream. Only Type needs to be set 
    /// </summary>
    public class CompressionOptions
    {
        /// <summary>
        /// CompressionType can be Decompress or Level
        /// </summary>
        public CompressionType Type { get; set; }

        /// <summary>
        /// Leave the base stream open on dispose. Defaults to false
        /// </summary>
        public bool LeaveOpen { get; set; }

        /// <summary>
        /// Version of the compressiong e.g. ZLib supports v1.3.1 and Ng 2.2.1 (at the time this comment was written)
        /// </summary>
        public CompressionVersion? Version { get; set; }

        /// <summary>
        /// ThreadCount for supported algorithms
        /// </summary>
        public int? ThreadCount { get; set; }

        /// <summary>
        /// BlockSize. LZMA2 Blocksize, -1 will compress in full solid mode. If threads > 1 and BlockSize != -1 then the block is divided by the amount of threads and processed in subblocks. If not -1 this will override ProcessSizeMin and ProcessSizeMax
        /// </summary>
        public long? BlockSize { get; set; }

        /// <summary>
        /// Properties required for processing, e.g. LZMA/2 requires these for decoding - they can be read from the encoder and stored
        /// </summary>
        public byte[]? InitProperties { get; set; }

        /// <summary>
        /// Compression/Decompression will be performed when the internal buffer is at least this size. Useful when using WriteByte etc
        /// </summary>
        public int? ProcessSizeMin { get; set; }

        /// <summary>
        /// Maximum size of buffer. This size will be used where possible
        /// </summary>
        public int? ProcessSizeMax { get; set; }

        /// <summary>
        /// This will override any internal stream default sizes
        /// </summary>
        public int? InternalBufferSize { get; set; }

        public static CompressionOptions DefaultDecompress() => new CompressionOptions() { Type = CompressionType.Decompress };

        public static CompressionOptions DefaultCompressOptimal() => new CompressionOptions() { Type = CompressionType.Decompress };

    }
}

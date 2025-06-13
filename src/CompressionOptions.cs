using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Represents options that apply to all compression streams and blocks.
    /// </summary>
    public class CompressionOptions
    {
        /// <summary>
        /// Gets or sets the compression type. Can be <see cref="CompressionType.Decompress"/> or a compression level.
        /// </summary>
        public CompressionType Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the base stream should be left open after the compression stream is disposed. Defaults to <c>false</c>.
        /// </summary>
        public bool LeaveOpen { get; set; }

        /// <summary>
        /// Gets or sets the version of the compression algorithm to use (e.g., ZLib supports v1.3.1 and Ng 2.2.1).
        /// </summary>
        public CompressionVersion? Version { get; set; }

        /// <summary>
        /// Gets or sets the thread count for supported algorithms.
        /// </summary>
        public int? ThreadCount { get; set; }

        /// <summary>
        /// Gets or sets the block size. For LZMA2, -1 will compress in full solid mode. 
        /// If threads &gt; 1 and BlockSize != -1, then the block is divided by the number of threads and processed in subblocks.
        /// If not -1, this will override ProcessSizeMin and BufferSize.
        /// </summary>
        public long? BlockSize { get; set; }

        /// <summary>
        /// Gets or sets the write limit during compression and the read limit during decompression. 
        /// This corresponds to the Position property of CompressionStream.If null, no limit is applied.
        /// </summary>
        public long? PositionLimit { get; set; }

        /// <summary>
        /// Gets or sets the buffer read limit during compression and the buffer write limit during decompression. 
        /// This corresponds to the PositionFullSize property of CompressionStream. If null, no limit is applied.
        /// </summary>
        public long? PositionFullSizeLimit { get; set; }

        /// <summary>
        /// Gets or sets the properties required for processing (e.g., LZMA/2 requires these for decoding; they can be read from the encoder and stored).
        /// </summary>
        public byte[]? InitProperties { get; set; }

        /// <summary>
        /// Gets or sets the buffer size. Compression/Decompression will be performed when the internal output buffer is at least this size.
        /// Useful when using <see cref="System.IO.Stream.WriteByte(byte)"/> etc. This is the maximum size of the output buffer and will be used where possible.
        /// </summary>
        public int? BufferSize { get; set; }

        /// <summary>
        /// Returns a <see cref="CompressionOptions"/> instance configured for decompression.
        /// </summary>
        public static CompressionOptions DefaultDecompress() => new CompressionOptions() { Type = CompressionType.Decompress };

        /// <summary>
        /// Returns a <see cref="CompressionOptions"/> instance configured for optimal compression.
        /// </summary>
        public static CompressionOptions DefaultCompressOptimal() => new CompressionOptions() { Type = CompressionType.Optimal };
    }
}

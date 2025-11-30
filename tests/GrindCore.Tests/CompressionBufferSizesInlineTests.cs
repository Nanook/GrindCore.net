using System;
using System.Threading.Tasks;
using Xunit;
using Nanook.GrindCore;
using GrindCore.Tests.Utility;
using Utl = GrindCore.Tests.Utility.Utilities;
using System.Diagnostics;

namespace GrindCore.Tests
{
#if !IS_32BIT
    public sealed class CompressionBufferSizesTests
    {
        // Buffer sizes to exercise small, medium, large and non-aligned cases (hex literals)
        private static readonly int[] BufferSizes = new[] { 0x40, 0x400, 0x2000, 0x10000, 0x10001, 0x186A0, 0x20000, 0x40000 };

        // Use non-compressible stream data
        private const int StreamLen = 5 * 1024 * 1024; // 5 MiB - keeps runtime reasonable

        // Only fastest compression. Buffer size is provided inline for each test.
        [Theory]
        // Brotli
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0, "", "", 0x40000)]

        // Deflate
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0, "", "", 0x40000)]

        // DeflateNg
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0, "", "", 0x40000)]

        // FastLzma2
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0, "", "", 0x40000)]

        // Lz4
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0, "", "", 0x40000)]

        // Lzma
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0, "", "", 0x40000)]

        // Lzma2
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0, "", "", 0x40000)]

        // ZLib
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0, "", "", 0x40000)]

        // ZLibNg
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0, "", "", 0x40000)]

        // ZStd
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0, "", "", 0x40)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0, "", "", 0x400)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0, "", "", 0x2000)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0, "", "", 0x10000)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0, "", "", 0x10001)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0, "", "", 0x186A0)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0, "", "", 0x20000)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0, "", "", 0x40000)]
        public void Data_BufferSizes_Various(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64, int bufferSize)
        {
            using (var data = new TestNonCompressibleDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, StreamLen, bufferSize, StreamLen);

                // Write out an InlineData line that includes the buffer size as a hex comment so it's easy to pick up expected values.
                Trace.WriteLine($"// bufferSize=0x{bufferSize:x}");
                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\", 0x{bufferSize:x})]\n");

                Assert.True(r.CompressedBytes > 0, $"Compressed output should be non-empty for {algorithm}/{type} (buf=0x{bufferSize:x})");
                if (compressedSize != 0)
                    Assert.Equal(compressedSize, r.CompressedBytes);
                if (!string.IsNullOrEmpty(compXxH64))
                    Assert.Equal(compXxH64, r.CompressedHash);
                if (!string.IsNullOrEmpty(rawXxH64))
                    Assert.Equal(rawXxH64, r.InHash);

                Assert.Equal(r.InHash, r.OutHash);
            }
        }
    }
#endif
}

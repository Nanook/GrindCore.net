using System;
using System.Text;
using Nanook.GrindCore;
using Nanook.GrindCore.ZStd;
using Xunit;

namespace GrindCore.Tests
{
    /// <summary>
    /// Covers raw content-dictionary support for ZStd, both the one-shot block API (<see cref="ZStdBlock"/>,
    /// v1.5.7 and v1.5.2) and the streaming API (<see cref="ZStdStream"/>). A dictionary is supplied via
    /// <see cref="CompressionOptions.InitProperties"/>, the same convention used across both APIs.
    /// </summary>
    public sealed class ZStdDictionaryTests
    {
        // Shared "content" that a dictionary can be built from and later referenced by similar,
        // independently-compressed records - mirrors NDZ's prefix-dictionary-built-from-the-data use case.
        private static byte[] buildRecord(string id, string body)
        {
            return Encoding.UTF8.GetBytes($"{{\"type\":\"record\",\"id\":\"{id}\",\"body\":\"{body}\",\"version\":1,\"flags\":[\"a\",\"b\",\"c\"]}}");
        }

        private static readonly byte[] Dictionary = buildRecord("dict-seed", "the quick brown fox jumps over the lazy dog repeatedly for padding");

        [Theory]
        [InlineData("record-001", "alpha payload contents here")]
        [InlineData("record-002", "beta payload contents here, slightly longer than alpha")]
        [InlineData("record-003", "gamma")]
        public void ZStdBlock_Dictionary_RoundTrip(string id, string body)
        {
            byte[] data = buildRecord(id, body);

            var options = new CompressionOptions
            {
                Type = CompressionType.Optimal,
                BlockSize = data.Length,
                InitProperties = Dictionary
            };

            using (var block = new ZStdBlock(options))
            {
                byte[] compressed = new byte[block.RequiredCompressOutputSize];
                int compressedLength = compressed.Length;
                var compressResult = block.Compress(data, 0, data.Length, compressed, 0, ref compressedLength);
                Assert.Equal(CompressionResultCode.Success, compressResult);

                byte[] decompressed = new byte[data.Length];
                int decompressedLength = decompressed.Length;
                var decompressResult = block.Decompress(compressed, 0, compressedLength, decompressed, 0, ref decompressedLength);
                Assert.Equal(CompressionResultCode.Success, decompressResult);

                Assert.Equal(data.Length, decompressedLength);
                Assert.Equal(data, decompressed);
            }
        }

        [Fact]
        public void ZStdBlock_Dictionary_ProducesSmallerOutput_ThanPlain()
        {
            // Small, self-similar records don't have enough internal repetition for zstd to exploit on
            // their own - a shared content dictionary should measurably help here. Proves the dictionary
            // is actually being used by CompressBlockWithDict, not silently accepted and ignored.
            byte[] data = buildRecord("record-042", "the quick brown fox jumps over the lazy dog repeatedly for padding, with a bit more unique content appended for realism");

            var plainOptions = new CompressionOptions { Type = CompressionType.Optimal, BlockSize = data.Length };
            var dictOptions = new CompressionOptions { Type = CompressionType.Optimal, BlockSize = data.Length, InitProperties = Dictionary };

            int plainSize = compressAndGetSize(plainOptions, data);
            int dictSize = compressAndGetSize(dictOptions, data);

            Assert.True(dictSize < plainSize, $"Expected dictionary-primed compression ({dictSize} bytes) to beat plain compression ({plainSize} bytes) for dictionary-similar data");
        }

        private static int compressAndGetSize(CompressionOptions options, byte[] data)
        {
            using (var block = new ZStdBlock(options))
            {
                byte[] compressed = new byte[block.RequiredCompressOutputSize];
                int compressedLength = compressed.Length;
                var result = block.Compress(data, 0, data.Length, compressed, 0, ref compressedLength);
                Assert.Equal(CompressionResultCode.Success, result);
                return compressedLength;
            }
        }

        [Theory]
        [InlineData("record-001", "alpha payload contents here")]
        [InlineData("record-002", "beta payload contents here, slightly longer than alpha")]
        [InlineData("record-003", "gamma")]
        public void ZStdBlock_Dictionary_V1_5_2_RoundTrip(string id, string body)
        {
            byte[] data = buildRecord(id, body);

            var options = new CompressionOptions
            {
                Type = CompressionType.Optimal,
                BlockSize = data.Length,
                Version = CompressionVersion.ZStd(ZStdVersion.v1_5_2),
                InitProperties = Dictionary
            };

            using (var block = new ZStdBlock(options))
            {
                byte[] compressed = new byte[block.RequiredCompressOutputSize];
                int compressedLength = compressed.Length;
                var compressResult = block.Compress(data, 0, data.Length, compressed, 0, ref compressedLength);
                Assert.Equal(CompressionResultCode.Success, compressResult);

                byte[] decompressed = new byte[data.Length];
                int decompressedLength = decompressed.Length;
                var decompressResult = block.Decompress(compressed, 0, compressedLength, decompressed, 0, ref decompressedLength);
                Assert.Equal(CompressionResultCode.Success, decompressResult);

                Assert.Equal(data.Length, decompressedLength);
                Assert.Equal(data, decompressed);
            }
        }

        [Fact]
        public void ZStdBlock_Dictionary_V1_5_2_ProducesSmallerOutput_ThanPlain()
        {
            // Same rationale as the v1.5.7 test above: proves CompressBlockWithDict is actually using the
            // dictionary for v1.5.2, not silently ignoring it.
            byte[] data = buildRecord("record-042", "the quick brown fox jumps over the lazy dog repeatedly for padding, with a bit more unique content appended for realism");

            var plainOptions = new CompressionOptions { Type = CompressionType.Optimal, BlockSize = data.Length, Version = CompressionVersion.ZStd(ZStdVersion.v1_5_2) };
            var dictOptions = new CompressionOptions { Type = CompressionType.Optimal, BlockSize = data.Length, Version = CompressionVersion.ZStd(ZStdVersion.v1_5_2), InitProperties = Dictionary };

            int plainSize = compressAndGetSize(plainOptions, data);
            int dictSize = compressAndGetSize(dictOptions, data);

            Assert.True(dictSize < plainSize, $"Expected dictionary-primed compression ({dictSize} bytes) to beat plain compression ({plainSize} bytes) for dictionary-similar data");
        }

        [Theory]
        [InlineData("record-101", "delta payload for the streaming round trip")]
        [InlineData("record-102", "epsilon payload, a little longer to span more than one internal buffer flush")]
        public void ZStdStream_Dictionary_RoundTrip(string id, string body)
        {
            // Regression coverage for the v1.5.7 streaming-decompress dictionary gap: compressing with a
            // dictionary via ZStdStream previously could not be decompressed back through ZStdStream with
            // the same dictionary (ZStdDecoder's base v1.5.7 constructor silently ignored it). Fixed
            // upstream in commit 0d25fdf; this test exists so a future regression fails loudly instead of
            // silently, since nothing previously exercised this round trip.
            byte[] data = buildRecord(id, body);
            byte[] compressed;

            var compressOptions = new CompressionOptions { Type = CompressionType.Optimal, LeaveOpen = true, InitProperties = Dictionary };
            using (var output = new System.IO.MemoryStream())
            {
                using (var stream = new ZStdStream(output, compressOptions))
                    stream.Write(data, 0, data.Length);
                compressed = output.ToArray();
            }

            byte[] decompressed;
            var decompressOptions = new CompressionOptions { Type = CompressionType.Decompress, LeaveOpen = true, InitProperties = Dictionary };
            using (var input = new System.IO.MemoryStream(compressed))
            using (var output = new System.IO.MemoryStream())
            {
                using (var stream = new ZStdStream(input, decompressOptions))
                    stream.CopyTo(output);
                decompressed = output.ToArray();
            }

            Assert.Equal(data, decompressed);
        }
    }
}

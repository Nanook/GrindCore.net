using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Xunit;
using Nanook.GrindCore;
using GrindCore.Tests.Utility;
using Utl = GrindCore.Tests.Utility.Utilities;

namespace GrindCore.Tests
{
#if !IS_32BIT

    public sealed class CompressionStreamBoundaryTests
    {
        [Fact]
        public void Data_Stream_NonAligned_Sync()
        {
            // Use sizes that are not multiples of 1KiB or 1MiB to exercise boundary/flush logic
            int streamLen = 5 * 1024 * 1024 + 123; // 5MiB + 123 bytes
            int bufferSize = 65537; // not aligned to 1KiB or 1MiB

            using (var data = new TestDataStream())
            {
                // compSize here is just initial MemoryStream capacity - exact compressed size is not asserted
                var r = Utl.TestStreamBlocks(data, CompressionAlgorithm.ZStd, CompressionType.Fastest, streamLen, bufferSize, streamLen);

                Assert.True(r.CompressedBytes > 0, "Compressed output should be non-empty");
                Assert.Equal(r.InHash, r.OutHash); // ensure decompressed data matches input
            }
        }

        [Fact]
        public async Task Data_Stream_NonAligned_Async()
        {
            // Use sizes that break typical buffer boundaries
            int streamLen = 3 * 1024 * 1024 + 789; // 3MiB + 789 bytes
            int bufferSize = 1024 * 1024 + 7; // 1MiB + 7

            using (var data = new TestPseudoTextStream())
            {
                var r = await Utl.TestStreamBlocksAsync(data, CompressionAlgorithm.Brotli, CompressionType.Optimal, streamLen, bufferSize, streamLen);

                Assert.True(r.CompressedBytes > 0, "Compressed output should be non-empty");
                Assert.Equal(r.InHash, r.OutHash);
            }
        }

        [Fact]
        public void Data_Stream_NonAligned_ByteByByte()
        {
            // Byte-at-a-time processing with non-aligned total size
            int streamLen = 1 * 1024 * 1024 + 511; // 1MiB + 511 bytes
            int bufferSize = 8193; // odd buffer size

            using (var data = new TestDataStream())
            {
                var r = Utl.TestStreamBytes(data, CompressionAlgorithm.DeflateNg, CompressionType.Fastest, streamLen, bufferSize, streamLen);

                Assert.True(r.CompressedBytes > 0, "Compressed output should be non-empty");
                Assert.Equal(r.InHash, r.OutHash);
            }
        }

        // The following two tests mirror the large parameterized test style used elsewhere.
        // Expected compressed sizes and hashes are filled in from provided results.
        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x6ecc, "e22ce1d8b9a7a23d", "4c2adc9ca4d7e5c4")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x255d5, "e22ce1d8b9a7a23d", "abf3a6e7a3c696ae")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x40a11, "e22ce1d8b9a7a23d", "a1053465d3f2d62a")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x2062, "e22ce1d8b9a7a23d", "de8008df939145b7")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x1534a, "e22ce1d8b9a7a23d", "532a8eba4082cb70")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0xd67, "e22ce1d8b9a7a23d", "087f8295dab7ddc4")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x3293, "e22ce1d8b9a7a23d", "48bcb21a593eaced")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x255db, "e22ce1d8b9a7a23d", "378b342d377b690e")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x40a17, "e22ce1d8b9a7a23d", "59651dff4e3b8922")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x916, "e22ce1d8b9a7a23d", "3763b14f06732584")]

        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1b5, "e22ce1d8b9a7a23d", "d99f92dae92f0c4d")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x1677f, "e22ce1d8b9a7a23d", "b77efec84cf55321")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x167a3, "e22ce1d8b9a7a23d", "467f84a650645793")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x1372, "e22ce1d8b9a7a23d", "d097feab6b6a1c45")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0x33489, "e22ce1d8b9a7a23d", "97477534f19906b8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0xd67, "e22ce1d8b9a7a23d", "7190f212225d8c46")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x3299, "e22ce1d8b9a7a23d", "8e75767a6c72e018")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x16785, "e22ce1d8b9a7a23d", "642e1d1b214b2412")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x167a9, "e22ce1d8b9a7a23d", "cf23a2bd3bfd2f6f")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x916, "e22ce1d8b9a7a23d", "63860edf5917614a")]

        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1a5, "e22ce1d8b9a7a23d", "21ec2267f1307de6")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x1677f, "e22ce1d8b9a7a23d", "b77efec84cf55321")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x1677f, "e22ce1d8b9a7a23d", "b77efec84cf55321")]
        // too slow [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x1372, "e22ce1d8b9a7a23d", "a844a31d2167ae00")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0x33489, "e22ce1d8b9a7a23d", "97477534f19906b8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0xd67, "e22ce1d8b9a7a23d", "7190f212225d8c46")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x3299, "e22ce1d8b9a7a23d", "8e75767a6c72e018")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x16785, "e22ce1d8b9a7a23d", "6978328d38f6a954")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x16785, "e22ce1d8b9a7a23d", "6978328d38f6a954")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x878, "e22ce1d8b9a7a23d", "a3646178807c3262")]
        public async Task Data_Stream20MiB_NonAligned_Chunk1MiB_Async(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            int streamLen = 20 * 1024 * 1024 + 12345; // 20MiB + non-aligned bytes
            int bufferSize = 1 * 1024 * 1024 + 77; // 1MiB + offset to break boundaries

            using (var data = new TestDataStream())
            {
                TestResults r = await Utl.TestStreamBlocksAsync(data, algorithm, type, streamLen, bufferSize, (int)streamLen);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]\n");

                if (compressedSize != 0)
                    Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected when provided
                if (!string.IsNullOrEmpty(compXxH64))
                    Assert.Equal(compXxH64, r.CompressedHash); //test compressed hash when provided
                if (!string.IsNullOrEmpty(rawXxH64))
                    Assert.Equal(rawXxH64, r.InHash); //test raw hash when provided

                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x6ecc, "e22ce1d8b9a7a23d", "4c2adc9ca4d7e5c4")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x255d5, "e22ce1d8b9a7a23d", "abf3a6e7a3c696ae")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x40a11, "e22ce1d8b9a7a23d", "a1053465d3f2d62a")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x2062, "e22ce1d8b9a7a23d", "de8008df939145b7")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x1534a, "e22ce1d8b9a7a23d", "532a8eba4082cb70")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0xd67, "e22ce1d8b9a7a23d", "087f8295dab7ddc4")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x3293, "e22ce1d8b9a7a23d", "48bcb21a593eaced")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x255db, "e22ce1d8b9a7a23d", "378b342d377b690e")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x40a17, "e22ce1d8b9a7a23d", "59651dff4e3b8922")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x916, "e22ce1d8b9a7a23d", "3763b14f06732584")]

        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1b5, "e22ce1d8b9a7a23d", "d99f92dae92f0c4d")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x1677f, "e22ce1d8b9a7a23d", "b77efec84cf55321")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x167a3, "e22ce1d8b9a7a23d", "467f84a650645793")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x1372, "e22ce1d8b9a7a23d", "d097feab6b6a1c45")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0x33489, "e22ce1d8b9a7a23d", "97477534f19906b8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0xd67, "e22ce1d8b9a7a23d", "7190f212225d8c46")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x3299, "e22ce1d8b9a7a23d", "8e75767a6c72e018")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x16785, "e22ce1d8b9a7a23d", "642e1d1b214b2412")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x167a9, "e22ce1d8b9a7a23d", "cf23a2bd3bfd2f6f")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x916, "e22ce1d8b9a7a23d", "63860edf5917614a")]

        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1a5, "e22ce1d8b9a7a23d", "21ec2267f1307de6")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x1677f, "e22ce1d8b9a7a23d", "b77efec84cf55321")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x1677f, "e22ce1d8b9a7a23d", "b77efec84cf55321")]
        // too slow [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x1372, "e22ce1d8b9a7a23d", "a844a31d2167ae00")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0x33489, "e22ce1d8b9a7a23d", "97477534f19906b8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0xd67, "e22ce1d8b9a7a23d", "7190f212225d8c46")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x3299, "e22ce1d8b9a7a23d", "8e75767a6c72e018")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x16785, "e22ce1d8b9a7a23d", "6978328d38f6a954")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x16785, "e22ce1d8b9a7a23d", "6978328d38f6a954")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x878, "e22ce1d8b9a7a23d", "a3646178807c3262")]
        public void Data_Stream20MiB_NonAligned_Chunk1MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            int streamLen = 20 * 1024 * 1024 + 12345; // 20MiB + non-aligned bytes
            int bufferSize = 1 * 1024 * 1024 + 77; // 1MiB + offset to break boundaries

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)streamLen);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]\n");

                if (compressedSize != 0)
                    Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected when provided
                if (!string.IsNullOrEmpty(compXxH64))
                    Assert.Equal(compXxH64, r.CompressedHash); //test compressed hash when provided
                if (!string.IsNullOrEmpty(rawXxH64))
                    Assert.Equal(rawXxH64, r.InHash); //test raw hash when provided

                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }
    }
#endif
}

using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Xunit;
using Utl = GrindCore.Tests.Utility.Utilities;

namespace GrindCore.Tests
{

#if !IS_32BIT
    public sealed class Lzma2Tests
    {

        [Theory]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest,      0x200000,  1,  0x953, "7833322f45651d24", "eb4d661eaefb646f")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest,      0x200000,  4,  0xec5, "7833322f45651d24", "5f71c3fb6c0b1c7b")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest,      0x600000,  1,  0x8bb, "7833322f45651d24", "f728b3d543c6e2cb")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest,      0x600000,  4,  0xe7c, "7833322f45651d24", "d545ad69abce9f99")]

        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,            -1,  1,  0x572, "7833322f45651d24", "9b0d306d9158f3f1")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,            -1,  4,  0xae5, "7833322f45651d24", "8f6b84ebdffcd0f5")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,             0,  8, 0x1261, "7833322f45651d24", "1004ed0ff69112de")]
        //[InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,             0, 16, 0x2141, "7833322f45651d24", "3b868872aade61ff")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,      0x200000,  1,  0x92b, "7833322f45651d24", "d5731ec04f7bc864")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,      0x200000,  4, 0x19c9, "7833322f45651d24", "c69e0eb6acf6f443")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.SmallestSize, 0x200000,  4, 0x19d5, "7833322f45651d24", "676dca129bb3ed21")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.SmallestSize, 0x200000,  1,  0x92b, "7833322f45651d24", "e6241c0cc51e3eae")]
        //[InlineData(CompressionAlgorithm.Lzma2,     CompressionType.SmallestSize, 0x200000, 16, 0x5d01, "7833322f45651d24", "1ebbf3862ca8b369")]

        public void Data_Stream6MiB(CompressionAlgorithm algorithm, CompressionType type, int blockSize, int threadCount, long compressedSize, string rawXxH64, string compXxH64)
        {
            int streamLen = 6 * 1024 * 1024;
            int buffLen = blockSize <= 0 ? streamLen : blockSize;

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, buffLen, (int)compressedSize, threadCount);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, {(blockSize <= 0 ? blockSize : ("0x" + blockSize.ToString("x")))}, {threadCount}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }

        }

        [Theory] // verified hashes that 7zip app creates for the same settings (at the time of testing)
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Level5, -1, 1, 0x572, "7833322f45651d24", "9031f3192bac146c")]
        public void Match7ZipAppHashes(CompressionAlgorithm algorithm, CompressionType type, int blockSize, int threadCount, long compressedSize, string rawXxH64, string compXxH64)
        {

            int streamLen = 6 * 1024 * 1024;
            int buffLen = blockSize <= 0 ? streamLen : blockSize;

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, buffLen, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, {(blockSize <= 0 ? blockSize : ("0x" + blockSize.ToString("x")))}, {threadCount}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }
    }
#endif
}

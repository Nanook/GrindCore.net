using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Xunit;
using Utl = GrindCore.Tests.Utility.Utilities;
using Nanook.GrindCore.XXHash;
using System.IO;

namespace GrindCore.Tests
{
#if !IS_32BIT //works for win-x86 not arm :(

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
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,             0, 16, 0x2141, "7833322f45651d24", "3b868872aade61ff")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,      0x200000,  1,  0x92b, "7833322f45651d24", "d5731ec04f7bc864")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,      0x200000,  4, 0x19c9, "7833322f45651d24", "c69e0eb6acf6f443")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.SmallestSize, 0x200000,  4, 0x19d5, "7833322f45651d24", "676dca129bb3ed21")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.SmallestSize, 0x200000,  1,  0x92b, "7833322f45651d24", "e6241c0cc51e3eae")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.SmallestSize, 0x200000, 16, 0x5d01, "7833322f45651d24", "1ebbf3862ca8b369")]

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

        // === NEW: LZMA2 dictionary-focused tests ===

        [Fact]
        public void Lzma2_WithLzma2Dictionary_RoundTrip()
        {
            const int streamLen = 1 * 1024 * 1024; // 1MiB
            const int bufferSize = 256 * 1024; // 256KiB blocks to exercise block handling

            using (var data = new TestDataStream())
            using (var inHash = XXHash64.Create())
            using (var compHash = XXHash64.Create())
            using (var outHash = XXHash64.Create())
            using (var compMs = new MemoryStream())
            {
                var options = CompressionOptions.DefaultCompressOptimal()
                    .WithLzma2Dictionary(dictionarySize: 1L << 24, fastBytes: 64);
                options.Type = CompressionType.Level5;
                options.LeaveOpen = true;
                options.BufferSize = bufferSize;
                options.BlockSize = bufferSize;
                options.ThreadCount = 1;

                var decompOptions = new CompressionOptions
                {
                    Type = CompressionType.Decompress,
                    LeaveOpen = true,
                    BufferSize = bufferSize,
                    BlockSize = bufferSize
                };

                // compress & hash input
                using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, compMs, options))
                using (var crypto = new CryptoStream(compStream, inHash, CryptoStreamMode.Write, true))
                {
                    byte[] buf = new byte[bufferSize];
                    int read;
                    int total = 0;
                    while (total < streamLen && (read = data.Read(buf, 0, Math.Min(buf.Length, streamLen - total))) > 0)
                    {
                        crypto.Write(buf, 0, read);
                        total += read;
                    }
                    compStream.Complete();
                    decompOptions.InitProperties = compStream.Properties;
                }

                long compressedBytes = compMs.Position;
                compMs.Position = 0;

                // hash compressed bytes
                using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
                    compMs.CopyTo(cs);

                // decompress and hash output
                compMs.Position = 0;
                using (var decStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, compMs, decompOptions))
                {
                    byte[] outBuf = new byte[bufferSize];
                    int r;
                    while ((r = decStream.Read(outBuf, 0, outBuf.Length)) > 0)
                    {
                        outHash.TransformBlock(outBuf, 0, r, null, 0);
                    }
                    outHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                }

                string inHx = inHash.Hash!.ToHexString();
                string outHx = outHash.Hash!.ToHexString();

                Trace.WriteLine($"LZMA2 Balanced: input={streamLen:N0} compressed={compressedBytes:N0}");
                Assert.Equal(inHx, outHx);
                Assert.True(compressedBytes > 0);
            }
        }

        [Fact]
        public void Lzma2_WithLzma2MaxDictionary_RoundTrip()
        {
            const int streamLen = 1 * 1024 * 1024; // 1MiB
            const int bufferSize = 256 * 1024;

            using (var data = new TestDataStream())
            using (var inHash = XXHash64.Create())
            using (var compHash = XXHash64.Create())
            using (var outHash = XXHash64.Create())
            using (var compMs = new MemoryStream())
            {
                var options = CompressionOptions.DefaultCompressOptimal()
                    .WithLzma2MaxDictionary(); // sets large dict + heavy tuning
                options.Type = CompressionType.SmallestSize; // prefer best ratio
                options.LeaveOpen = true;
                options.BufferSize = bufferSize;
                options.BlockSize = -1; // solid by default from helper, ensure it's set
                options.ThreadCount = 1; // single-thread for deterministic behavior

                var decompOptions = new CompressionOptions
                {
                    Type = CompressionType.Decompress,
                    LeaveOpen = true,
                    BufferSize = bufferSize,
                    BlockSize = options.BlockSize
                };

                // compress & hash input
                using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, compMs, options))
                using (var crypto = new CryptoStream(compStream, inHash, CryptoStreamMode.Write, true))
                {
                    byte[] buf = new byte[bufferSize];
                    int total = 0;
                    int read;
                    while (total < streamLen && (read = data.Read(buf, 0, Math.Min(buf.Length, streamLen - total))) > 0)
                    {
                        crypto.Write(buf, 0, read);
                        total += read;
                    }
                    compStream.Complete();
                    decompOptions.InitProperties = compStream.Properties;
                }

                long compressedBytes = compMs.Position;
                compMs.Position = 0;

                // hash compressed bytes
                using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
                    compMs.CopyTo(cs);

                // decompress and hash output
                compMs.Position = 0;
                using (var decStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, compMs, decompOptions))
                {
                    byte[] outBuf = new byte[bufferSize];
                    int r;
                    while ((r = decStream.Read(outBuf, 0, outBuf.Length)) > 0)
                    {
                        outHash.TransformBlock(outBuf, 0, r, null, 0);
                    }
                    outHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                }

                string inHx = inHash.Hash!.ToHexString();
                string outHx = outHash.Hash!.ToHexString();

                Trace.WriteLine($"LZMA2 Max: input={streamLen:N0} compressed={compressedBytes:N0}");
                Assert.Equal(inHx, outHx);
                Assert.True(compressedBytes > 0);
            }
        }

        [Fact]
        public void Lzma2_DictionarySettings_ImpactCompression()
        {
            const int streamLen = 1 * 1024 * 1024; // 1MiB
            const int bufferSize = 256 * 1024;

            using (var compHash = XXHash64.Create())
            using (var outHash = XXHash64.Create())
            {
                var presets = new (string Name, CompressionOptions Options)[]
                {
                    ("Default", CompressionOptions.DefaultCompressOptimal()),
                    ("Lzma2Balanced", CompressionOptions.DefaultCompressOptimal().WithLzma2Dictionary(1L<<24, 64)),
                    ("Lzma2Max", CompressionOptions.DefaultCompressOptimal().WithLzma2MaxDictionary())
                };  

                var results = new System.Collections.Generic.List<(string Name, long Size, string Hash)>();

                foreach (var p in presets)
                {
                    // recreate data stream (not seekable) so each preset sees same input
                    using (var data = new TestDataStream())
                    using (var inHash = XXHash64.Create())
                    {
                        // prepare options
                        var options = p.Options;
                        options.Type = CompressionType.Level5;
                        options.LeaveOpen = true;
                        options.BufferSize = bufferSize;
                        // prefer single-thread for determinism
                        options.ThreadCount = 1;
                        // set block size: use buffer to force block mode for balanced preset, leave solid for max if helper set -1
                        if (!options.BlockSize.HasValue)
                            options.BlockSize = bufferSize;

                        using (var compMs = new MemoryStream())
                        using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, compMs, options))
                        using (var crypto = new CryptoStream(compStream, inHash, CryptoStreamMode.Write, true))
                        {
                            // feed same data
                            byte[] buf = new byte[bufferSize];
                            int total = 0;
                            int read;
                            while (total < streamLen && (read = data.Read(buf, 0, Math.Min(buf.Length, streamLen - total))) > 0)
                            {
                                crypto.Write(buf, 0, read);
                                total += read;
                            }
                            compStream.Complete();
                        
                            long compressedBytes = compMs.Position;
                            compMs.Position = 0;

                            // compute compressed hash
                            using (var ch = XXHash64.Create())
                            using (var cs = new CryptoStream(Stream.Null, ch, CryptoStreamMode.Write, true))
                            {
                                compMs.CopyTo(cs);
                                cs.FlushFinalBlock();
                                results.Add((p.Name, compressedBytes, ch.Hash!.ToHexString()));
                            }
                        }
                    }
                }

                // Log results
                foreach (var r in results)
                    Trace.WriteLine($"Preset={r.Name}, Compressed={r.Size}, Hash={r.Hash}");

                // Ensure not all compressed sizes are equal
                var distinctSizes = new System.Collections.Generic.HashSet<long>(results.ConvertAll(x => x.Size));
                Assert.True(distinctSizes.Count > 1, "Expected different compressed sizes across dictionary presets");
            }
        }

    }
#endif
}

using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Xunit;
using Utl = GrindCore.Tests.Utility.Utilities;
using Nanook.GrindCore.XXHash;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Nanook.GrindCore.Lzma;

namespace GrindCore.Tests
{

#if !IS_32BIT //works for win-x86 not arm :(

    public sealed class Lzma2Tests
    {
        //// These match the 7zip app (v25.01) when setting the dict size, threads to 1 and block size to solid
        //[Theory]
        //[InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 4, 0x1831cfe2, "5498dfddb9e1a40e", "0e7687d82e5aeee6")]
        //[InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 4, 0x1831911c, "5498dfddb9e1a40e", "92036e1d1f07ede8")] // level 1
        //[InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 32, 0x144d5196, "5498dfddb9e1a40e", "c93a3eab5ad48b56")] // level 5
        //public void Data_StreamCorpus(CompressionAlgorithm algorithm, CompressionType type, int dictMb, long compressedSize, string rawXxH64, string compXxH64)
        //{
        //    FileInfo inputFile = new FileInfo(@"D:\Temp\mcorpus.bin");
        //    int streamLen = (int)inputFile.Length; // Total bytes to process
        //    int bufferSize = dictMb * 1024 * 1024; // 1MiB block size

        //    using (var data = inputFile.OpenRead())
        //    {
        //        TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, 0, 1, null, -1);

        //        Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, {dictMb}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
        //        Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
        //        Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
        //        Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
        //        Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
        //    }
        //}

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

    //    [Fact]
    //    public void Lzma2_WithLzma2MaxDictionary_RoundTrip()
    //    {
    //        const int streamLen = 1 * 1024 * 1024; // 1MiB
    //        const int bufferSize = 256 * 1024;

    //        using (var data = new TestDataStream())
    //        using (var inHash = XXHash64.Create())
    //        using (var compHash = XXHash64.Create())
    //        using (var outHash = XXHash64.Create())
    //        using (var compMs = new MemoryStream())
    //        {
    //            var options = CompressionOptions.DefaultCompressOptimal()
    //                .WithLzma2MaxDictionary(); // sets large dict + heavy tuning
    //            options.Type = CompressionType.SmallestSize; // prefer best ratio
    //            options.LeaveOpen = true;
    //            options.BufferSize = bufferSize;
    //            options.BlockSize = -1; // solid by default from helper, ensure it's set
    //            options.ThreadCount = 1; // single-thread for deterministic behavior

    //            var decompOptions = new CompressionOptions
    //            {
    //                Type = CompressionType.Decompress,
    //                LeaveOpen = true,
    //                BufferSize = bufferSize,
    //                BlockSize = options.BlockSize
    //            };

    //            // compress & hash input
    //            using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, compMs, options))
    //            using (var crypto = new CryptoStream(compStream, inHash, CryptoStreamMode.Write, true))
    //            {
    //                byte[] buf = new byte[bufferSize];
    //                int total = 0;
    //                int read;
    //                while (total < streamLen && (read = data.Read(buf, 0, Math.Min(buf.Length, streamLen - total))) > 0)
    //                {
    //                    crypto.Write(buf, 0, read);
    //                    total += read;
    //                }
    //                compStream.Complete();
    //                decompOptions.InitProperties = compStream.Properties;
    //            }

    //            long compressedBytes = compMs.Position;
    //            compMs.Position = 0;

    //            // hash compressed bytes
    //            using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
    //                compMs.CopyTo(cs);

    //            // decompress and hash output
    //            compMs.Position = 0;
    //            using (var decStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, compMs, decompOptions))
    //            {
    //                byte[] outBuf = new byte[bufferSize];
    //                int r;
    //                while ((r = decStream.Read(outBuf, 0, outBuf.Length)) > 0)
    //                {
    //                    outHash.TransformBlock(outBuf, 0, r, null, 0);
    //                }
    //                outHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    //            }

    //            string inHx = inHash.Hash!.ToHexString();
    //            string outHx = outHash.Hash!.ToHexString();

    //            Trace.WriteLine($"LZMA2 Max: input={streamLen:N0} compressed={compressedBytes:N0}");
    //            Assert.Equal(inHx, outHx);
    //            Assert.True(compressedBytes > 0);
    //        }
    //    }

    //    [Fact]
    //    public void Lzma2_DictionarySettings_ImpactCompression()
    //    {
    //        const int streamLen = 1 * 1024 * 1024; // 1MiB
    //        const int bufferSize = 256 * 1024;

    //        using (var compHash = XXHash64.Create())
    //        using (var outHash = XXHash64.Create())
    //        {
    //            var presets = new (string Name, CompressionOptions Options)[]
    //            {
    //                ("Default", CompressionOptions.DefaultCompressOptimal()),
    //                ("Lzma2Balanced", CompressionOptions.DefaultCompressOptimal().WithLzma2Dictionary(1L<<24, 64)),
    //                ("Lzma2Max", CompressionOptions.DefaultCompressOptimal().WithLzma2MaxDictionary())
    //            };  

    //            var results = new System.Collections.Generic.List<(string Name, long Size, string Hash)>();

    //            foreach (var p in presets)
    //            {
    //                // recreate data stream (not seekable) so each preset sees same input
    //                using (var data = new TestDataStream())
    //                using (var inHash = XXHash64.Create())
    //                {
    //                    // prepare options
    //                    var options = p.Options;
    //                    options.Type = CompressionType.Level5;
    //                    options.LeaveOpen = true;
    //                    options.BufferSize = bufferSize;
    //                    // prefer single-thread for determinism
    //                    options.ThreadCount = 1;
    //                    // set block size: use buffer to force block mode for balanced preset, leave solid for max if helper set -1
    //                    if (!options.BlockSize.HasValue)
    //                        options.BlockSize = bufferSize;

    //                    using (var compMs = new MemoryStream())
    //                    using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, compMs, options))
    //                    using (var crypto = new CryptoStream(compStream, inHash, CryptoStreamMode.Write, true))
    //                    {
    //                        // feed same data
    //                        byte[] buf = new byte[bufferSize];
    //                        int total = 0;
    //                        int read;
    //                        while (total < streamLen && (read = data.Read(buf, 0, Math.Min(buf.Length, streamLen - total))) > 0)
    //                        {
    //                            crypto.Write(buf, 0, read);
    //                            total += read;
    //                        }
    //                        compStream.Complete();
                        
    //                        long compressedBytes = compMs.Position;
    //                        compMs.Position = 0;

    //                        // compute compressed hash
    //                        using (var ch = XXHash64.Create())
    //                        using (var cs = new CryptoStream(Stream.Null, ch, CryptoStreamMode.Write, true))
    //                        {
    //                            compMs.CopyTo(cs);
    //                            cs.FlushFinalBlock();
    //                            results.Add((p.Name, compressedBytes, ch.Hash!.ToHexString()));
    //                        }
    //                    }
    //                }
    //            }

    //            // Log results
    //            foreach (var r in results)
    //                Trace.WriteLine($"Preset={r.Name}, Compressed={r.Size}, Hash={r.Hash}");

    //            // Ensure not all compressed sizes are equal
    //            var distinctSizes = new System.Collections.Generic.HashSet<long>(results.ConvertAll(x => x.Size));
    //            Assert.True(distinctSizes.Count > 1, "Expected different compressed sizes across dictionary presets");
    //        }
    //    }

    //    [Fact]
    //    public void Lzma_WithSharpCompressionEquivalentSettings_RoundTrip()
    //    {
    //        const int streamLen = 1 * 1024 * 1024; // 1MiB
    //        const int bufferSize = 256 * 1024;
    //        const int dictionary = 16 * 1024 * 1024; // 16MB dictionary
    //        const int numFastBytes = 64;
    //        const bool eos = true; // end marker

    //        using (var data = new TestDataStream())
    //        using (var inHash = XXHash64.Create())
    //        using (var compHash = XXHash64.Create())
    //        using (var outHash = XXHash64.Create())
    //        using (var compMs = new MemoryStream())
    //        {
    //            // Map SharpCompression LzmaEncoderProperties to CompressionDictionaryOptions
    //            var options = CompressionOptions.DefaultCompressOptimal();
    //            options.Type = CompressionType.Level5;
    //            options.LeaveOpen = true;
    //            options.BufferSize = bufferSize;
    //            options.BlockSize = bufferSize;
    //            options.ThreadCount = 1;
                
    //            // Apply equivalent settings from LzmaEncoderProperties
    //            options.Dictionary = new CompressionDictionaryOptions
    //            {
    //                DictionarySize = dictionary,           // CoderPropId.DictionarySize
    //                PositionBits = 2,                     // CoderPropId.PosStateBits (posStateBits)
    //                LiteralContextBits = 3,               // CoderPropId.LitContextBits (litContextBits)
    //                LiteralPositionBits = 0,              // CoderPropId.LitPosBits (litPosBits)
    //                Algorithm = 2,                        // CoderPropId.Algorithm (algorithm)
    //                FastBytes = numFastBytes,             // CoderPropId.NumFastBytes
    //                BinaryTreeMode = 1,                   // CoderPropId.MatchFinder "bt4" = binary tree mode
    //                HashBytes = 4,                        // "bt4" implies 4 hash bytes
    //                WriteEndMarker = eos                  // CoderPropId.EndMarker
    //            };

    //            var decompOptions = new CompressionOptions
    //            {
    //                Type = CompressionType.Decompress,
    //                LeaveOpen = true,
    //                BufferSize = bufferSize,
    //                BlockSize = bufferSize
    //            };

    //            // compress & hash input
    //            using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compMs, options))
    //            using (var crypto = new CryptoStream(compStream, inHash, CryptoStreamMode.Write, true))
    //            {
    //                byte[] buf = new byte[bufferSize];
    //                int read;
    //                int total = 0;
    //                while (total < streamLen && (read = data.Read(buf, 0, Math.Min(buf.Length, streamLen - total))) > 0)
    //                {
    //                    crypto.Write(buf, 0, read);
    //                    total += read;
    //                }
    //                compStream.Complete();
    //                decompOptions.InitProperties = compStream.Properties;
    //            }

    //            long compressedBytes = compMs.Position;
    //            compMs.Position = 0;

    //            // hash compressed bytes
    //            using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
    //                compMs.CopyTo(cs);

    //            // decompress and hash output
    //            compMs.Position = 0;
    //            using (var decStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compMs, decompOptions))
    //            {
    //                byte[] outBuf = new byte[bufferSize];
    //                int r;
    //                while ((r = decStream.Read(outBuf, 0, outBuf.Length)) > 0)
    //                {
    //                    outHash.TransformBlock(outBuf, 0, r, null, 0);
    //                }
    //                outHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    //            }

    //            string inHx = inHash.Hash!.ToHexString();
    //            string outHx = outHash.Hash!.ToHexString();
    //            string compHx = compHash.Hash!.ToHexString();

    //            Trace.WriteLine($"LZMA SharpCompression-equivalent: input={streamLen:N0} compressed={compressedBytes:N0} hash={compHx}");
    //            Assert.Equal(inHx, outHx);
    //            Assert.True(compressedBytes > 0);
    //        }
    //    }

    //    [Theory]
    //    [InlineData(1024 * 1024, 32, false)]      // 1MB dict, 32 fast bytes, no end marker
    //    [InlineData(4 * 1024 * 1024, 64, true)]  // 4MB dict, 64 fast bytes, with end marker
    //    [InlineData(16 * 1024 * 1024, 128, false)] // 16MB dict, 128 fast bytes, no end marker
    //    public void Lzma_WithVariousSharpCompressionSettings_RoundTrip(int dictionary, int numFastBytes, bool eos)
    //    {
    //        const int streamLen = 512 * 1024; // 512KB
    //        const int bufferSize = 64 * 1024;

    //        using (var data = new TestDataStream())
    //        using (var inHash = XXHash64.Create())
    //        using (var compHash = XXHash64.Create())
    //        using (var outHash = XXHash64.Create())
    //        using (var compMs = new MemoryStream())
    //        {
    //            // Map SharpCompression LzmaEncoderProperties to CompressionDictionaryOptions
    //            var options = CompressionOptions.DefaultCompressOptimal();
    //            options.Type = CompressionType.Level5;
    //            options.LeaveOpen = true;
    //            options.BufferSize = bufferSize;
    //            options.BlockSize = bufferSize;
    //            options.ThreadCount = 1;
                
    //            // Apply equivalent settings from LzmaEncoderProperties constructor
    //            options.Dictionary = new CompressionDictionaryOptions
    //            {
    //                DictionarySize = dictionary,           // CoderPropId.DictionarySize
    //                PositionBits = 2,                     // CoderPropId.PosStateBits (fixed at 2)
    //                LiteralContextBits = 3,               // CoderPropId.LitContextBits (fixed at 3)
    //                LiteralPositionBits = 0,              // CoderPropId.LitPosBits (fixed at 0)
    //                Algorithm = 2,                        // CoderPropId.Algorithm (fixed at 2)
    //                FastBytes = numFastBytes,             // CoderPropId.NumFastBytes (variable)
    //                BinaryTreeMode = 1,                   // CoderPropId.MatchFinder "bt4" = binary tree mode
    //                HashBytes = 4,                        // "bt4" implies 4 hash bytes
    //                WriteEndMarker = eos                  // CoderPropId.EndMarker (variable)
    //            };

    //            var decompOptions = new CompressionOptions
    //            {
    //                Type = CompressionType.Decompress,
    //                LeaveOpen = true,
    //                BufferSize = bufferSize,
    //                BlockSize = bufferSize
    //            };

    //            // compress & hash input
    //            using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compMs, options))
    //            using (var crypto = new CryptoStream(compStream, inHash, CryptoStreamMode.Write, true))
    //            {
    //                byte[] buf = new byte[bufferSize];
    //                int read;
    //                int total = 0;
    //                while (total < streamLen && (read = data.Read(buf, 0, Math.Min(buf.Length, streamLen - total))) > 0)
    //                {
    //                    crypto.Write(buf, 0, read);
    //                    total += read;
    //                }
    //                compStream.Complete();
    //                decompOptions.InitProperties = compStream.Properties;
    //            }

    //            long compressedBytes = compMs.Position;
    //            compMs.Position = 0;

    //            // hash compressed bytes
    //            using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
    //                compMs.CopyTo(cs);

    //            // decompress and hash output
    //            compMs.Position = 0;
    //            using (var decStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compMs, decompOptions))
    //            {
    //                byte[] outBuf = new byte[bufferSize];
    //                int r;
    //                while ((r = decStream.Read(outBuf, 0, outBuf.Length)) > 0)
    //                {
    //                    outHash.TransformBlock(outBuf, 0, r, null, 0);
    //                }
    //                outHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
    //            }

    //            string inHx = inHash.Hash!.ToHexString();
    //            string outHx = outHash.Hash!.ToHexString();

    //            Trace.WriteLine($"LZMA dict={dictionary:N0}, fb={numFastBytes}, eos={eos}: input={streamLen:N0} compressed={compressedBytes:N0}");
    //            Assert.Equal(inHx, outHx);
    //            Assert.True(compressedBytes > 0);
    //        }
    //    }


    //    [Fact]
    //    public void TempTest_CompressMcorpusBin_LzmaLevels()
    //    {
    //        const string inputFile = @"D:\Temp\mcorpus.bin";
    //        const int maxTestBytes = 1657415138; //1657415138; // 10 * 1024 * 1024; // Limit to 10MB for reasonable test time

    //        if (!File.Exists(inputFile))
    //        {
    //            Trace.WriteLine($"Test file not found: {inputFile}");
    //            return; // Skip test if file doesn't exist
    //        }

    //        var levels = new (CompressionType Level, string Name)[]
    //        {
    //            (CompressionType.Level1, "L1"),
    //            //(CompressionType.Level5, "L5"),
    //            //(CompressionType.Level9, "L9"),
    //        };

    //        // Read limited amount of data for testing
    //        byte[] inputData;
    //        using (var fs = File.OpenRead(inputFile))
    //        {
    //            long fileSize = fs.Length;
    //            int testSize = (int)Math.Min(fileSize, maxTestBytes);
    //            inputData = new byte[testSize]; 
    //            fs.Read(inputData, 0, testSize);
    //        }

    //        using (var inputHash = XXHash64.Create())
    //        {
    //            // Calculate input data hash
    //            string inputHx = inputHash.ComputeHash(inputData).ToHexString();
    //            long totalFileSize = new FileInfo(inputFile).Length;

    //            Trace.WriteLine($"=== LZMA Compression Test for mcorpus.bin (limited test) ===");
    //            Trace.WriteLine($"Input file: {inputFile}");
    //            Trace.WriteLine($"Total file size: {totalFileSize:N0} bytes");
    //            Trace.WriteLine($"Test size: {inputData.Length:N0} bytes");
    //            Trace.WriteLine($"Input hash: {inputHx}");
    //            Trace.WriteLine("");


    //            foreach (var level in levels)
    //            {
    //                // Test both with and without explicit dictionary settings
    //                var testConfigs = new (string Name, CompressionOptions Options)[]
    //                {
    //                    ("With Dictionary", new CompressionOptions
    //                    {
    //                        Type = level.Level,
    //                        LeaveOpen = true,
    //                        BufferSize = 4 * 1024 * 1024, // 4MB buffer
    //                        BlockSize = -1,
    //                        ThreadCount = 1,
    //                        Dictionary = new CompressionDictionaryOptions
    //                        {
    //                            DictionarySize = 4 * 1024 * 1024, //256 * 1024,  // 256KB - smaller for speed
    //                            FastBytes = 32,                // LZMA default for level >= 7 (should be 32 for level < 7)
    //                            LiteralContextBits = 3,        // lc=3 (LZMA default)
    //                            LiteralPositionBits = 0,       // lp=0 (LZMA default)
    //                            PositionBits = 2,              // pb=2 (LZMA default)
    //                            Algorithm = 0,                 // Fast algorithm for speed
    //                            BinaryTreeMode = 0,            // Hash chain for speed
    //                            HashBytes = 4,                 // 4 hash bytes (bt4)
    //                            WriteEndMarker = false         // No end marker by default
    //                        }
    //                    }),
    //                    ("No Dictionary", new CompressionOptions
    //                    {
    //                        Type = level.Level,
    //                        LeaveOpen = true,
    //                        BufferSize = 4 * 1024 * 1024, // 4MB buffer
    //                        BlockSize = -1,
    //                        ThreadCount = 1
    //                    }),

    //                };

    //                foreach (var config in testConfigs)
    //                {
    //                    using (var inputMs = new MemoryStream(inputData))
    //                    using (var compressedMs = new MemoryStream())
    //                    using (var decompressedMs = new MemoryStream())
    //                    using (var compHash = XXHash64.Create())
    //                    using (var outHash = XXHash64.Create())
    //                    {
    //                        var stopwatch = Stopwatch.StartNew();

    //                        var decompressOptions = new CompressionOptions
    //                        {
    //                            Type = CompressionType.Decompress,
    //                            LeaveOpen = true,
    //                            BufferSize = 4 * 1024 * 1024,
    //                            BlockSize = 4 * 1024 * 1024
    //                        };

    //                        // Compress
    //                        using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compressedMs, config.Options))
    //                        {
    //                            Trace.WriteLine($"Level {level.Name} - {config.Name}:");
    //                            Trace.WriteLine($"  Properties: {BitConverter.ToString(compStream.Properties).Replace("-", "")}");
                                
    //                            inputMs.CopyTo(compStream);
    //                            compStream.Complete();
    //                            decompressOptions.InitProperties = compStream.Properties;
    //                        }

    //                        var compressTime = stopwatch.Elapsed;
    //                        long compressedSize = compressedMs.Position;
    //                        compressedMs.Position = 0;

    //                        // Hash compressed data
    //                        using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
    //                        {
    //                            compressedMs.CopyTo(cs);
    //                        }
    //                        compressedMs.Position = 0;

    //                        stopwatch.Restart();

    //                        // Decompress
    //                        using (var decompStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compressedMs, decompressOptions))
    //                        using (var crypto = new CryptoStream(decompStream, outHash, CryptoStreamMode.Read, true))
    //                        {
    //                            crypto.CopyTo(decompressedMs);
    //                        }

    //                        var decompressTime = stopwatch.Elapsed;
    //                        stopwatch.Stop();

    //                        string compHx = compHash.Hash!.ToHexString();
    //                        string outHx = outHash.Hash!.ToHexString();
    //                        double ratio = (double)compressedSize / inputData.Length;

    //                        Trace.WriteLine($"  Compressed size: {compressedSize:N0} bytes");
    //                        Trace.WriteLine($"  Compression ratio: {ratio:F4} ({(1.0 - ratio) * 100:F2}% reduction)");
    //                        Trace.WriteLine($"  Compress time: {compressTime.TotalMilliseconds:F1} ms");
    //                        Trace.WriteLine($"  Decompress time: {decompressTime.TotalMilliseconds:F1} ms");
    //                        Trace.WriteLine($"  Compressed hash: {compHx}");
    //                        Trace.WriteLine($"  Decompressed hash: {outHx}");
    //                        Trace.WriteLine($"  Round-trip success: {inputHx == outHx}");
    //                        Trace.WriteLine("");

    //                        // Verify round-trip integrity
    //                        //Assert.Equal(inputHx, outHx);
    //                        //Assert.Equal(inputData.Length, decompressedMs.Length);
    //                        //Assert.True(compressedSize > 0);
    //                        //Assert.True(compressedSize < inputData.Length); // Should achieve some compression
    //                    }
    //                }
    //            }
    //        }
    //    }

    //    [Fact]
    //    public void VerifyNativeNormalizationFix()
    //    {
    //        // Test that different compression levels now produce different properties when no explicit dictionary is provided
    //        const int streamLen = 512 * 1024; // 512KB
    //        const int bufferSize = 64 * 1024;

    //        var levelResults = new List<(CompressionType Level, string Properties, string CompressedHash, long CompressedSize, TimeSpan CompressTime)>();

    //        var levels = new CompressionType[] { CompressionType.Level1, CompressionType.Level5, CompressionType.Level9 };

    //        foreach (var level in levels)
    //        {
    //            using (var data = new TestDataStream()) // Create new stream for each level
    //            using (var compMs = new MemoryStream())
    //            using (var compHash = XXHash64.Create())
    //            {
    //                var options = new CompressionOptions
    //                {
    //                    Type = level,
    //                    LeaveOpen = true,
    //                    BufferSize = bufferSize,
    //                    BlockSize = bufferSize,
    //                    ThreadCount = 1
    //                    // No explicit Dictionary - should use native normalization defaults
    //                };

    //                var stopwatch = Stopwatch.StartNew();

    //                // Compress
    //                using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compMs, options))
    //                {
    //                    byte[] buf = new byte[bufferSize];
    //                    int total = 0;
    //                    int read;
    //                    while (total < streamLen && (read = data.Read(buf, 0, Math.Min(buf.Length, streamLen - total))) > 0)
    //                    {
    //                        compStream.Write(buf, 0, read);
    //                        total += read;
    //                    }
    //                    compStream.Complete();

    //                    stopwatch.Stop();
    //                    var compressTime = stopwatch.Elapsed;

    //                    string propsHex = BitConverter.ToString(compStream.Properties).Replace("-", "");
    //                    long compressedSize = compMs.Position;
                        
    //                    compMs.Position = 0;
    //                    using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
    //                    {
    //                        compMs.CopyTo(cs);
    //                        cs.FlushFinalBlock();
    //                    }

    //                    levelResults.Add((level, propsHex, compHash.Hash!.ToHexString(), compressedSize, compressTime));
    //                }
    //            }
    //        }

    //        // Output results for verification
    //        Trace.WriteLine("=== Native Normalization Fix Verification ===");
    //        Trace.WriteLine($"Input size: {streamLen:N0} bytes");
    //        foreach (var result in levelResults)
    //        {
    //            double ratio = (double)result.CompressedSize / streamLen;
    //            Trace.WriteLine($"Level {result.Level}:");
    //            Trace.WriteLine($"  Properties: {result.Properties}");
    //            Trace.WriteLine($"  Compressed size: {result.CompressedSize:N0} bytes");
    //            Trace.WriteLine($"  Compression ratio: {ratio:F4} ({(1.0 - ratio) * 100:F2}% reduction)");
    //            Trace.WriteLine($"  Compress time: {result.CompressTime.TotalMilliseconds:F1} ms");
    //            Trace.WriteLine($"  Compressed hash: {result.CompressedHash}");
    //            Trace.WriteLine("");
    //        }

    //        // Verify that different levels produce different compressed outputs (indicating different settings)
    //        var distinctHashes = new HashSet<string>(levelResults.Select(r => r.CompressedHash));
    //        Assert.True(distinctHashes.Count > 1, "Expected different compressed outputs for different compression levels, indicating native normalization is working");

    //        // Verify that properties are reasonable (not all zeros or invalid)
    //        foreach (var result in levelResults)
    //        {
    //            Assert.True(result.Properties.Length >= 10, $"Properties for {result.Level} seem too short: {result.Properties}");
    //            Assert.False(result.Properties == "0000000000", $"Properties for {result.Level} should not be all zeros: {result.Properties}");
    //        }

    //        // Verify that compressed sizes are different (indicating different compression efficiency)
    //        var distinctSizes = new HashSet<long>(levelResults.Select(r => r.CompressedSize));
    //        Assert.True(distinctSizes.Count > 1, "Expected different compressed sizes for different compression levels");
    //    }

    //    [Fact]
    //    public void Debug_CompareNativeVsExplicitDictionary()
    //    {
    //        const int testSize = 1024 * 1024; // 1MB test
            
    //        // Create test data
    //        byte[] testData = TestDataStream.Create(testSize);
            
    //        var results = new List<(string Config, string Properties, long CompressedSize, TimeSpan Time, string Hash)>();
            
    //        var configs = new (string Name, CompressionOptions Options)[]
    //        {
    //            ("Native Normalization", new CompressionOptions
    //            {
    //                Type = CompressionType.Level1,
    //                LeaveOpen = true,
    //                BufferSize = 4 * 1024 * 1024,
    //                BlockSize = -1,
    //                ThreadCount = 1
    //                // No Dictionary = native normalization
    //            }),
    //            ("Explicit Fast", new CompressionOptions  
    //            {
    //                Type = CompressionType.Level1,
    //                LeaveOpen = true,
    //                BufferSize = 4 * 1024 * 1024,
    //                BlockSize = -1,
    //                ThreadCount = 1,
    //                Dictionary = new CompressionDictionaryOptions
    //                {
    //                    DictionarySize = 4 * 1024 * 1024,
    //                    FastBytes = 32,               // Level1 should probably use fewer
    //                    Algorithm = 0,                // Fast algorithm
    //                    BinaryTreeMode = 0,           // Hash chain (fast)
    //                    HashBytes = 4,
    //                    WriteEndMarker = false
    //                }
    //            }),
    //            ("Explicit Optimized", new CompressionOptions
    //            {
    //                Type = CompressionType.Level1,
    //                LeaveOpen = true,
    //                BufferSize = 4 * 1024 * 1024,
    //                BlockSize = -1,
    //                ThreadCount = 1,
    //                Dictionary = new CompressionDictionaryOptions
    //                {
    //                    DictionarySize = 4 * 1024 * 1024,
    //                    FastBytes = 16,               // Less for Level1
    //                    Algorithm = 0,                // Fast algorithm  
    //                    BinaryTreeMode = 0,           // Hash chain (faster)
    //                    HashBytes = 3,                 // Less hash bytes
    //                    WriteEndMarker = false
    //                }
    //            })
    //        };
            
    //        foreach (var config in configs)
    //        {
    //            using (var inputMs = new MemoryStream(testData))
    //            using (var compressedMs = new MemoryStream())
    //            using (var compHash = XXHash64.Create())
    //            {
    //                var stopwatch = Stopwatch.StartNew();
                    
    //                // Compress
    //                using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compressedMs, config.Options))
    //                {
    //                    inputMs.CopyTo(compStream);
    //                    compStream.Complete();
                        
    //                    stopwatch.Stop();
                        
    //                    string propsHex = BitConverter.ToString(compStream.Properties).Replace("-", "");
    //                    long compressedSize = compressedMs.Position;
    //                    compressedMs.Position = 0;
                        
    //                    using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
    //                    {
    //                        compressedMs.CopyTo(cs);
    //                        cs.FlushFinalBlock();
    //                    }
                        
    //                    results.Add((config.Name, propsHex, compressedSize, stopwatch.Elapsed, compHash.Hash!.ToHexString()));
    //                }
    //            }
    //        }
            
    //        Trace.WriteLine("=== Debug: Native vs Explicit Dictionary Comparison ===");
    //        foreach (var result in results)
    //        {
    //            double ratio = (double)result.CompressedSize / testSize;
    //            Trace.WriteLine($"{result.Config}:");
    //            Trace.WriteLine($"  Properties: {result.Properties}");
    //            Trace.WriteLine($"  Compressed size: {result.CompressedSize:N0} bytes ({ratio:F4} ratio)");
    //            Trace.WriteLine($"  Compression time: {result.Time.TotalMilliseconds:F1} ms");
    //            Trace.WriteLine($"  Compressed hash: {result.Hash}");
    //            Trace.WriteLine("");
    //        }
            
    //        // The test should pass - we're just gathering diagnostic info
    //        Assert.True(results.Count == 3);
    //    }

    //    [Fact]
    //    public void Diagnose_McorpusCompression_DetailedAnalysis()
    //    {
    //        const string inputFile = @"D:\Temp\mcorpus.bin";
    //        const int testSize = 1024 * 1024; // 1MB for quick testing
            
    //        if (!File.Exists(inputFile))
    //        {
    //            Trace.WriteLine($"Test file not found: {inputFile}");
    //            return;
    //        }

    //        // Read test data
    //        byte[] inputData = new byte[testSize];
    //        using (var fs = File.OpenRead(inputFile))
    //        {
    //            fs.Read(inputData, 0, testSize);
    //        }

    //        Trace.WriteLine("=== Detailed Analysis: Native vs Explicit Dictionary ===");
    //        Trace.WriteLine($"Input size: {testSize:N0} bytes");
    //        Trace.WriteLine("");

    //        // Test configurations that should theoretically produce the same results
    //        var configs = new (string Name, CompressionOptions Options)[]
    //        {
    //            ("Native L1", new CompressionOptions
    //            {
    //                Type = CompressionType.Level1,
    //                LeaveOpen = true,
    //                BufferSize = 4 * 1024 * 1024,
    //                BlockSize = -1,
    //                ThreadCount = 1
    //                // No Dictionary - should use native normalization
    //            }),
    //            ("Native L5", new CompressionOptions
    //            {
    //                Type = CompressionType.Level5,
    //                LeaveOpen = true,
    //                BufferSize = 4 * 1024 * 1024,
    //                BlockSize = -1,
    //                ThreadCount = 1
    //                // No Dictionary - should use native normalization
    //            }),
    //            ("Native L9", new CompressionOptions
    //            {
    //                Type = CompressionType.Level9,
    //                LeaveOpen = true,
    //                BufferSize = 4 * 1024 * 1024,
    //                BlockSize = -1,
    //                ThreadCount = 1
    //                // No Dictionary - should use native normalization
    //            }),
    //            ("Explicit L1-style", new CompressionOptions
    //            {
    //                Type = CompressionType.Level1,
    //                LeaveOpen = true,
    //                BufferSize = 4 * 1024 * 1024,
    //                BlockSize = -1,
    //                ThreadCount = 1,
    //                Dictionary = new CompressionDictionaryOptions
    //                {
    //                    DictionarySize = 4 * 1024 * 1024,
    //                    FastBytes = 16,        // Less for Level1
    //                    Algorithm = 0,         // Fast algorithm
    //                    BinaryTreeMode = 0,    // Hash chain
    //                    HashBytes = 3,         // Fewer hash bytes
    //                    LiteralContextBits = 3,
    //                    LiteralPositionBits = 0,
    //                    PositionBits = 2,
    //                    WriteEndMarker = false
    //                }
    //            }),
    //            ("Your Explicit", new CompressionOptions
    //            {
    //                Type = CompressionType.Level1,
    //                LeaveOpen = true,
    //                BufferSize = 4 * 1024 * 1024,
    //                BlockSize = -1,
    //                ThreadCount = 1,
    //                Dictionary = new CompressionDictionaryOptions
    //                {
    //                    DictionarySize = 4 * 1024 * 1024,
    //                    FastBytes = 32,        // Your setting
    //                    Algorithm = 0,         // Fast algorithm
    //                    BinaryTreeMode = 0,    // Hash chain
    //                    HashBytes = 4,         // Your setting
    //                    LiteralContextBits = 3,
    //                    LiteralPositionBits = 0,
    //                    PositionBits = 2,
    //                    WriteEndMarker = false
    //                }
    //            })
    //        };
            
    //        foreach (var config in configs)
    //        {
    //            using (var inputMs = new MemoryStream(inputData))
    //            using (var compressedMs = new MemoryStream())
    //            using (var compHash = XXHash64.Create())
    //            {
    //                var stopwatch = Stopwatch.StartNew();
                    
    //                using (var compStream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, compressedMs, config.Options))
    //                {
    //                    inputMs.CopyTo(compStream);
    //                    compStream.Complete();
                        
    //                    stopwatch.Stop();
                        
    //                    string propsHex = BitConverter.ToString(compStream.Properties).Replace("-", "");
    //                    long compressedSize = compressedMs.Position;
    //                    compressedMs.Position = 0;
                        
    //                    using (var cs = new CryptoStream(Stream.Null, compHash, CryptoStreamMode.Write, true))
    //                    {
    //                        compressedMs.CopyTo(cs);
    //                        cs.FlushFinalBlock();
    //                    }
                        
    //                    double ratio = (double)compressedSize / testSize;
    //                    Trace.WriteLine($"{config.Name}:");
    //                    Trace.WriteLine($"  Properties: {propsHex}");
    //                    Trace.WriteLine($"  Compressed: {compressedSize:N0} bytes ({ratio:F4} ratio)");
    //                    Trace.WriteLine($"  Time: {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
    //                    Trace.WriteLine($"  Hash: {compHash.Hash!.ToHexString()}");
    //                    Trace.WriteLine("");
    //                }
    //            }
    //        }
            
    //        Assert.True(true); // Always pass - this is diagnostic
    //    }

    //    [Fact]
    //    public void LzmaBlock_VerifyNativeNormalizationFix()
    //    {
    //        const int testSize = 64 * 1024; // 64KB test data
            
    //        // Create test data
    //        byte[] testData = TestDataStream.Create(testSize);
            
    //        var results = new List<(string Config, string Properties, long CompressedSize, TimeSpan Time)>();
            
    //        var levels = new CompressionType[] { CompressionType.Level1, CompressionType.Level5, CompressionType.Level9 };
            
    //        foreach (var level in levels)
    //        {
    //            var configs = new (string Name, CompressionOptions Options)[]
    //            {
    //                ("Native", new CompressionOptions
    //                {
    //                    Type = level,
    //                    BlockSize = testSize, // Still need BlockSize for output buffer calculation
    //                    ThreadCount = 1
    //                    // No Dictionary - should use native normalization for all settings except dictSize
    //                }),
    //                ("Native-No-Dict", new CompressionOptions
    //                {
    //                    Type = level,
    //                    BlockSize = testSize,
    //                    ThreadCount = 1,
    //                    Dictionary = new CompressionDictionaryOptions
    //                    {
    //                        DictionarySize = 0  // 0 means use level-based default
    //                    }
    //                }),
    //                ("Explicit", new CompressionOptions
    //                {
    //                    Type = level,
    //                    BlockSize = testSize,
    //                    ThreadCount = 1,
    //                    Dictionary = new CompressionDictionaryOptions
    //                    {
    //                        DictionarySize = testSize,
    //                        FastBytes = 32,
    //                        Algorithm = 0,
    //                        BinaryTreeMode = 0,
    //                        HashBytes = 4,
    //                        WriteEndMarker = false
    //                    }
    //                })
    //            };
                
    //            foreach (var config in configs)
    //            {
    //                using (var block = new LzmaBlock(config.Options))
    //                {
    //                    var stopwatch = Stopwatch.StartNew();
                        
    //                    byte[] compressed = BufferPool.Rent(block.RequiredCompressOutputSize);
    //                    int compressedLength = compressed.Length;
                        
    //                    var result = block.Compress(testData, 0, testData.Length, compressed, 0, ref compressedLength);
                        
    //                    stopwatch.Stop();
                        
    //                    Assert.Equal(CompressionResultCode.Success, result);
                        
    //                    string propsHex = BitConverter.ToString(block.Properties).Replace("-", "");
                        
    //                    results.Add(($"{level}-{config.Name}", propsHex, compressedLength, stopwatch.Elapsed));
                        
    //                    BufferPool.Return(compressed);
    //                }
    //            }
    //        }
            
    //        Trace.WriteLine("=== LzmaBlock Native Normalization Verification ===");
    //        foreach (var result in results)
    //        {
    //            double ratio = (double)result.CompressedSize / testSize;
    //            Trace.WriteLine($"{result.Config}:");
    //            Trace.WriteLine($"  Properties: {result.Properties}");
    //            Trace.WriteLine($"  Compressed: {result.CompressedSize:N0} bytes ({ratio:F4} ratio)");
    //            Trace.WriteLine($"  Time: {result.Time.TotalMilliseconds:F1} ms");
    //            Trace.WriteLine("");
    //        }
            
    //        // Verify different levels produce different results when using native normalization  
    //        var nativeResults = results.Where(r => r.Config.Contains("Native-No-Dict")).ToList();
    //        var distinctSizes = new HashSet<long>(nativeResults.Select(r => r.CompressedSize));
    //        Assert.True(distinctSizes.Count > 1, "Expected different compressed sizes for different levels with native normalization");
            
    //        // Verify properties are valid (not all zeros)
    //        foreach (var result in results)
    //        {
    //            Assert.True(result.Properties.Length >= 10, $"Properties for {result.Config} seem too short: {result.Properties}");
    //            Assert.False(result.Properties == "0000000000", $"Properties for {result.Config} should not be all zeros: {result.Properties}");
    //        }
    //    }
    }
#endif
}
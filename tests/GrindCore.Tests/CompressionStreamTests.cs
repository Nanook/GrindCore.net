using Utl = GrindCore.Tests.Utility.Utilities;
using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using System.Diagnostics;
using System.Security.Cryptography;
using Nanook.GrindCore.ZLib;
using Xunit;
using Nanook.GrindCore.Lzma;
using System;
using GrindCore.Tests.Utility;

namespace GrindCore.Tests
{
    public sealed class CompressionStreamTests
    {

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1f6, "1fd0ab5c0058d51a")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x3cd, "eefbb1f99743a612")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x4bf, "ca5207caef02504d")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x1ea, "e8dcd55f29d31d27")]
        //[InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x29a, "72f75d0b96ea09ee")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x1de, "6d7905044230ec1f")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1e5, "b4550df1c1bd0067")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x3d3, "faf781046fb77de8")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x4c5, "1c5c2490ab900308")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x197, "6f32a8f86c2c0f8b")]
#else
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1f6, "1fd0ab5c0058d51a")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x19b, "e39f3f4d64825537")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x18a, "03e8b1d250f7e6aa")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x3cd, "eefbb1f99743a612")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x4bf, "ca5207caef02504d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x1ea, "e8dcd55f29d31d27")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x1ea, "4ffd75974e4d0d93")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x1ea, "bfb715f62e1a0e6b")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x29a, "72f75d0b96ea09ee")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0x29a, "55372f7b165ee9e8")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0x29a, "55372f7b165ee9e8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x1de, "6d7905044230ec1f")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x1de, "069b2a2799eadee8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x1de, "069b2a2799eadee8")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1e5, "b4550df1c1bd0067")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x1e5, "3e6f77f9c11f4e70")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x1e5, "3e6f77f9c11f4e70")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x3d3, "faf781046fb77de8")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x305, "a3c36ab37f8f236d")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x305, "a21b9fa33c110bc5")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x4c5, "1c5c2490ab900308")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x305, "a3c36ab37f8f236d")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x305, "a21b9fa33c110bc5")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x197, "6f32a8f86c2c0f8b")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x197, "7dd3bfedab192873")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x197, "e6b4aafc1efa2266")]
#endif
        public void Data_Stream64KiB(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string compXxH64)
        {
            int streamLen = 64 * 1024; // Total bytes to process
            int bufferSize = 64 * 1024;

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                //Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x2, "fae4a10ff02fd326")]
        //[InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0xf, "0de3431ec7da9349")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x9, "ea7ad91258c4ea87")]
#else
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0xb, "c387a340004c1b0d")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0xb, "c387a340004c1b0d")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0xb, "9810acdea8d71804")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x8, "65f2e912ed28c1ed")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x8, "31edcf2ea90ea820")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x8, "65f2e912ed28c1ed")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x8, "31edcf2ea90ea820")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x9, "ea7ad91258c4ea87")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x9, "22c883899be40e7c")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x9, "8559f845bc0c4f54")]
#endif
        public void Text_StreamEmpty(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string compXxH64)
        {
            int streamLen = 0; // Total bytes to process
            int bufferSize = 0x100;

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                //Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x84d, "25be05c704cb5995")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x1db4, "2464d64063e022ba")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x264c, "728b6f680101e18d")]
        //[InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x16e8, "a0783a6c336c8bd9")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x632, "1ee9e334582493e9")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x636, "eca2b056732b6c26")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x5e5, "cdd2efd3865069b2")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x1dba, "bdd8b43a296a44ad")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x2652, "f2324065e2e34d09")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x827, "6227887e1bc483a0")]
#else
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x84d, "25be05c704cb5995")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x5d1, "2b444156a4305ae3")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x4fa, "bd7a15fc895f1b65")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x1db4, "2464d64063e022ba")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0xcc2, "88b181dc28558433")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0xb9b, "080ef351410b77ac")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x264c, "728b6f680101e18d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0xbe1, "8fbdcf11b9e9fcb4")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0xb9b, "080ef351410b77ac")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x16e8, "a0783a6c336c8bd9")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0xebf, "a64c1f527824c624")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0xebc, "62e511e4bd89818b")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x632, "1ee9e334582493e9")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x64e, "e36983b8df1f0fa0")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x64e, "e36983b8df1f0fa0")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x636, "eca2b056732b6c26")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x605, "9ac9ca397bdbf54b")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x647, "1b1484110fe9391a")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x5e5, "cdd2efd3865069b2")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x733, "347363ee8e7e7f1d")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x733, "0f256c67b74d6676")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x1dba, "bdd8b43a296a44ad")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0xcc8, "aa99d2252c2f6606")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0xba1, "2b9ecf7dce8e81ce")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x2652, "f2324065e2e34d09")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0xbe7, "bd93e4482eb778cb")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0xba1, "2b9ecf7dce8e81ce")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x827, "6227887e1bc483a0")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x7f2, "b18fa96dc3b4708d")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x5c2, "5b61c22239bba82c")]
#endif
        public void Text_Stream64KiB(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string compXxH64)
        {
            int streamLen = 64 * 1024; // Total bytes to process
            int bufferSize = 64 * 1024;

            using (var data = new TestPseudoTextStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                //Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

#if !IS_32BIT
        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x600014, "6aaba9c27838a268", "d3d5fdf377e15940")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x60077b, "6aaba9c27838a268", "6cd757c2d4c89c1c")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x654013, "6aaba9c27838a268", "85d2edff8cc11b12")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x600147, "6aaba9c27838a268", "d879fa782d69a9d1")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x60018b, "6aaba9c27838a268", "5439ee2aa1e5f765")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x6160ee, "6aaba9c27838a268", "a434182c245fd148")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x60018d, "6aaba9c27838a268", "9f24550a997d41c8")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x600781, "6aaba9c27838a268", "4d650db1242fefec")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x654019, "6aaba9c27838a268", "503de593cc2df76a")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x600099, "6aaba9c27838a268", "d033cda5805f142b")]
        public void DataNonCompressible_Stream6MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxh64, string compXxH64)
        {
            int streamLen = 6 * 1024 * 1024; // Total bytes to process
            int bufferSize = 1 * 1024 * 1024; // 1MiB block size

            using (var data = new TestNonCompressibleDataStream())
            {
                var x = TestNonCompressibleDataStream.Create(streamLen);

                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(rawXxh64, r.InHash); //test raw data hash matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Level0, 0xc32a, "5cb9b63eb9a4c344", "ec081c314d14bcb1")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Level0, 0x6001e5, "5cb9b63eb9a4c344", "3440f5b2cbb3990d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Level0, 0x6001e5, "5cb9b63eb9a4c344", "3440f5b2cbb3990d")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Level0, 0x1f2d, "5cb9b63eb9a4c344", "d901c9e905f2ed23")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Level0, 0x41d61, "5cb9b63eb9a4c344", "b1cd187b8091400c")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Level0, 0x230b, "5cb9b63eb9a4c344", "8257184f24479e50")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Level0, 0x25f6, "5cb9b63eb9a4c344", "fc2e2fcb6505c945")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Level0, 0x6001eb, "5cb9b63eb9a4c344", "0a28fcae3b408197")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Level0, 0x6001eb, "5cb9b63eb9a4c344", "0a28fcae3b408197")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Level0, 0x5778, "5cb9b63eb9a4c344", "e2b8c5961872ad4c")]
        public void NoCompression_Stream6MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxh64, string compXxH64)
        {
            int streamLen = 6 * 1024 * 1024; // Total bytes to process
            int bufferSize = 1 * 1024 * 1024; // 1MiB block size

            using (var data = new TestPseudoTextStream())
            {
                var x = TestNonCompressibleDataStream.Create(streamLen);

                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(rawXxh64, r.InHash); //test raw data hash matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x2030, "7833322f45651d24", "4095103af13e29d7")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0xb499, "7833322f45651d24", "3aa7fa616a0a0beb")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x1371d, "7833322f45651d24", "4e21fd2ecc76dda6")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x6e51, "7833322f45651d24", "4f4908addd1991a1")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x54e, "7833322f45651d24", "635862db00fae132")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0xea7, "7833322f45651d24", "08cf4de3f6733691")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x9e3, "7833322f45651d24", "4232839926c89dcf")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0xb49f, "7833322f45651d24", "6971719677322556")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x13723, "7833322f45651d24", "34a0eaac76a7fd87")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x3d4, "7833322f45651d24", "ae6f556cb2489ead")]
        public void Data_Stream6MiB_ReadByteWriteByte(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            int streamLen = 6 * 1024 * 1024; // Total bytes to process
            int bufferSize = 1 * 1024 * 1024; // 1MiB block size

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBytes(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Copy, CompressionType.Fastest, 0x1400000, "6d522dca7d96dfe8", "6d522dca7d96dfe8")]
        [InlineData(CompressionAlgorithm.Copy, CompressionType.Optimal, 0x1400000, "6d522dca7d96dfe8", "6d522dca7d96dfe8")]
        [InlineData(CompressionAlgorithm.Copy, CompressionType.SmallestSize, 0x1400000, "6d522dca7d96dfe8", "6d522dca7d96dfe8")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x6b42, "6d522dca7d96dfe8", "0a1ce1dc6372e770")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1b5, "6d522dca7d96dfe8", "d20488bddff3a34b")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1a5, "6d522dca7d96dfe8", "9dfc18b1eae394b7")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x2557b, "6d522dca7d96dfe8", "21a9545122b4336f")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x16749, "6d522dca7d96dfe8", "3a6e1acfa971804d")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x16749, "6d522dca7d96dfe8", "3a6e1acfa971804d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x40976, "6d522dca7d96dfe8", "a519415c475fd0d2")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x16749, "6d522dca7d96dfe8", "3a6e1acfa971804d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x16749, "6d522dca7d96dfe8", "3a6e1acfa971804d")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x208b, "6d522dca7d96dfe8", "eceddb5fb719e23f")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x132a, "6d522dca7d96dfe8", "b14b454b8273d87c")]
        // too slow [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x132a, "6d522dca7d96dfe8", "de68e77100a71261")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x16f9f, "6d522dca7d96dfe8", "b9dbe31b7aaa178a")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0x332cb, "6d522dca7d96dfe8", "6f07ae04ef765603")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0x332cb, "6d522dca7d96dfe8", "6f07ae04ef765603")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0xd65, "6d522dca7d96dfe8", "de79b5fd4314f0e5")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0xd66, "6d522dca7d96dfe8", "a975d12987fde7eb")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0xd66, "6d522dca7d96dfe8", "a975d12987fde7eb")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x30d5, "6d522dca7d96dfe8", "cb07ceedc0627fcc")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x30d5, "6d522dca7d96dfe8", "be03df0742f1daeb")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x30d5, "6d522dca7d96dfe8", "be03df0742f1daeb")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x25581, "6d522dca7d96dfe8", "7b0de0f3ce743798")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x1674f, "6d522dca7d96dfe8", "7266f745c4d9b110")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "ac87e7997d39673a")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x4097c, "6d522dca7d96dfe8", "e157de4720ea78e2")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x1674f, "6d522dca7d96dfe8", "7266f745c4d9b110")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "ac87e7997d39673a")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x90b, "6d522dca7d96dfe8", "5690b6b13a63fa53")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x90b, "6d522dca7d96dfe8", "8c2c44e66f7f5ff0")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x86e, "6d522dca7d96dfe8", "060d59864bfd9550")]
        public async Task Data_Stream20MiB_Chunk1MiB_Async(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            int streamLen = 20 * 1024 * 1024; // Total bytes to process
            int bufferSize = 1 * 1024 * 1024; // 1MiB block size

            using (var data = new TestDataStream())
            {
                TestResults r = await Utl.TestStreamBlocksAsync(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

#if WIN_X64
        [Theory]
        //[InlineData(CompressionAlgorithm.Copy, CompressionType.Fastest, 512 * 1024 * 1024, "c668fabe6e6e9235", "c668fabe6e6e9235")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0xaba27, "c668fabe6e6e9235", "f11172f7d39c66ff")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x3b918d, "c668fabe6e6e9235", "78d7773f85aa8e7c")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x673322, "c668fabe6e6e9235", "2be7825c1fffe4d2")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x3214b, "c668fabe6e6e9235", "4075fff2db91dc02")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x24c20b, "c668fabe6e6e9235", "b9cbe71dc4731422")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x129aa, "c668fabe6e6e9235", "150b42d11de57fc7")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x4e201, "c668fabe6e6e9235", "4ae0151988b74cae")]
        [InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x3b919f, "c668fabe6e6e9235", "527b08f1d7436fcb")]
        [InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x673334, "c668fabe6e6e9235", "73866ae185a2ca00")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x3b9193, "c668fabe6e6e9235", "a951bcf26245af42")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x673328, "c668fabe6e6e9235", "acd9eae8637f56d9")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0xc18b, "c668fabe6e6e9235", "2cfc65995205f07d")]
        public void Data_Stream512MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            int streamLen = 512 * 1024 * 1024; // Total bytes to process
            int bufferSize = 1 * 1024 * 1024; // 1MiB block size

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x129aa, "c668fabe6e6e9235", "150b42d11de57fc7")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x138c1, "c668fabe6e6e9235", "cc526df9a9d44632")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x29dc9, "c668fabe6e6e9235", "d2ec321fdb0dc743")]
        public void Data_Stream512MiB_Chunk128MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            int streamLen = 512 * 1024 * 1024; // Total bytes to process
            int bufferSize = 128 * 1024 * 1024; // 128MiB block size

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

#endif
#endif

    }
}

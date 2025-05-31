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
        private static byte[] _dataEmpty;
        private static byte[] _data64KiB;
        private static byte[] _text64KiB;

        static CompressionStreamTests()
        {
            _dataEmpty = new byte[0];
            _data64KiB = TestDataStream.Create(64 * 1024);
            _text64KiB = TestPseudoTextStream.Create(64 * 1024);
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x84d, "25be05c704cb5995")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x1db4, "2464d64063e022ba")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x264c, "728b6f680101e18d")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x632, "1ee9e334582493e9")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x1dba, "bdd8b43a296a44ad")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x2652, "f2324065e2e34d09")]
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
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x632, "1ee9e334582493e9")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x64e, "e36983b8df1f0fa0")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x64e, "e36983b8df1f0fa0")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x636, "eca2b056732b6c26")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x605, "9ac9ca397bdbf54b")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x647, "1b1484110fe9391a")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x5e5, "cdd2efd3865069b2")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x733, "347363ee8e7e7f1d")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x733, "0f256c67b74d6676")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x1dc6, "2baeb36442c0367d")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Optimal, 0xcd4, "e1f838db8a9218b9")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.SmallestSize, 0xbad, "b2f20d23bf119caa")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x265e, "acaac697d8fad04f")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Optimal, 0xbf3, "120d4661eec0d065")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.SmallestSize, 0xbad, "b2f20d23bf119caa")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x1dba, "bdd8b43a296a44ad")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0xcc8, "aa99d2252c2f6606")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0xba1, "2b9ecf7dce8e81ce")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x2652, "f2324065e2e34d09")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0xbe7, "bd93e4482eb778cb")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0xba1, "2b9ecf7dce8e81ce")]
#endif
        public void Text_Stream64KiB(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            int streamLen = 64 * 1024; // Total bytes to process
            int bufferSize = 64 * 1024;

            using (var data = new TestPseudoTextStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                //Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                //Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1f6, "1fd0ab5c0058d51a")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x3cd, "eefbb1f99743a612")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x4bf, "ca5207caef02504d")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x1ea, "e8dcd55f29d31d27")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x29e, "5f7ee7aedc2cb44c")]
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
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x29e, "5f7ee7aedc2cb44c")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x1de, "6d7905044230ec1f")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x1de, "069b2a2799eadee8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x1de, "069b2a2799eadee8")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1e5, "b4550df1c1bd0067")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x1e5, "3e6f77f9c11f4e70")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x1e5, "3e6f77f9c11f4e70")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x3df, "23cc51e8d014ea83")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Optimal, 0x311, "dd79ecbbf6270f98")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.SmallestSize, 0x311, "e72c2161819ae447")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x4d1, "dd877cb678c20ac8")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Optimal, 0x311, "dd79ecbbf6270f98")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.SmallestSize, 0x311, "e72c2161819ae447")]
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

        public void Data_Stream64KiB(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            int streamLen = 64 * 1024; // Total bytes to process
            int bufferSize = 64 * 1024;

            using (var data = new TestDataStream())
            {
                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                //Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                ////Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                ////Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                //Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0xf, "0de3431ec7da9349")]
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
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0xf, "0de3431ec7da9349")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x6, "d8018fa1b17508d8")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x14, "da5b8f3c1492541a")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Optimal, 0x14, "ebc2b57c01d2615b")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.SmallestSize, 0x14, "e206a5f5f4865fbb")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x14, "da5b8f3c1492541a")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Optimal, 0x14, "ebc2b57c01d2615b")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.SmallestSize, 0x14, "e206a5f5f4865fbb")]
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
                //Assert.Equal(rawXxH64, r.InHash); //test raw data hash matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x600014, "6aaba9c27838a268", "d3d5fdf377e15940")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x60077b, "6aaba9c27838a268", "6cd757c2d4c89c1c")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x654013, "6aaba9c27838a268", "85d2edff8cc11b12")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x600147, "6aaba9c27838a268", "d879fa782d69a9d1")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x6160ee, "6aaba9c27838a268", "a434182c245fd148")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x60018d, "6aaba9c27838a268", "9f24550a997d41c8")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x600781, "6aaba9c27838a268", "4d650db1242fefec")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x654019, "6aaba9c27838a268", "503de593cc2df76a")]
        public void DataNonCompressible_Stream6MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string xxh64, string compXxH64)
        {
            int streamLen = 6 * 1024 * 1024; // Total bytes to process
            int bufferSize = 1 * 1024 * 1024; // 1MiB block size

            using (var data = new TestNonCompressibleDataStream())
            {
                var x = TestNonCompressibleDataStream.Create(streamLen);

                TestResults r = Utl.TestStreamBlocks(data, algorithm, type, streamLen, bufferSize, (int)compressedSize);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{r.CompressedBytes:x}, \"{r.InHash}\", \"{r.CompressedHash}\")]");
                Assert.Equal(compressedSize, r.CompressedBytes); //test compressed data size matches expected
                Assert.Equal(xxh64, r.InHash); //test raw data hash matches expected
                Assert.Equal(compXxH64, r.CompressedHash); //test compressed data hash matches expected
                Assert.Equal(r.InHash, r.OutHash); //test IN and decompressed data hashes match
            }
        }

#if !IS_32BIT
        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x2030, "7833322f45651d24", "4095103af13e29d7")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0xb499, "7833322f45651d24", "3aa7fa616a0a0beb")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x1371d, "7833322f45651d24", "4e21fd2ecc76dda6")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x54e, "7833322f45651d24", "635862db00fae132")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0xea7, "7833322f45651d24", "08cf4de3f6733691")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x9e3, "7833322f45651d24", "4232839926c89dcf")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0xb4ab, "7833322f45651d24", "1a0f0e211e41a761")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x1372f, "7833322f45651d24", "3a041a9d0d3ed1cc")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0xb49f, "7833322f45651d24", "6971719677322556")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x13723, "7833322f45651d24", "34a0eaac76a7fd87")]
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
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x16fa3, "6d522dca7d96dfe8", "f0e78620b0b3ca96")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0x15310, "6d522dca7d96dfe8", "023838e061289d25")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0x15310, "6d522dca7d96dfe8", "023838e061289d25")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0xd65, "6d522dca7d96dfe8", "de79b5fd4314f0e5")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0xd66, "6d522dca7d96dfe8", "a975d12987fde7eb")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0xd66, "6d522dca7d96dfe8", "a975d12987fde7eb")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x30d5, "6d522dca7d96dfe8", "cb07ceedc0627fcc")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x30d5, "6d522dca7d96dfe8", "be03df0742f1daeb")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x30d5, "6d522dca7d96dfe8", "be03df0742f1daeb")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x208b, "6d522dca7d96dfe8", "eceddb5fb719e23f")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x132a, "6d522dca7d96dfe8", "b14b454b8273d87c")]
        // too slow [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x132a, "6d522dca7d96dfe8", "de68e77100a71261")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x25594, "6d522dca7d96dfe8", "d7d3020f5e9928ce")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Optimal, 0x16761, "6d522dca7d96dfe8", "8c8bbce501b98acb")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.SmallestSize, 0x16761, "6d522dca7d96dfe8", "4aaadffba8313a94")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x4098d, "6d522dca7d96dfe8", "572527c6643e493c")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Optimal, 0x1676a, "6d522dca7d96dfe8", "57b4bf88afef1001")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.SmallestSize, 0x16761, "6d522dca7d96dfe8", "4aaadffba8313a94")]
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
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x24c20f, "c668fabe6e6e9235", "709d1a023261544d")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x129aa, "c668fabe6e6e9235", "150b42d11de57fc7")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x4e201, "c668fabe6e6e9235", "4ae0151988b74cae")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x3b91a5, "c668fabe6e6e9235", "71c2677e0d742cee")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x673339, "c668fabe6e6e9235", "82cdb7c74478eca4")]
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

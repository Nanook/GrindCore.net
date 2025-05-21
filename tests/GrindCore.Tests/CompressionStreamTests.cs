using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using System.Diagnostics;
using System.Security.Cryptography;
using Nanook.GrindCore.ZLib;
using Xunit;
using Nanook.GrindCore.Lzma;
using System;

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
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x5e5, "cdd2efd3865069b2")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x733, "347363ee8e7e7f1d")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x733, "0f256c67b74d6676")]
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
        public void Text_ByteArray64KiB(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Process(algorithm, _text64KiB, new CompressionOptions() { Type = type, Version = CompressionVersion.Create(algorithm, "") }, out byte[]? props);
            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            var decompressed = CompressionStreamFactory.Process(algorithm, compressed, new CompressionOptions() { Type = CompressionType.Decompress, Version = CompressionVersion.Create(algorithm, ""), InitProperties = props });
            Assert.Equal(_text64KiB, decompressed);
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1f6, "1fd0ab5c0058d51a")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x3cd, "eefbb1f99743a612")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x4bf, "ca5207caef02504d")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x1de, "6d7905044230ec1f")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x3d3, "faf781046fb77de8")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x4c5, "1c5c2490ab900308")]
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
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x1de, "6d7905044230ec1f")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x1de, "069b2a2799eadee8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x1de, "069b2a2799eadee8")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1e5, "b4550df1c1bd0067")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x1e5, "3e6f77f9c11f4e70")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x1e5, "3e6f77f9c11f4e70")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x1ea, "e8dcd55f29d31d27")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x1ea, "4ffd75974e4d0d93")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x1ea, "bfb715f62e1a0e6b")]
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
#endif

        public void Data_ByteArray64KiB(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Process(algorithm, _data64KiB, new CompressionOptions() { Type = type, Version = CompressionVersion.Create(algorithm, "") }, out byte[]? props);
            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            var decompressed = CompressionStreamFactory.Process(algorithm, compressed, new CompressionOptions() { Type = CompressionType.Decompress, Version = CompressionVersion.Create(algorithm, ""), InitProperties = props });
            Assert.Equal(_data64KiB, decompressed);
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x0, "ef46db3751d8e999")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x0, "ef46db3751d8e999")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x0, "ef46db3751d8e999")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x0, "ef46db3751d8e999")]
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
#endif
        public void Text_ByteArrayEmpty(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Process(algorithm, _dataEmpty, new CompressionOptions() { Type = type, Version = CompressionVersion.Create(algorithm, "") }, out byte[]? props);
            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            //Assert.Equal(compressedSize, compressed.Length);
            //Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            //var decompressed = CompressionStreamFactory.Process(algorithm, compressed, new CompressionOptions() { Type = CompressionType.Decompress, Version = CompressionVersion.Create(algorithm, ""), InitProperties = props });
            //Assert.Equal(_dataEmpty, decompressed);
        }

        [Fact]
        public void Data_ByteArray64KiB_Zlib_Compress()
        {
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress(compressed, 0, compressed.Length, _data64KiB, 0, _data64KiB.Length);
            string result = XXHash64.Compute(compressed, 0, sz).ToHexString();
            string inHash = XXHash64.Compute(_data64KiB).ToHexString();
            Assert.Equal(0x305, sz);
            Assert.Equal("a3c36ab37f8f236d", result);

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress(decompressed, 0, decompressed.Length, compressed, 0, sz);

            Assert.Equal(_data64KiB.Length, sz);
            Assert.Equal(XXHash64.Compute(_data64KiB).ToHexString(), XXHash64.Compute(decompressed, 0, sz).ToHexString());
        }

        [Theory]
        [InlineData(0, 0x10010, "c42aa68d0ed670fd")]
        [InlineData(1, 0x3d3, "faf781046fb77de8")]
        [InlineData(2, 0x3d3, "3e74a6f73cae570e")]
        [InlineData(3, 0x3d3, "3e74a6f73cae570e")]
        [InlineData(4, 0x305, "f4304db22b1f3f33")]
        [InlineData(5, 0x305, "f4304db22b1f3f33")]
        [InlineData(6, 0x305, "a3c36ab37f8f236d")]
        [InlineData(7, 0x305, "a21b9fa33c110bc5")]
        [InlineData(8, 0x305, "a21b9fa33c110bc5")]
        [InlineData(9, 0x305, "a21b9fa33c110bc5")]

        public void Data_ByteArray64KiB_Zlib_Compress2(int level, int size, string expected)
        {
            CompressionVersion version = CompressionVersion.Create(CompressionAlgorithm.ZLib, CompressionVersion.ZLIB_v1_3_1); //latest regular zlib
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress2(compressed, 0, compressed.Length, _data64KiB, 0, _data64KiB.Length, level, version);
            string result = XXHash64.Compute(compressed, 0, sz).ToHexString();
            Trace.WriteLine($"[InlineData({level}, 0x{sz:x}, \"{result}\")]");

            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress2(decompressed, 0, decompressed.Length, compressed, 0, sz, version);

            Assert.Equal(_data64KiB.Length, sz);
            Assert.Equal(XXHash64.Compute(_data64KiB).ToHexString(), XXHash64.Compute(decompressed, 0, sz).ToHexString());

        }

        [Theory]
        [InlineData(0, 0x10010, "c42aa68d0ed670fd")]
        [InlineData(1, 0x4c4, "fc40654142efec69")]
        [InlineData(2, 0x3d3, "3e74a6f73cae570e")]
        [InlineData(3, 0x305, "f4304db22b1f3f33")]
        [InlineData(4, 0x305, "f4304db22b1f3f33")]
        [InlineData(5, 0x305, "f4304db22b1f3f33")]
        [InlineData(6, 0x305, "a3c36ab37f8f236d")]
        [InlineData(7, 0x305, "a21b9fa33c110bc5")]
        [InlineData(8, 0x305, "a21b9fa33c110bc5")]
        [InlineData(9, 0x305, "a21b9fa33c110bc5")]

        public void Data_ByteArray64KiB_ZlibNg_Compress2(int level, int size, string expected)
        {
            CompressionVersion version = CompressionVersion.Create(CompressionAlgorithm.ZLibNg, ""); //latestng
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress2(compressed, 0, compressed.Length, _data64KiB, 0, _data64KiB.Length, level, version);
            string result = XXHash64.Compute(compressed, 0, sz).ToHexString();
            Trace.WriteLine($"[InlineData({level}, 0x{sz:x}, \"{result}\")]");

            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress2(decompressed, 0, decompressed.Length, compressed, 0, sz, version);

            Assert.Equal(_data64KiB.Length, sz);
            Assert.Equal(XXHash64.Compute(_data64KiB).ToHexString(), XXHash64.Compute(decompressed, 0, sz).ToHexString());

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="level">zlib level 0 to 9</param>
        /// <param name="strategy">0=Normal, 1=Filtered, 2=Huffman, 3=RLE</param>
        /// <param name="header"></param>
        /// <param name="size">Expect compressed size</param>
        /// <param name="expected">Expected xxhash64</param>
        [Theory]
        [InlineData(0, 0, false, 0x1000a, "23dad93a68ae7c89")] //Normal
        [InlineData(0, 0, true, 0x10010, "c42aa68d0ed670fd")] //Normal
        [InlineData(0, 1, false, 0x1000a, "23dad93a68ae7c89")] //Filtered
        [InlineData(0, 1, true, 0x10010, "c42aa68d0ed670fd")] //Filtered
        [InlineData(0, 2, false, 0x1000a, "23dad93a68ae7c89")] //Huffman
        [InlineData(0, 2, true, 0x10010, "c42aa68d0ed670fd")] //Huffman
        [InlineData(0, 3, false, 0x1000a, "23dad93a68ae7c89")] //RLE
        [InlineData(0, 3, true, 0x10010, "c42aa68d0ed670fd")] //RLE
        [InlineData(1, 0, false, 0x3cd, "eefbb1f99743a612")] //Normal
        [InlineData(1, 0, true, 0x3d3, "faf781046fb77de8")] //Normal
        [InlineData(1, 1, false, 0x3cd, "eefbb1f99743a612")] //Filtered
        [InlineData(1, 1, true, 0x3d3, "faf781046fb77de8")] //Filtered
        [InlineData(1, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(1, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(1, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(1, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(2, 0, false, 0x3cd, "eefbb1f99743a612")] //Normal
        [InlineData(2, 0, true, 0x3d3, "3e74a6f73cae570e")] //Normal
        [InlineData(2, 1, false, 0x3cd, "eefbb1f99743a612")] //Filtered
        [InlineData(2, 1, true, 0x3d3, "3e74a6f73cae570e")] //Filtered
        [InlineData(2, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(2, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(2, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(2, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(3, 0, false, 0x3cd, "eefbb1f99743a612")] //Normal
        [InlineData(3, 0, true, 0x3d3, "3e74a6f73cae570e")] //Normal
        [InlineData(3, 1, false, 0x3cd, "eefbb1f99743a612")] //Filtered
        [InlineData(3, 1, true, 0x3d3, "3e74a6f73cae570e")] //Filtered
        [InlineData(3, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(3, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(3, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(3, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(4, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(4, 0, true, 0x305, "f4304db22b1f3f33")] //Normal
        [InlineData(4, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(4, 1, true, 0x305, "f4304db22b1f3f33")] //Filtered
        [InlineData(4, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(4, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(4, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(4, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(5, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(5, 0, true, 0x305, "f4304db22b1f3f33")] //Normal
        [InlineData(5, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(5, 1, true, 0x305, "f4304db22b1f3f33")] //Filtered
        [InlineData(5, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(5, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(5, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(5, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(6, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(6, 0, true, 0x305, "a3c36ab37f8f236d")] //Normal
        [InlineData(6, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(6, 1, true, 0x305, "a3c36ab37f8f236d")] //Filtered
        [InlineData(6, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(6, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(6, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(6, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(7, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(7, 0, true, 0x305, "a21b9fa33c110bc5")] //Normal
        [InlineData(7, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(7, 1, true, 0x305, "a21b9fa33c110bc5")] //Filtered
        [InlineData(7, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(7, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(7, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(7, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(8, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(8, 0, true, 0x305, "a21b9fa33c110bc5")] //Normal
        [InlineData(8, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(8, 1, true, 0x305, "a21b9fa33c110bc5")] //Filtered
        [InlineData(8, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(8, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(8, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(8, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(9, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(9, 0, true, 0x305, "a21b9fa33c110bc5")] //Normal
        [InlineData(9, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(9, 1, true, 0x305, "a21b9fa33c110bc5")] //Filtered
        [InlineData(9, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(9, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(9, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(9, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE


        public void Data_ByteArray64KiB_Zlib_Compress3(int level, int strategy, bool header, int size, string expected)
        {
            CompressionVersion version = CompressionVersion.ZLib(ZLibVersion.Latest); //latest regular zlib
            int off = 0;
            if (!header)
                off = 2;
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress3(compressed, off, compressed.Length - off, _data64KiB, 0, _data64KiB.Length, level, strategy, header, version);
            string result = XXHash64.Compute(compressed, off, sz).ToHexString();
            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            Trace.WriteLine($"[InlineData({level}, {strategy}, {header.ToString().ToLower()}, 0x{sz:x}, \"{result}\")] //{(new string[] { "Normal", "Filtered", "Huffman", "RLE" }[strategy])}");

            if (!header) //insert the header
            {
                compressed[0] = 0x78;
                compressed[1] = 0x9C;
                sz += 2;
            }

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress3(decompressed, 0, decompressed.Length, compressed, 0, sz, version);

            Assert.Equal(_data64KiB.Length, sz);
            Assert.Equal(XXHash64.Compute(_data64KiB).ToHexString(), XXHash64.Compute(decompressed, 0, sz).ToHexString());

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="level">zlib level 0 to 9</param>
        /// <param name="strategy">0=Normal, 1=Filtered, 2=Huffman, 3=RLE</param>
        /// <param name="header"></param>
        /// <param name="size">Expect compressed size</param>
        /// <param name="expected">Expected xxhash64</param>
        [Theory]
        [InlineData(0, 0, false, 0x1000a, "23dad93a68ae7c89")] //Normal
        [InlineData(0, 0, true, 0x10010, "c42aa68d0ed670fd")] //Normal
        [InlineData(0, 1, false, 0x1000a, "23dad93a68ae7c89")] //Filtered
        [InlineData(0, 1, true, 0x10010, "c42aa68d0ed670fd")] //Filtered
        [InlineData(0, 2, false, 0x1000a, "23dad93a68ae7c89")] //Huffman
        [InlineData(0, 2, true, 0x10010, "c42aa68d0ed670fd")] //Huffman
        [InlineData(0, 3, false, 0x1000a, "23dad93a68ae7c89")] //RLE
        [InlineData(0, 3, true, 0x10010, "c42aa68d0ed670fd")] //RLE
        [InlineData(1, 0, false, 0x4be, "6564467a7a492f66")] //Normal
        [InlineData(1, 0, true, 0x4c4, "fc40654142efec69")] //Normal
        [InlineData(1, 1, false, 0x4be, "6564467a7a492f66")] //Filtered
        [InlineData(1, 1, true, 0x4c4, "fc40654142efec69")] //Filtered
        [InlineData(1, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(1, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(1, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(1, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(2, 0, false, 0x3cd, "eefbb1f99743a612")] //Normal
        [InlineData(2, 0, true, 0x3d3, "3e74a6f73cae570e")] //Normal
        [InlineData(2, 1, false, 0x3cd, "eefbb1f99743a612")] //Filtered
        [InlineData(2, 1, true, 0x3d3, "3e74a6f73cae570e")] //Filtered
        [InlineData(2, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(2, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(2, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(2, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(3, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(3, 0, true, 0x305, "f4304db22b1f3f33")] //Normal
        [InlineData(3, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(3, 1, true, 0x305, "f4304db22b1f3f33")] //Filtered
        [InlineData(3, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(3, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(3, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(3, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(4, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(4, 0, true, 0x305, "f4304db22b1f3f33")] //Normal
        [InlineData(4, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(4, 1, true, 0x305, "f4304db22b1f3f33")] //Filtered
        [InlineData(4, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(4, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(4, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(4, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(5, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(5, 0, true, 0x305, "f4304db22b1f3f33")] //Normal
        [InlineData(5, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(5, 1, true, 0x305, "f4304db22b1f3f33")] //Filtered
        [InlineData(5, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(5, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(5, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(5, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(6, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(6, 0, true, 0x305, "a3c36ab37f8f236d")] //Normal
        [InlineData(6, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(6, 1, true, 0x305, "a3c36ab37f8f236d")] //Filtered
        [InlineData(6, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(6, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(6, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(6, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(7, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(7, 0, true, 0x305, "a21b9fa33c110bc5")] //Normal
        [InlineData(7, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(7, 1, true, 0x305, "a21b9fa33c110bc5")] //Filtered
        [InlineData(7, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(7, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(7, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(7, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(8, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(8, 0, true, 0x305, "a21b9fa33c110bc5")] //Normal
        [InlineData(8, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(8, 1, true, 0x305, "a21b9fa33c110bc5")] //Filtered
        [InlineData(8, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(8, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(8, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(8, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE
        [InlineData(9, 0, false, 0x2ff, "fd1a57a63d29c607")] //Normal
        [InlineData(9, 0, true, 0x305, "a21b9fa33c110bc5")] //Normal
        [InlineData(9, 1, false, 0x2ff, "fd1a57a63d29c607")] //Filtered
        [InlineData(9, 1, true, 0x305, "a21b9fa33c110bc5")] //Filtered
        [InlineData(9, 2, false, 0xe609, "3e0ffb89647f3de9")] //Huffman
        [InlineData(9, 2, true, 0xe60f, "57d45a574d4876d4")] //Huffman
        [InlineData(9, 3, false, 0xe609, "3e0ffb89647f3de9")] //RLE
        [InlineData(9, 3, true, 0xe60f, "57d45a574d4876d4")] //RLE


        public void Data_ByteArray64KiB_ZlibNg_Compress3(int level, int strategy, bool header, int size, string expected)
        {
            CompressionVersion version = CompressionVersion.ZLibNgLatest(); //latest zlib-ng

            int off = 0;
            if (!header)
                off = 2;
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress3(compressed, off, compressed.Length - off, _data64KiB, 0, _data64KiB.Length, level, strategy, header, version);
            string result = XXHash64.Compute(compressed, off, sz).ToHexString();

            Trace.WriteLine($"[InlineData({level}, {strategy}, {header.ToString().ToLower()}, 0x{sz:x}, \"{result}\")] //{(new string[] { "Normal", "Filtered", "Huffman", "RLE" }[strategy])}");
            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            if (!header) //insert the header
            {
                compressed[0] = 0x78;
                compressed[1] = 0x9C;
                sz += 2;
            }

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress3(decompressed, 0, decompressed.Length, compressed, 0, sz, version);

            Assert.Equal(_data64KiB.Length, sz);
            Assert.Equal(XXHash64.Compute(_data64KiB).ToHexString(), XXHash64.Compute(decompressed, 0, sz).ToHexString());

        }

#if !IS_32BIT
        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x2030, "7833322f45651d24", "4095103af13e29d7")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0xb499, "7833322f45651d24", "3aa7fa616a0a0beb")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x1371d, "7833322f45651d24", "4e21fd2ecc76dda6")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x54e, "7833322f45651d24", "635862db00fae132")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0xea7, "7833322f45651d24", "08cf4de3f6733691")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0xf55, "7833322f45651d24", "e5cd71b270e57d8d")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0xb4ab, "7833322f45651d24", "1a0f0e211e41a761")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x1372f, "7833322f45651d24", "3a041a9d0d3ed1cc")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0xb49f, "7833322f45651d24", "6971719677322556")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x13723, "7833322f45651d24", "34a0eaac76a7fd87")]
        public void Data_Stream6MiB_ReadByteWriteByte(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            // Process in 1MiB blocks
            int total = 6 * 1024 * 1024; // Total bytes to process
            int blockSize = 1 * 1024 * 1024; // 1MiB block size
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;
            byte[] properties;
            int? threadCount = null;

            if (algorithm == CompressionAlgorithm.FastLzma2)
                threadCount = 4;

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Process
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, new CompressionOptions() { Type = type, LeaveOpen = true, BufferOverflowSize = blockSize, BufferSize = blockSize, BlockSize = blockSize, ThreadCount = threadCount, Version = CompressionVersion.Create(algorithm, "") }))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                while (totalInProcessedBytes < total)
                                {
                                    int b = inDataStream.ReadByte();
                                    if (b == -1) break;
                                    cryptoStream.WriteByte((byte)b);
                                    totalInProcessedBytes++;
                                    //Trace.WriteLine($"{totalInProcessedBytes} of {total} ({compMemoryStream.Position})");
                                }
                            }
                            //compMemoryStream.Flush();
                            properties = compressionStream.Properties;
                        }

                        // Hash Compressed data
                        totalCompressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                        {
                            int b;
                            while ((b = compMemoryStream.ReadByte()) != -1)
                                cryptoStream.WriteByte((byte)b);
                        }

                        // Decompress and hash
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, new CompressionOptions() { Type = CompressionType.Decompress, LeaveOpen = true, InitProperties = properties, BufferSize = blockSize, Version = CompressionVersion.Create(algorithm, "") }))
                        {
                            int b;
                            while (totalOutProcessedBytes < total && (b = compressionStream.ReadByte()) != -1)
                            {
                                outXxhash.TransformBlock(new[] { (byte)b }, 0, 1, null, 0);
                                totalOutProcessedBytes++;
                            }
                            outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        }
                    }

                    string hashInString = inXxhash.Hash!.ToHexString();
                    string hashCompString = compXxhash.Hash!.ToHexString();
                    string hashOutString = outXxhash.Hash!.ToHexString();
                    Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected
                    Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                    Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
                    Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
                }
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x6b42, "6d522dca7d96dfe8", "0a1ce1dc6372e770")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1b5, "6d522dca7d96dfe8", "d20488bddff3a34b")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1a5, "6d522dca7d96dfe8", "9dfc18b1eae394b7")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x25582, "6d522dca7d96dfe8", "f6f7ebd36ab15670")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x4097b, "6d522dca7d96dfe8", "0e9009f7ff7f372d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x16758, "6d522dca7d96dfe8", "9567de3b29629820")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0xd65, "6d522dca7d96dfe8", "de79b5fd4314f0e5")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0xd66, "6d522dca7d96dfe8", "a975d12987fde7eb")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0xd66, "6d522dca7d96dfe8", "a975d12987fde7eb")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x30d5, "6d522dca7d96dfe8", "cb07ceedc0627fcc")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x30d5, "6d522dca7d96dfe8", "be03df0742f1daeb")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x30d5, "6d522dca7d96dfe8", "be03df0742f1daeb")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x32ad, "6d522dca7d96dfe8", "236f82069d776982")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x2585, "6d522dca7d96dfe8", "f514e839a94f3114")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x2585, "6d522dca7d96dfe8", "2bb46a1cb021ffdb")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x25594, "6d522dca7d96dfe8", "d7d3020f5e9928ce")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Optimal, 0x16761, "6d522dca7d96dfe8", "8c8bbce501b98acb")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.SmallestSize, 0x16761, "6d522dca7d96dfe8", "4aaadffba8313a94")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x4098d, "6d522dca7d96dfe8", "572527c6643e493c")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Optimal, 0x1676a, "6d522dca7d96dfe8", "57b4bf88afef1001")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.SmallestSize, 0x16761, "6d522dca7d96dfe8", "4aaadffba8313a94")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x25588, "6d522dca7d96dfe8", "52939e62ee507885")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x16755, "6d522dca7d96dfe8", "a7fe8e5eb6ce2b02")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x16755, "6d522dca7d96dfe8", "0cf63fbf2648d734")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x40981, "6d522dca7d96dfe8", "63423a67659ba6ca")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x1675e, "6d522dca7d96dfe8", "976b095b1f42c3e1")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x16755, "6d522dca7d96dfe8", "0cf63fbf2648d734")]

        public async Task Data_Stream20MiB_Chunk1MiB_Async(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            // Process in 1MiB blocks
            int total = 20 * 1024 * 1024; // Total bytes to process
            int blockSize = 1 * 1024 * 1024; // 1MiB block size
            byte[] buffer = new byte[blockSize];
            byte[] properties;
            int? threadCount = null;
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            if (algorithm == CompressionAlgorithm.FastLzma2)
                threadCount = 4;

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Process
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, new CompressionOptions() { Type = type, LeaveOpen = true, BufferSize = blockSize, BlockSize = blockSize, ThreadCount = threadCount, Version = CompressionVersion.Create(algorithm, "") }))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = await inDataStream.ReadAsync(buffer, 0, Math.Min(blockSize, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    await cryptoStream.WriteAsync(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                }
                                await compressionStream.FlushAsync();
                                properties = compressionStream.Properties;
                                Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                                Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                            }
                        }

                        // Hash Compressed data
                        totalCompressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            await compMemoryStream.CopyToAsync(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, new CompressionOptions() { Type = CompressionType.Decompress, LeaveOpen = true, InitProperties = properties, BufferSize = blockSize, Version = CompressionVersion.Create(algorithm, "") }))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = await compressionStream.ReadAsync(buffer, 0, Math.Min(blockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
                            }
                            outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                            Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                            Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                        }
                    }

                    string hashInString = inXxhash.Hash!.ToHexString();
                    string hashCompString = compXxhash.Hash!.ToHexString();
                    string hashOutString = outXxhash.Hash!.ToHexString();
                    Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected
                    Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
                    Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                    Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected

                }
            }

        }

#if WIN_X64
        [Theory]
        [InlineData(CompressionAlgorithm.Copy, CompressionType.Fastest, 512 * 1024 * 1024, "c668fabe6e6e9235", "c668fabe6e6e9235")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0xaba27, "c668fabe6e6e9235", "f11172f7d39c66ff")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x3b918d, "c668fabe6e6e9235", "78d7773f85aa8e7c")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x673322, "c668fabe6e6e9235", "2be7825c1fffe4d2")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x4f14d, "c668fabe6e6e9235", "b0d59105b8b98e82")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x129aa, "c668fabe6e6e9235", "150b42d11de57fc7")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x4e201, "c668fabe6e6e9235", "4ae0151988b74cae")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x3b91a5, "c668fabe6e6e9235", "71c2677e0d742cee")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x673339, "c668fabe6e6e9235", "82cdb7c74478eca4")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x3b9193, "c668fabe6e6e9235", "a951bcf26245af42")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x673328, "c668fabe6e6e9235", "acd9eae8637f56d9")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x24c20f, "c668fabe6e6e9235", "709d1a023261544d")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0xc188, "c668fabe6e6e9235", "dcfbfce99695a474")]
        public void Data_Stream512MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            // Process in 1MiB blocks
            int total = 512 * 1024 * 1024; // Total bytes to process
            int blockSize = 1 * 1024 * 1024; // 1MiB block size
            byte[] buffer = BufferPool.Rent(blockSize);
            byte[] properties;
            int? threadCount = null;
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            if (algorithm == CompressionAlgorithm.FastLzma2)
                threadCount = 4;

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Process
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, new CompressionOptions() { Type = type, LeaveOpen = true, BufferSize = blockSize, BlockSize = blockSize, ThreadCount = threadCount, Version = CompressionVersion.Create(algorithm, "") }))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = inDataStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    cryptoStream.Write(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                    //Trace.WriteLine($"{totalInProcessedBytes} of {total} ({compMemoryStream.Position})");
                                }
                                compressionStream.Complete(); //complete without fully disposing to set accurate Positions
                                properties = compressionStream.Properties;
                                Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                                Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                                compMemoryStream.Flush();
                            }
                        }

                        // Hash Compressed data
                        totalCompressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            compMemoryStream.CopyTo(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, new CompressionOptions() { Type = CompressionType.Decompress, LeaveOpen = true, InitProperties = properties, BufferSize = blockSize, Version = CompressionVersion.Create(algorithm, "") }))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
                            }
                            outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                            Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                            Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                        }
                    }

                    string hashInString = inXxhash.Hash!.ToHexString();
                    string hashCompString = compXxhash.Hash!.ToHexString();
                    string hashOutString = outXxhash.Hash!.ToHexString();
                    Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected
                    Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                    Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
                    Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
                }

            }
            BufferPool.Return(buffer);
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x129aa, "c668fabe6e6e9235", "150b42d11de57fc7")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x138c1, "c668fabe6e6e9235", "cc526df9a9d44632")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x49d1d, "c668fabe6e6e9235", "6f7def11d3567702")]
        public void Data_Stream512MiB_Chunk128MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            // Process in 1MiB blocks
            int total = 512 * 1024 * 1024; // Total bytes to process
            int blockSize = 128 * 1024 * 1024; // 64MiB block size
            byte[] buffer = new byte[blockSize];
            byte[] properties;
            int? threadCount = null;
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            if (algorithm == CompressionAlgorithm.FastLzma2)
                threadCount = 4;

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Process
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, new CompressionOptions() { Type = type, LeaveOpen = true, BufferSize = blockSize, BlockSize = blockSize, ThreadCount = threadCount, Version = CompressionVersion.Create(algorithm, "") }))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = inDataStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    cryptoStream.Write(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                    //Trace.WriteLine($"{totalInProcessedBytes} of {total} ({compMemoryStream.Position})");
                                }
                                properties = compressionStream.Properties;
                                compressionStream.Complete();
                                Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                                Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                            }
                        }

                        // Hash Compressed data
                        totalCompressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            compMemoryStream.CopyTo(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, new CompressionOptions() { Type = CompressionType.Decompress, LeaveOpen = true, InitProperties = properties, BufferSize = blockSize, Version = CompressionVersion.Create(algorithm, "") }))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
                            }
                            outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                            Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                            Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                        }
                    }

                    string hashInString = inXxhash.Hash!.ToHexString();
                    string hashCompString = compXxhash.Hash!.ToHexString();
                    string hashOutString = outXxhash.Hash!.ToHexString();
                    Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected
                    Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
                    Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
                    Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                }
            }

        }
#endif
#endif

    }
}

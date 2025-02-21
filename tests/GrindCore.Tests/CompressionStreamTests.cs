using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using System.Diagnostics;
using System.Security.Cryptography;
using Nanook.GrindCore.ZLib;
using Xunit;

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
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x5e5, "cdd2efd3865069b2")]
        public void Text_ByteArray64KiBArm(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Compress(algorithm, _text64KiB, type);
            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            //Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            //var decompressed = CompressionStreamFactory.Decompress(algorithm, compressed);
            //Assert.Equal(_text64KiB, decompressed);
        }


#if !IS_32BIT
        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x84d, "25be05c704cb5995")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x5d1, "2b444156a4305ae3")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x4fa, "bd7a15fc895f1b65")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x1db4, "2464d64063e022ba")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0xcc2, "88b181dc28558433")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0xb9b, "080ef351410b77ac")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x264c, "728b6f680101e18d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0xbe1, "8fbdcf11b9e9fcb4")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0xb9b, "080ef351410b77ac")]
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
        public void Text_ByteArray64KiB(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Compress(algorithm, _text64KiB, type);
            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            var decompressed = CompressionStreamFactory.Decompress(algorithm, compressed);
            Assert.Equal(_text64KiB, decompressed);
        }

        [Theory]
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

        public void Data_ByteArray64KiB(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Compress(algorithm, _data64KiB, type);
            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            var decompressed = CompressionStreamFactory.Decompress(algorithm, compressed);
            Assert.Equal(_data64KiB, decompressed);
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1, "12f83c352a398165")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x2, "fae4a10ff02fd326")]
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
        public void Text_ByteArrayEmpty(CompressionAlgorithm algorithm, CompressionType type, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Compress(algorithm, _dataEmpty, type);
            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            var decompressed = CompressionStreamFactory.Decompress(algorithm, compressed);
            Assert.Equal(_dataEmpty, decompressed);
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

        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x6b44, "6d522dca7d96dfe8", "879665c04f8d526d")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1b6, "6d522dca7d96dfe8", "cf0f006564a490d7")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1a7, "6d522dca7d96dfe8", "35c4c25d8a95aa8c")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x25582, "6d522dca7d96dfe8", "f6f7ebd36ab15670")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x4097b, "6d522dca7d96dfe8", "0e9009f7ff7f372d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x16758, "6d522dca7d96dfe8", "9567de3b29629820")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
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

        public void Data_Stream20MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            // Process in 1MiB blocks
            int total = 20 * 1024 * 1024; // Total bytes to process
            int blockSize = 1 * 1024 * 1024; // 1MiB block size
            byte[] buffer = new byte[blockSize * 2];
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Compress
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, type, true))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = inDataStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    cryptoStream.Write(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                }
                            }
                            compMemoryStream.Flush();
                        }

                        // Hash Compressed data
                        totalCompressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            compMemoryStream.CopyTo(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, CompressionType.Decompress, true))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
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
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x6b44, "6d522dca7d96dfe8", "879665c04f8d526d")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1b6, "6d522dca7d96dfe8", "cf0f006564a490d7")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1a7, "6d522dca7d96dfe8", "35c4c25d8a95aa8c")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x25582, "6d522dca7d96dfe8", "f6f7ebd36ab15670")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x4097b, "6d522dca7d96dfe8", "0e9009f7ff7f372d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x16758, "6d522dca7d96dfe8", "9567de3b29629820")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x32b8, "6d522dca7d96dfe8", "b75df1a5252ce82f")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x2590, "6d522dca7d96dfe8", "1d1b3f4f5bb27e39")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x2590, "6d522dca7d96dfe8", "3e1926735828783a")]
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
            byte[] buffer = new byte[blockSize * 2];
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Compress
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, type, true))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = await inDataStream.ReadAsync(buffer, 0, Math.Min(blockSize, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    await cryptoStream.WriteAsync(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                }
                            }
                            compMemoryStream.Flush();
                        }

                        // Hash Compressed data
                        totalCompressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            await compMemoryStream.CopyToAsync(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, CompressionType.Decompress, true))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = await compressionStream.ReadAsync(buffer, 0, Math.Min(blockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
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

#if WIN_X64
        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0xaba29, "c668fabe6e6e9235", "b81723649b82a53d")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x4b9, "c668fabe6e6e9235", "c61dc24b52781f66")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x4c7, "c668fabe6e6e9235", "2781a4b8151afdab")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x3b9193, "c668fabe6e6e9235", "d660c36b53d5724a")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x673327, "c668fabe6e6e9235", "f0980ebcdcde5d8b")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x23c252, "c668fabe6e6e9235", "a332285f97856aa5")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x4f158, "c668fabe6e6e9235", "99bc4c760e5330db")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x3a936, "c668fabe6e6e9235", "736565400eba0fc9")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x39cbe, "c668fabe6e6e9235", "bd3edf1368d12af3")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x3b91a5, "c668fabe6e6e9235", "71c2677e0d742cee")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Optimal, 0x23c137, "c668fabe6e6e9235", "d4055f148c70a44c")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.SmallestSize, 0x23c137, "c668fabe6e6e9235", "8139b6563f0a1e18")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Fastest, 0x673339, "c668fabe6e6e9235", "82cdb7c74478eca4")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.Optimal, 0x23c264, "c668fabe6e6e9235", "6bbd6a89515111a4")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionType.SmallestSize, 0x23c137, "c668fabe6e6e9235", "8139b6563f0a1e18")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x3b9199, "c668fabe6e6e9235", "77e08be9bcdb4e41")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x23c12b, "c668fabe6e6e9235", "b5ae77c847a84a88")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x23c12b, "c668fabe6e6e9235", "89fb4ce3386045e9")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x67332d, "c668fabe6e6e9235", "d682cb103ee16df0")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x23c258, "c668fabe6e6e9235", "6bfec589b36154e5")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x23c12b, "c668fabe6e6e9235", "89fb4ce3386045e9")]
        public void Data_Stream512MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            // Process in 1MiB blocks
            int total = 512 * 1024 * 1024; // Total bytes to process
            int blockSize = 1 * 1024 * 1024; // 1MiB block size
            byte[] buffer = new byte[blockSize * 2];
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Compress
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, type, true))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = inDataStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    cryptoStream.Write(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                    Trace.WriteLine($"{totalInProcessedBytes} of {total} ({compMemoryStream.Position})");
                                }
                            }
                            compMemoryStream.Flush();
                        }

                        // Hash Compressed data
                        totalCompressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            compMemoryStream.CopyTo(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, CompressionType.Decompress, true))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
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
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x485d6, "c668fabe6e6e9235", "b63e735ffc5263fb")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x1682d, "c668fabe6e6e9235", "cc63ff0e04f71804")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x13cc8, "c668fabe6e6e9235", "7dc0c316356acd4b")]
        public void Data_Stream512MiB_Chunk128MiB(CompressionAlgorithm algorithm, CompressionType type, long compressedSize, string rawXxH64, string compXxH64)
        {
            // Process in 1MiB blocks
            int total = 512 * 1024 * 1024; // Total bytes to process
            int blockSize = 128 * 1024 * 1024; // 64MiB block size
            byte[] buffer = new byte[blockSize * 2];
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Compress
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, type, true))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = inDataStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    cryptoStream.Write(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                    Trace.WriteLine($"{totalInProcessedBytes} of {total} ({compMemoryStream.Position})");
                                }
                            }
                            compMemoryStream.Flush();
                        }

                        // Hash Compressed data
                        totalCompressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            compMemoryStream.CopyTo(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, CompressionType.Decompress, true))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
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
#endif
#endif
    }
}

using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using System.Diagnostics;
using System.Security.Cryptography;
using Nanook.GrindCore.ZLib;
using Xunit;
using System.Reflection.Emit;

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
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Fastest, 0x1f6, "1fd0ab5c0058d51a")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Optimal, 0x19b, "e39f3f4d64825537")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.SmallestSize, 0x18a, "03e8b1d250f7e6aa")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Fastest, 0x3cd, "eefbb1f99743a612")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Optimal, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.SmallestSize, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Fastest, 0x3cd, "eefbb1f99743a612")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Optimal, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.SmallestSize, 0x2ff, "fd1a57a63d29c607")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.Fastest, 0x3df, "23cc51e8d014ea83")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.Optimal, 0x311, "dd79ecbbf6270f98")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.SmallestSize, 0x311, "e72c2161819ae447")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.Fastest, 0x3df, "23cc51e8d014ea83")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.Optimal, 0x311, "dd79ecbbf6270f98")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.SmallestSize, 0x311, "e72c2161819ae447")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Fastest, 0x3d3, "faf781046fb77de8")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Optimal, 0x305, "a3c36ab37f8f236d")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.SmallestSize, 0x305, "a21b9fa33c110bc5")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Fastest, 0x3d3, "faf781046fb77de8")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Optimal, 0x305, "a3c36ab37f8f236d")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.SmallestSize, 0x305, "a21b9fa33c110bc5")]

        public void Data_ByteArray64KiB(CompressionStreamType type, CompressionLevel level, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Compress(type, _data64KiB, level);
            //Trace.WriteLine($"[InlineData(CompressionStreamType.{type}, CompressionLevel.{level}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            var decompressed = CompressionStreamFactory.Decompress(type, compressed);
            Assert.Equal(_data64KiB, decompressed);
        }

        [Theory]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Fastest, 0x84d, "25be05c704cb5995")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Optimal, 0x5c9, "4d23d17226f074fb")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.SmallestSize, 0x4fa, "bd7a15fc895f1b65")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Fastest, 0x1db4, "2464d64063e022ba")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Optimal, 0xcc2, "88b181dc28558433")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.SmallestSize, 0xb9b, "080ef351410b77ac")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.Fastest, 0x1dc6, "2baeb36442c0367d")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.Optimal, 0xcd4, "e1f838db8a9218b9")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.SmallestSize, 0xbad, "b2f20d23bf119caa")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Fastest, 0x1dba, "bdd8b43a296a44ad")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Optimal, 0xcc8, "aa99d2252c2f6606")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.SmallestSize, 0xba1, "2b9ecf7dce8e81ce")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Fastest, 0x1db4, "2464d64063e022ba")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Optimal, 0xcc2, "88b181dc28558433")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.SmallestSize, 0xb9b, "080ef351410b77ac")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.Fastest, 0x1dc6, "2baeb36442c0367d")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.Optimal, 0xcd4, "e1f838db8a9218b9")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.SmallestSize, 0xbad, "b2f20d23bf119caa")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Fastest, 0x1dba, "bdd8b43a296a44ad")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Optimal, 0xcc8, "aa99d2252c2f6606")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.SmallestSize, 0xba1, "2b9ecf7dce8e81ce")]
        public void Text_ByteArray64KiB(CompressionStreamType type, CompressionLevel level, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Compress(type, _text64KiB, level);
            //Trace.WriteLine($"[InlineData(CompressionStreamType.{type}, CompressionLevel.{level}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            var decompressed = CompressionStreamFactory.Decompress(type, compressed);
            Assert.Equal(_text64KiB, decompressed);
        }

        [Theory]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Fastest, 0x1, "12f83c352a398165")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Optimal, 0x1, "12f83c352a398165")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.SmallestSize, 0x1, "12f83c352a398165")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Optimal, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.SmallestSize, 0x2, "fae4a10ff02fd326")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.Fastest, 0x14, "da5b8f3c1492541a")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.Optimal, 0x14, "ebc2b57c01d2615b")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.SmallestSize, 0x14, "e206a5f5f4865fbb")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Optimal, 0x8, "65f2e912ed28c1ed")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.SmallestSize, 0x8, "31edcf2ea90ea820")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Fastest, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Optimal, 0x2, "fae4a10ff02fd326")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.SmallestSize, 0x2, "fae4a10ff02fd326")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.Fastest, 0x14, "da5b8f3c1492541a")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.Optimal, 0x14, "ebc2b57c01d2615b")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.SmallestSize, 0x14, "e206a5f5f4865fbb")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Optimal, 0x8, "65f2e912ed28c1ed")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.SmallestSize, 0x8, "31edcf2ea90ea820")]
        public void Text_ByteArrayEmpty(CompressionStreamType type, CompressionLevel level, int compressedSize, string xxh64)
        {
            var compressed = CompressionStreamFactory.Compress(type, _dataEmpty, level);
            //Trace.WriteLine($"[InlineData(CompressionStreamType.{type}, CompressionLevel.{level}, 0x{compressed.Length:x}, \"{XXHash64.Compute(compressed).ToHexString()}\")]");
            Assert.Equal(compressedSize, compressed.Length);
            Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
            var decompressed = CompressionStreamFactory.Decompress(type, compressed);
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
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress2(compressed, 0, compressed.Length, _data64KiB, 0, _data64KiB.Length, level);
            string result = XXHash64.Compute(compressed, 0, sz).ToHexString();
            //Trace.WriteLine($"[InlineData({level}, 0x{sz:x}, \"{result}\")]");

            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress2(decompressed, 0, decompressed.Length, compressed, 0, sz);

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

        public void Data_ByteArray64KiB_ZlibNg_Compress2(int level, int size, string expected)
        {
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLibNg.Compress2(compressed, 0, compressed.Length, _data64KiB, 0, _data64KiB.Length, level);
            string result = XXHash64.Compute(compressed, 0, sz).ToHexString();
            //Trace.WriteLine($"[InlineData({level}, 0x{sz:x}, \"{result}\")]");

            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLibNg.Uncompress2(decompressed, 0, decompressed.Length, compressed, 0, sz);

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
            int off = 0;
            if (!header)
                off = 2;
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress3(compressed, off, compressed.Length - off, _data64KiB, 0, _data64KiB.Length, level, strategy, header);
            string result = XXHash64.Compute(compressed, off, sz).ToHexString();
            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            //Trace.WriteLine($"[InlineData({level}, {strategy}, {header.ToString().ToLower()}, 0x{sz:x}, \"{result}\")] //{(new string[] { "Normal", "Filtered", "Huffman", "RLE" }[strategy])}");

            if (!header) //insert the header
            {
                compressed[0] = 0x78;
                compressed[1] = 0x9C;
                sz += 2;
            }

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress3(decompressed, 0, decompressed.Length, compressed, 0, sz);

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


        public void Data_ByteArray64KiB_ZlibNg_Compress3(int level, int strategy, bool header, int size, string expected)
        {
            int off = 0;
            if (!header)
                off = 2;
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLibNg.Compress3(compressed, off, compressed.Length - off, _data64KiB, 0, _data64KiB.Length, level, strategy, header);
            string result = XXHash64.Compute(compressed, off, sz).ToHexString();
            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            //Trace.WriteLine($"[InlineData({level}, {strategy}, {header.ToString().ToLower()}, 0x{sz:x}, \"{result}\")] //{(new string[] { "Normal", "Filtered", "Huffman", "RLE" }[strategy])}");

            if (!header) //insert the header
            {
                compressed[0] = 0x78;
                compressed[1] = 0x9C;
                sz += 2;
            }

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLibNg.Uncompress3(decompressed, 0, decompressed.Length, compressed, 0, sz);

            Assert.Equal(_data64KiB.Length, sz);
            Assert.Equal(XXHash64.Compute(_data64KiB).ToHexString(), XXHash64.Compute(decompressed, 0, sz).ToHexString());

        }

        [Theory]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Fastest, 0x6b44, "6d522dca7d96dfe8", "879665c04f8d526d")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Optimal, 0x1b6, "6d522dca7d96dfe8", "cf0f006564a490d7")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.SmallestSize, 0x1a7, "6d522dca7d96dfe8", "35c4c25d8a95aa8c")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Fastest, 0x25582, "6d522dca7d96dfe8", "f6f7ebd36ab15670")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Optimal, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Fastest, 0x25582, "6d522dca7d96dfe8", "f6f7ebd36ab15670")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Optimal, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Fastest, 0x25588, "6d522dca7d96dfe8", "52939e62ee507885")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Optimal, 0x16755, "6d522dca7d96dfe8", "a7fe8e5eb6ce2b02")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.SmallestSize, 0x16755, "6d522dca7d96dfe8", "0cf63fbf2648d734")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Fastest, 0x25588, "6d522dca7d96dfe8", "52939e62ee507885")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Optimal, 0x16755, "6d522dca7d96dfe8", "a7fe8e5eb6ce2b02")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.SmallestSize, 0x16755, "6d522dca7d96dfe8", "0cf63fbf2648d734")]

        public void Data_Stream20MiB_Chunk1MiB(CompressionStreamType type, CompressionLevel level, long compressedSize, string rawXxH64, string compXxH64)
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
                        using (var compressionStream = CompressionStreamFactory.Create(type, compMemoryStream, level, true))
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
                        using (var compressionStream = CompressionStreamFactory.Create(type, compMemoryStream, CompressionMode.Decompress, true))
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
                    Trace.WriteLine($"[InlineData(CompressionStreamType.{type}, CompressionLevel.{level}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    //Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                    //Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
                    //Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
                    //Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected

                }
            }

        }

#if !IS32BIT
        [Theory]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Fastest, 0xaba29, "c668fabe6e6e9235", "b81723649b82a53d")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Optimal, 0x4b9, "c668fabe6e6e9235", "c61dc24b52781f66")]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.SmallestSize, 0x4c7, "c668fabe6e6e9235", "2781a4b8151afdab")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Fastest, 0x3b9193, "c668fabe6e6e9235", "d660c36b53d5724a")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Optimal, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.SmallestSize, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Fastest, 0x3b9193, "c668fabe6e6e9235", "d660c36b53d5724a")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.Optimal, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        [InlineData(CompressionStreamType.DeflateNg, CompressionLevel.SmallestSize, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.Fastest, 0x3b91a5, "c668fabe6e6e9235", "71c2677e0d742cee")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.Optimal, 0x23c137, "c668fabe6e6e9235", "d4055f148c70a44c")]
        //[InlineData(CompressionStreamType.GZip, CompressionLevel.SmallestSize, 0x23c137, "c668fabe6e6e9235", "8139b6563f0a1e18")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.Fastest, 0x3b91a5, "c668fabe6e6e9235", "71c2677e0d742cee")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.Optimal, 0x23c137, "c668fabe6e6e9235", "d4055f148c70a44c")]
        //[InlineData(CompressionStreamType.GZipNg, CompressionLevel.SmallestSize, 0x23c137, "c668fabe6e6e9235", "8139b6563f0a1e18")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Fastest, 0x3b9199, "c668fabe6e6e9235", "77e08be9bcdb4e41")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Optimal, 0x23c12b, "c668fabe6e6e9235", "b5ae77c847a84a88")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.SmallestSize, 0x23c12b, "c668fabe6e6e9235", "89fb4ce3386045e9")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Fastest, 0x3b9199, "c668fabe6e6e9235", "77e08be9bcdb4e41")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.Optimal, 0x23c12b, "c668fabe6e6e9235", "b5ae77c847a84a88")]
        [InlineData(CompressionStreamType.ZLibNg, CompressionLevel.SmallestSize, 0x23c12b, "c668fabe6e6e9235", "89fb4ce3386045e9")]
        public void Data_Stream512MiB_Chunk1MiB(CompressionStreamType type, CompressionLevel level, long compressedSize, string rawXxH64, string compXxH64)
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
                        using (var compressionStream = CompressionStreamFactory.Create(type, compMemoryStream, level, true))
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
                        using (var compressionStream = CompressionStreamFactory.Create(type, compMemoryStream, CompressionMode.Decompress, true))
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
                    //Trace.WriteLine($"[InlineData(CompressionStreamType.{type}, CompressionLevel.{level}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                    Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
                    Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
                    Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected

                }
            }

        }
#endif
    }
}

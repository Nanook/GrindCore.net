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
using System.Drawing;
using System.Reflection.Emit;

namespace GrindCore.Tests
{
    public sealed class CompressionBlockTests
    {
        private static byte[] _dataEmpty;
        private static byte[] _data64KiB;
        private static byte[] _dataNC64KiB;
        private static byte[] _text64KiB;

        static CompressionBlockTests()
        {
            _dataEmpty = new byte[0];
            _data64KiB = TestDataStream.Create(64 * 1024);
            _dataNC64KiB = TestNonCompressibleDataStream.Create(64 * 1024);
            _text64KiB = TestPseudoTextStream.Create(64 * 1024);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type">zlib level 0 to 9</param>
        /// <param name="strategy">0=Normal, 1=Filtered, 2=Huffman, 3=RLE</param>
        /// <param name="header"></param>
        /// <param name="size">Expect compressed size</param>
        /// <param name="expected">Expected xxhash64</param>
        [Theory]
        [InlineData(CompressionAlgorithm.Copy, CompressionType.Fastest, 0x10000, "8d572de6b2804494")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1f6, "1fd0ab5c0058d51a")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x19b, "e39f3f4d64825537")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x18a, "03e8b1d250f7e6aa")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x3cd, "eefbb1f99743a612")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x4be, "6564467a7a492f66")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x2ff, "fd1a57a63d29c607")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x1ea, "5cf931277dcafef1")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x1ea, "5cf931277dcafef1")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x1ea, "5cf931277dcafef1")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x28b, "3eac7ba192b0f661")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0x28b, "3eac7ba192b0f661")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0x28b, "3eac7ba192b0f661")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x1de, "6d7905044230ec1f")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x1de, "069b2a2799eadee8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x1de, "069b2a2799eadee8")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1e5, "b4550df1c1bd0067")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x1e5, "3e6f77f9c11f4e70")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x1e5, "3e6f77f9c11f4e70")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x3d3, "faf781046fb77de8")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x305, "a3c36ab37f8f236d")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x305, "a21b9fa33c110bc5")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x4c4, "fc40654142efec69")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x305, "a3c36ab37f8f236d")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x305, "a21b9fa33c110bc5")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x195, "3545b23ad651d5d4")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x195, "3545b23ad651d5d4")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x195, "3545b23ad651d5d4")]
        public void Data_ByteArray64KiB_BlockCompress(CompressionAlgorithm algorithm, CompressionType type, int size, string expected)
        {
            using (CompressionBlock block = CompressionBlockFactory.Create(algorithm, type, _data64KiB.Length, false, CompressionVersion.Create(algorithm)))
            {
                byte[] compressed = BufferPool.Rent(block.RequiredCompressOutputSize);
                byte[] decompressed = BufferPool.Rent(_data64KiB.Length);

                int sz = block.Compress(_data64KiB, 0, _data64KiB.Length, compressed, 0, compressed.Length);
                int dsz = block.Decompress(compressed, 0, sz, decompressed, 0, decompressed.Length);

                string compHash = XXHash64.Compute(compressed, 0, sz).ToHexString();
                string decompHash = XXHash64.Compute(decompressed, 0, dsz).ToHexString();

                BufferPool.Return(compressed);
                BufferPool.Return(decompressed);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, {type}, 0x{sz:x}, \"{compHash}\")]");
                Assert.Equal(size, sz);
                Assert.Equal(expected, compHash);
                Assert.Equal(_data64KiB.Length, dsz);
                Assert.Equal(XXHash64.Compute(_data64KiB, 0, _data64KiB.Length).ToHexString(), decompHash);
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Fastest, 0x1000a, "f4a2740735a848e5")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.Optimal, 0x1000a, "602832ca2a11542c")]
        [InlineData(CompressionAlgorithm.Deflate, CompressionType.SmallestSize, 0x1000a, "602832ca2a11542c")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Fastest, 0x10021, "a5ed03187d448a19")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.Optimal, 0x10021, "e2f946756bf49fce")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionType.SmallestSize, 0x1000a, "f4a2740735a848e5")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x1000c, "b2cee4d81324c0e2")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x1000c, "03b1e2cafe441952")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x1000c, "03b1e2cafe441952")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x10102, "d26190b7008c1829")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0x10102, "d26190b7008c1829")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0x10102, "d26190b7008c1829")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x103c6, "37e1e569c57d08d8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x103a0, "0965194c263a54e8")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x103a0, "0965194c263a54e8")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x10007, "c93c02419eed314c")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x10007, "8a3d45bab1e3772d")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x10007, "8a3d45bab1e3772d")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x10010, "306fb18b107697be")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x10010, "e6f25d701c2ebb19")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x10010, "cf6bcf32107b4790")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x10021, "687553b61e121c29")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x10021, "750a3decaa561361")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x10010, "ae8890c171895efb")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x1000a, "4b9f7d6be30a4eca")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x1000a, "4b9f7d6be30a4eca")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x1000a, "4b9f7d6be30a4eca")]
        public void DataNonCompressible_ByteArray64KiB_BlockCompress(CompressionAlgorithm algorithm, CompressionType type, int size, string expected)
        {
            using (CompressionBlock block = CompressionBlockFactory.Create(algorithm, type, _dataNC64KiB.Length, false, CompressionVersion.Create(algorithm)))
            {
                byte[] compressed = BufferPool.Rent(block.RequiredCompressOutputSize);
                int sz = block.Compress(_dataNC64KiB, 0, _dataNC64KiB.Length, compressed, 0, compressed.Length);

                string result = XXHash64.Compute(compressed, 0, sz).ToHexString();
                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, {type}, 0x{sz:x}, \"{result}\")]");
                Assert.Equal(size, sz);
                Assert.Equal(expected, result);
            }
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x84d, "25be05c704cb5995")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x5e5, "bc53adaf025e726e")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x16d9, "f9420ec7af17eccf")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x62e, "3da20be52c61534e")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x636, "eca2b056732b6c26")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x200c, "c43831d20deadb6b")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x2651, "8b0b438386b0c1f5")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x83a, "8a7fd9bf458ad52a")]
#else
        [InlineData(CompressionAlgorithm.Copy, CompressionType.Fastest, 0x10000, "cfd90e2b5db1ff5d")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x84d, "25be05c704cb5995")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x5d1, "2b444156a4305ae3")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x4fa, "bd7a15fc895f1b65")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x5e5, "bc53adaf025e726e")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x733, "b3c5d10c6777b8b2")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x733, "b3c5d10c6777b8b2")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x16d9, "f9420ec7af17eccf")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0xeb0, "164b5106da27890c")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0xead, "2dec3fb739b2a8bf")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x62e, "3da20be52c61534e")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x5fe, "c784746694318d8a")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x640, "b39f25c858e03706")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x636, "eca2b056732b6c26")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x605, "9ac9ca397bdbf54b")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x647, "1b1484110fe9391a")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x200c, "c43831d20deadb6b")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0xcc8, "aa99d2252c2f6606")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0xba1, "2b9ecf7dce8e81ce")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x2651, "8b0b438386b0c1f5")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0xbe7, "bd93e4482eb778cb")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0xba1, "2b9ecf7dce8e81ce")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x83a, "8a7fd9bf458ad52a")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x7f0, "8723d465c72e2e88")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x5c0, "6775dd2c3d7c5222")]


#endif
        public void Text_ByteArray64KiB(CompressionAlgorithm algorithm, CompressionType type, int size, string expected)
        {
            using (CompressionBlock block = CompressionBlockFactory.Create(algorithm, type, _text64KiB.Length, false, CompressionVersion.Create(algorithm)))
            {
                byte[] compressed = BufferPool.Rent(block.RequiredCompressOutputSize);
                byte[] decompressed = BufferPool.Rent(_text64KiB.Length);

                int sz = block.Compress(_text64KiB, 0, _text64KiB.Length, compressed, 0, compressed.Length);
                int dsz = block.Decompress(compressed, 0, sz, decompressed, 0, decompressed.Length);

                string compHash = XXHash64.Compute(compressed, 0, sz).ToHexString();
                string decompHash = XXHash64.Compute(decompressed, 0, dsz).ToHexString();

                BufferPool.Return(compressed);
                BufferPool.Return(decompressed);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, {type}, 0x{sz:x}, \"{compHash}\")]");
                Assert.Equal(size, sz);
                Assert.Equal(expected, compHash);
                Assert.Equal(_text64KiB.Length, dsz);
                Assert.Equal(XXHash64.Compute(_text64KiB, 0, _data64KiB.Length).ToHexString(), decompHash);
            }
        }

        [Theory]
#if IS_32BIT
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1, "5896bb9a27ab7ba5")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x9, "9fccd29d986b864f")]
#else
        [InlineData(CompressionAlgorithm.Copy, CompressionType.Fastest, 0x0, "ef46db3751d8e999")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Fastest, 0x1, "5896bb9a27ab7ba5")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.Optimal, 0x1, "5896bb9a27ab7ba5")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionType.SmallestSize, 0x1, "5896bb9a27ab7ba5")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Optimal, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.SmallestSize, 0x6, "d8018fa1b17508d8")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Fastest, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.Optimal, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lz4, CompressionType.SmallestSize, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Fastest, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.Optimal, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma, CompressionType.SmallestSize, 0x5, "00f4f72fb7a8c648")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Fastest, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Optimal, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.SmallestSize, 0x1, "e934a84adb052768")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.Optimal, 0x8, "65f2e912ed28c1ed")]
        [InlineData(CompressionAlgorithm.ZLib, CompressionType.SmallestSize, 0x8, "31edcf2ea90ea820")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Fastest, 0x8, "2bc964c3d0162760")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.Optimal, 0x8, "65f2e912ed28c1ed")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionType.SmallestSize, 0x8, "31edcf2ea90ea820")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Fastest, 0x9, "9fccd29d986b864f")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.Optimal, 0x9, "9fccd29d986b864f")]
        [InlineData(CompressionAlgorithm.ZStd, CompressionType.SmallestSize, 0x9, "9fccd29d986b864f")]
#endif
        public void Text_ByteArrayEmpty(CompressionAlgorithm algorithm, CompressionType type, int size, string expected)
        {
            using (CompressionBlock block = CompressionBlockFactory.Create(algorithm, type, 0, false, CompressionVersion.Create(algorithm)))
            {
                byte[] compressed = BufferPool.Rent(0x100);
                byte[] decompressed = BufferPool.Rent(0x100);

                int sz = block.Compress(_text64KiB, 0, 0, compressed, 0, compressed.Length);
                int dsz = block.Decompress(compressed, 0, sz, decompressed, 0, 0);

                string compHash = XXHash64.Compute(compressed, 0, sz).ToHexString();
                string decompHash = XXHash64.Compute(decompressed, 0, dsz).ToHexString();

                BufferPool.Return(compressed);
                BufferPool.Return(decompressed);

                Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, {type}, 0x{sz:x}, \"{compHash}\")]");
                Assert.Equal(size, sz);
                Assert.Equal(expected, compHash);
                Assert.Equal(0, dsz);
                Assert.Equal(XXHash64.Compute(new byte[0]).ToHexString(), decompHash);
            }
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
            int windowBits = header ? Interop.ZLib.ZLib_DefaultWindowBits : Interop.ZLib.Deflate_DefaultWindowBits;
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress3(compressed, 0, compressed.Length, _data64KiB, 0, _data64KiB.Length, level, windowBits, strategy, version);
            string result = XXHash64.Compute(compressed, 0, sz).ToHexString();
            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            Trace.WriteLine($"[InlineData({level}, {strategy}, {header.ToString().ToLower()}, 0x{sz:x}, \"{result}\")] //{(new string[] { "Normal", "Filtered", "Huffman", "RLE" }[strategy])}");

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress3(decompressed, 0, decompressed.Length, compressed, 0, sz, windowBits, version);

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

            int windowBits = header ? Interop.ZLib.ZLib_DefaultWindowBits : Interop.ZLib.Deflate_DefaultWindowBits;
            byte[] compressed = new byte[_data64KiB.Length * 2];
            int sz = ZLib.Compress3(compressed, 0, compressed.Length, _data64KiB, 0, _data64KiB.Length, level, windowBits, strategy, version);
            string result = XXHash64.Compute(compressed, 0, sz).ToHexString();

            Trace.WriteLine($"[InlineData({level}, {strategy}, {header.ToString().ToLower()}, 0x{sz:x}, \"{result}\")] //{(new string[] { "Normal", "Filtered", "Huffman", "RLE" }[strategy])}");
            Assert.Equal(size, sz);
            Assert.Equal(expected, result);

            byte[] decompressed = new byte[_data64KiB.Length * 2];
            sz = ZLib.Uncompress3(decompressed, 0, decompressed.Length, compressed, 0, sz, windowBits, version);

            Assert.Equal(_data64KiB.Length, sz);
            Assert.Equal(XXHash64.Compute(_data64KiB).ToHexString(), XXHash64.Compute(decompressed, 0, sz).ToHexString());

        }

    }
}

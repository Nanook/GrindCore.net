using GrindCore.Tests.Utility;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using SharpCompress.Compressors.LZMA;
using Nanook.GrindCore.XXHash;
using Nanook.GrindCore;
using System.Diagnostics;

namespace GrindCore.Tests
{
    public class SharpCompressTests
    {
        [Theory]
        //[InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Fastest, 0xaba29, "c668fabe6e6e9235", "b81723649b82a53d")]
        //[InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Optimal, 0x4b9, "c668fabe6e6e9235", "c61dc24b52781f66")]
        //[InlineData(CompressionAlgorithm.Brotli, CompressionLevel.SmallestSize, 0x4c7, "c668fabe6e6e9235", "2781a4b8151afdab")]
        ////[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.Fastest, 0x3b9193, "c668fabe6e6e9235", "d660c36b53d5724a")]
        ////[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.Optimal, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        ////[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.SmallestSize, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        //[InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Fastest, 0x673327, "c668fabe6e6e9235", "f0980ebcdcde5d8b")]
        //[InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Optimal, 0x23c252, "c668fabe6e6e9235", "a332285f97856aa5")]
        //[InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.SmallestSize, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        ////[InlineData(CompressionAlgorithm.GZip, CompressionLevel.Fastest, 0x3b91a5, "c668fabe6e6e9235", "71c2677e0d742cee")]
        ////[InlineData(CompressionAlgorithm.GZip, CompressionLevel.Optimal, 0x23c137, "c668fabe6e6e9235", "d4055f148c70a44c")]
        ////[InlineData(CompressionAlgorithm.GZip, CompressionLevel.SmallestSize, 0x23c137, "c668fabe6e6e9235", "8139b6563f0a1e18")]
        ////[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Fastest, 0x673339, "c668fabe6e6e9235", "82cdb7c74478eca4")]
        ////[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Optimal, 0x23c264, "c668fabe6e6e9235", "6bbd6a89515111a4")]
        ////[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.SmallestSize, 0x23c137, "c668fabe6e6e9235", "8139b6563f0a1e18")]
        ////[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.Fastest, 0x3b9199, "c668fabe6e6e9235", "77e08be9bcdb4e41")]
        ////[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.Optimal, 0x23c12b, "c668fabe6e6e9235", "b5ae77c847a84a88")]
        ////[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.SmallestSize, 0x23c12b, "c668fabe6e6e9235", "89fb4ce3386045e9")]
        //[InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Fastest, 0x67332d, "c668fabe6e6e9235", "d682cb103ee16df0")]
        //[InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Optimal, 0x23c258, "c668fabe6e6e9235", "6bfec589b36154e5")]
        //[InlineData(CompressionAlgorithm.FastLzma2, CompressionLevel.SmallestSize, 0x23c12b, "c668fabe6e6e9235", "89fb4ce3386045e9")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionLevel.SmallestSize, 0x129b1, "c668fabe6e6e9235", "442717ef1c90e829")]
        public void Data_Stream512MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionLevel level, long compressedSize, string rawXxH64, string compXxH64)
        {
            // Process in 1MiB blocks
            int total = 512 * 1024 * 1024; // Total bytes to process
            int blockSize = 1 * 1024 * 1024; // 1MiB block size
            byte[] buffer = new byte[blockSize * 2];
            long totalCompressedBytes = 0;
            long totalInProcessedBytes = 0;
            //long totalOutProcessedBytes = 0;




            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        LzmaEncoderProperties encoderProperties = new LzmaEncoderProperties(
                            eos: false,
                            dictionary: 1 << 24, // Dictionary size (16MB)
                            numFastBytes: 273 // Number of fast bytes
                        );

                        // Hash raw input data and Compress
                        // using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, type, true))
                        using (var compressionStream = new LzmaStream(new LzmaEncoderProperties(true), false, compMemoryStream))
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

                        //// Deompress and hash 
                        //compMemoryStream.Position = 0; //reset for reading
                        //using (var compressionStream = CompressionStreamDotNetFactory.Create(algorithm, compMemoryStream, CompressionMode.Decompress, level, true))
                        //{
                        //    int bytesRead;
                        //    while (totalOutProcessedBytes < total && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(blockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                        //    {
                        //        outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                        //        totalOutProcessedBytes += bytesRead;
                        //    }
                        //    outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        //}
                    }

                    string hashInString = inXxhash.Hash!.ToHexString();
                    string hashCompString = compXxhash.Hash!.ToHexString();
                    //string hashOutString = outXxhash.Hash!.ToHexString();
                    Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionLevel.{level}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    //Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected
                    ////Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                    //Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
                    //Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected

                }
            }

        }
    }
}

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

#if !IS_32BIT
    public sealed class Lzma2Tests
    {
        private void processStream(int dataSize, int bufferSize, Func<Stream, CompressionStream> createCompressStream, Func<Stream, byte[], CompressionStream> createDecompressStream, out string inHash, out string compressedHash, out int compBytes, out string outHash)
        {
            // Process in 1MiB blocks
            int total = dataSize; // Total bytes to process
            byte[] buffer = new byte[bufferSize];
            byte[] properties;
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
                        // Hash raw input data and Process
                        using (var compressionStream = createCompressStream(compMemoryStream))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = inDataStream.Read(buffer, 0, Math.Min(buffer.Length, (int)(total - totalInProcessedBytes)))) > 0)
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
                        compBytes = (int)compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            compMemoryStream.CopyTo(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = createDecompressStream(compMemoryStream, properties))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(buffer.Length, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
                            }
                            outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                            Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                            Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                        }
                    }

                    inHash = inXxhash.Hash!.ToHexString();
                    compressedHash = compXxhash.Hash!.ToHexString();
                    outHash = outXxhash.Hash!.ToHexString();
                }
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest,      0x200000,  1,  0x953, "7833322f45651d24", "eb4d661eaefb646f")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest,      0x200000,  4,  0xec5, "7833322f45651d24", "5f71c3fb6c0b1c7b")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest,      0x600000,  1,  0x908, "7833322f45651d24", "6237a569f033aaa4")]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionType.Fastest,      0x600000,  4,  0xe7c, "7833322f45651d24", "ac01d99f087f75a2")]

        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,            -1,  1,  0x572, "7833322f45651d24", "9b0d306d9158f3f1")]
        [InlineData(CompressionAlgorithm.Lzma2,     CompressionType.Fastest,            -1,  4,  0x572, "7833322f45651d24", "9b0d306d9158f3f1")]
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

            CompressionOptions compOptions = new CompressionOptions()
            {
                Type = type,
                LeaveOpen = true,
                BufferSize = buffLen,
                BlockSize = blockSize,
                ThreadCount = threadCount,
                Version = CompressionVersion.Create(algorithm, "")
            };

            CompressionOptions decompOptions = new CompressionOptions()
            {
                Type = CompressionType.Decompress,
                LeaveOpen = true,
                BufferSize = buffLen,
                Version = CompressionVersion.Create(algorithm, "")
            };

            processStream(streamLen,
                          buffLen,
                          wStream => CompressionStreamFactory.Create(algorithm, wStream, compOptions),
                          (rStream, props) =>
                          {
                              decompOptions.InitProperties = props;
                              return CompressionStreamFactory.Create(algorithm, rStream, decompOptions);
                          },
                          out string hashInString, out string hashCompString, out int compBytes, out string hashOutString);

            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, {(blockSize <= 0 ? blockSize : ("0x" + blockSize.ToString("x")))}, {threadCount}, 0x{compBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
            Assert.Equal(compressedSize, compBytes); //test compressed data size matches expected
            Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
            Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
            Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
        }

        [Theory] // verified hashes that 7zip app creates for the same settings (at the time of testing)
        //[InlineData(CompressionAlgorithm.Lzma2, CompressionType.Level5, -1, 1, 0x1332c, "c668fabe6e6e9235", "f3184dd0b07f0314")] // solid mode with blocksize == -1 OR >= data bing encoded
        [InlineData(CompressionAlgorithm.Lzma2, CompressionType.Level5, -1, 1, 0x572, "7833322f45651d24", "9031f3192bac146c")]
        public void Match7ZipAppHashes(CompressionAlgorithm algorithm, CompressionType type, int blockSize, int threadCount, long compressedSize, string rawXxH64, string compXxH64)
        {

            int streamLen = 6 * 1024 * 1024;
            int buffLen = blockSize <= 0 ? streamLen : blockSize;

            CompressionOptions compOptions = new CompressionOptions()
            {
                Type = type,
                LeaveOpen = true,
                BufferSize = buffLen,
                BlockSize = blockSize,
                ThreadCount = threadCount,
                Version = CompressionVersion.Create(algorithm, "")
            };

            CompressionOptions decompOptions = new CompressionOptions()
            {
                Type = CompressionType.Decompress,
                LeaveOpen = true,
                BufferSize = buffLen,
                Version = CompressionVersion.Create(algorithm, "")
            };

            processStream(streamLen,
                          buffLen,
                          wStream => CompressionStreamFactory.Create(algorithm, wStream, compOptions),
                          (rStream, props) =>
                          {
                              decompOptions.InitProperties = props;
                              return CompressionStreamFactory.Create(algorithm, rStream, decompOptions);
                          },
                          out string hashInString, out string hashCompString, out int compBytes, out string hashOutString);

            Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, {(blockSize <= 0 ? blockSize : ("0x" + blockSize.ToString("x")))}, {threadCount}, 0x{compBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
            Assert.Equal(compressedSize, compBytes); //test compressed data size matches expected
            Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
            Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
            Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
        }
    }
#endif
}

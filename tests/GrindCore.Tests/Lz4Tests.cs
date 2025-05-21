using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using System.Diagnostics;
using System.Security.Cryptography;
using Nanook.GrindCore.ZLib;
using Xunit;
using Nanook.GrindCore.Lzma;
using System;
using Nanook.GrindCore.Lz4;
using System.IO;
using System.IO.Pipes;

namespace GrindCore.Tests
{

    public sealed class Lz4Tests
    {
        private void processStream(int dataSize, int bufferSize, Func<Stream, Stream> createCompressStream, Func<Stream, Stream> createDecompressStream, out string inHash, out string compressedHash, out int compBytes, out string outHash)
        {
            // Process in 1MiB blocks
            int total = dataSize; // Total bytes to process
            byte[] buffer = new byte[bufferSize];
            //byte[] properties;
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
                            }
                            //properties = compressionStream.Properties;
                            compMemoryStream.Flush();
                            //Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                            //Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                        }

                        // Hash Compressed data
                        compBytes = (int)compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            compMemoryStream.CopyTo(cryptoStream);

                        // Deompress and hash 
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = createDecompressStream(compMemoryStream))
                        {
                            int bytesRead;
                            while (totalOutProcessedBytes < total && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(buffer.Length, (int)(total - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
                            }
                            outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                            //Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                            //Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                        }
                    }

                    inHash = inXxhash.Hash!.ToHexString();
                    compressedHash = compXxhash.Hash!.ToHexString();
                    outHash = outXxhash.Hash!.ToHexString();
                }
            }
        }

        [Fact]
        public void Data_Stream6MiB()
        {
            int streamLen = 512 * 1024 * 1024;
            int buffLen = 8 * 0x400;

            processStream(streamLen,
                          buffLen,
                          wStream => new Lz4Stream(wStream, new CompressionOptions() { Type = CompressionType.Optimal, LeaveOpen = true }),
                          rStream => new Lz4Stream(rStream, new CompressionOptions() { Type = CompressionType.Decompress, LeaveOpen = true }),
                          out string hashInString, out string hashCompString, out int compBytes, out string hashOutString);

            //Trace.WriteLine($"[InlineData(CompressionAlgorithm.{algorithm}, CompressionType.{type}, {(blockSize <= 0 ? blockSize : ("0x" + blockSize.ToString("x")))}, {threadCount}, 0x{compBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
            //Assert.Equal(compressedSize, compBytes); //test compressed data size matches expected
            //Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
            //Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected
            Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
        }


        [Fact]
        public void Lz4Test()
        {
            byte[] data = TestDataStream.Create(1024);

            string hashOriginal = ComputeSHA256(data);

            using (FileStream fileStream = new FileStream("compressed.lz4", FileMode.Create))
            {
                using (Lz4Stream lz4Stream = new Lz4Stream(fileStream, new CompressionOptions() { Type = CompressionType.Level1, LeaveOpen = false }))
                {
                    lz4Stream.Write(data, 0, data.Length);
                }
                //fileStream.Flush();
            }

            Trace.WriteLine("Compression complete!");

            byte[] decompressedData = new byte[data.Length];

            using (FileStream fileStream = new FileStream("compressed.lz4", FileMode.Open))
            using (Lz4Stream lz4Stream = new Lz4Stream(fileStream, new CompressionOptions() { Type = CompressionType.Decompress }))
            {
                lz4Stream.Read(decompressedData, 0, decompressedData.Length);
            }

            Trace.WriteLine("Decompression complete!");

            string hashDecompressed = ComputeSHA256(decompressedData);

            // Assert that checksums match
            Assert.Equal(hashOriginal, hashDecompressed);
        }

        private static string ComputeSHA256(byte[] data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }
}

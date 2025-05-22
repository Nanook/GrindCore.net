using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GrindCore.Tests.Utility
{
    internal struct TestResults
    {
        public string InHash;
        public string CompressedHash;
        public long CompressedBytes;
        public string OutHash;
    }
    internal partial class Utilities
    {
        
        public static TestResults TestStreamBlocks(CompressionAlgorithm algorithm, CompressionType type, int dataSize, int bufferSize)
        {
            // Process in 1MiB blocks
            int total = dataSize; // Total bytes to process
            byte[] buffer = BufferPool.Rent(bufferSize);
            long compressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            int? threadCount = algorithm != CompressionAlgorithm.FastLzma2 ? null : 4;

            CompressionOptions compOptions = new CompressionOptions()
            {
                Type = type,
                LeaveOpen = true,
                BufferSize = bufferSize,
                BlockSize = bufferSize,
                ThreadCount = threadCount,
                Version = CompressionVersion.Create(algorithm)
            };

            CompressionOptions decompOptions = new CompressionOptions()
            {
                Type = CompressionType.Decompress,
                LeaveOpen = true,
                BlockSize = bufferSize,
                Version = CompressionVersion.Create(algorithm)
            };

            try
            {
                using (var inXxhash = XXHash64.Create())
                using (var compXxhash = XXHash64.Create())
                using (var outXxhash = XXHash64.Create())
                {
                    using (var inDataStream = new TestDataStream())
                    {
                        using (var compMemoryStream = new MemoryStream())
                        {
                            // Hash raw input data and Process
                            using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, compOptions))
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
                                    decompOptions.InitProperties = compressionStream.Properties;
                                    compressionStream.Complete(); //like dispose, but doesn't risk the object being garbage collected - sets final Positions

                                    Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                                    Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                                }
                            }

                            // Hash Compressed data
                            compressedBytes = (int)compMemoryStream.Position;
                            compMemoryStream.Position = 0; //reset for reading
                            using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                                compMemoryStream.CopyTo(cryptoStream);

                            // Deompress and hash 
                            compMemoryStream.Position = 0; //reset for reading
                            using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, decompOptions))
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

                        return new TestResults
                        {
                            InHash = inXxhash.Hash!.ToHexString(),
                            CompressedHash = compXxhash.Hash!.ToHexString(),
                            OutHash = outXxhash.Hash!.ToHexString(),
                            CompressedBytes = compressedBytes
                        };
                    }
                }
            }
            finally
            {
                BufferPool.Return(buffer);
            }
        }

        public static async Task<TestResults> TestStreamBlocksAsync(CompressionAlgorithm algorithm, CompressionType type, int dataSize, int bufferSize)
        {
            // Process in 1MiB blocks
            int total = dataSize; // Total bytes to process
            byte[] buffer = BufferPool.Rent(bufferSize);
            long compressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            int? threadCount = algorithm != CompressionAlgorithm.FastLzma2 ? null : 4;

            CompressionOptions compOptions = new CompressionOptions()
            {
                Type = type,
                LeaveOpen = true,
                BufferSize = bufferSize,
                BlockSize = bufferSize,
                ThreadCount = threadCount,
                Version = CompressionVersion.Create(algorithm)
            };

            CompressionOptions decompOptions = new CompressionOptions()
            {
                Type = CompressionType.Decompress,
                LeaveOpen = true,
                BlockSize = bufferSize,
                Version = CompressionVersion.Create(algorithm)
            };

            try
            {
                using (var inXxhash = XXHash64.Create())
                using (var compXxhash = XXHash64.Create())
                using (var outXxhash = XXHash64.Create())
                {
                    using (var inDataStream = new TestDataStream())
                    {
                        using (var compMemoryStream = new MemoryStream())
                        {
                            // Hash raw input data and Process
                            using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, compOptions))
                            {
                                using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                                {
                                    int bytesRead;
                                    while (totalInProcessedBytes < total && (bytesRead = await inDataStream.ReadAsync(buffer, 0, Math.Min(bufferSize, (int)(total - totalInProcessedBytes)))) > 0)
                                    {
                                        await cryptoStream.WriteAsync(buffer, 0, bytesRead);
                                        totalInProcessedBytes += bytesRead;
                                    }
                                    await compressionStream.FlushAsync();
                                    decompOptions.InitProperties = compressionStream.Properties;
                                    Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                                    Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                                }
                            }

                            // Hash Compressed data
                            compressedBytes = compMemoryStream.Position;
                            compMemoryStream.Position = 0; //reset for reading
                            using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                                await compMemoryStream.CopyToAsync(cryptoStream);

                            // Decompress and hash 
                            compMemoryStream.Position = 0; //reset for reading
                            using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, decompOptions))
                            {
                                int bytesRead;
                                while (totalOutProcessedBytes < total && (bytesRead = await compressionStream.ReadAsync(buffer, 0, Math.Min((int)decompOptions.BlockSize, (int)(total - totalOutProcessedBytes)))) > 0)
                                {
                                    outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                    totalOutProcessedBytes += bytesRead;
                                }
                                outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                                Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                                Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                            }
                        }

                        return new TestResults
                        {
                            InHash = inXxhash.Hash!.ToHexString(),
                            CompressedHash = compXxhash.Hash!.ToHexString(),
                            OutHash = outXxhash.Hash!.ToHexString(),
                            CompressedBytes = compressedBytes
                        };
                    }
                }
            }
            finally
            {
                BufferPool.Return(buffer);
            }
        }

        public static TestResults TestStreamBytes(
            CompressionAlgorithm algorithm, CompressionType type, int dataSize, int bufferSize)
        {
            int total = dataSize; // Total bytes to process
            long compressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            int? threadCount = algorithm != CompressionAlgorithm.FastLzma2 ? null : 4;

            CompressionOptions compOptions = new CompressionOptions()
            {
                Type = type,
                LeaveOpen = true,
                BufferSize = bufferSize,
                BlockSize = bufferSize,
                ThreadCount = threadCount,
                Version = CompressionVersion.Create(algorithm)
            };

            CompressionOptions decompOptions = new CompressionOptions()
            {
                Type = CompressionType.Decompress,
                LeaveOpen = true,
                BlockSize = bufferSize,
                Version = CompressionVersion.Create(algorithm)
            };

            using (var inXxhash = XXHash64.Create())
            using (var compXxhash = XXHash64.Create())
            using (var outXxhash = XXHash64.Create())
            {
                using (var inDataStream = new TestDataStream())
                {
                    using (var compMemoryStream = new MemoryStream())
                    {
                        // Hash raw input data and Process
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, compOptions))
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
                            decompOptions.InitProperties = compressionStream.Properties;
                            compressionStream.Complete(); //like dispose, but doesn't risk the object being garbage collected - sets final Positions

                            Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                            Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                        }

                        // Hash Compressed data
                        compressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                        {
                            int b;
                            while ((b = compMemoryStream.ReadByte()) != -1)
                                cryptoStream.WriteByte((byte)b);
                        }

                        // Decompress and hash
                        compMemoryStream.Position = 0; //reset for reading
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, decompOptions))
                        {
                            int b;
                            while (totalOutProcessedBytes < total && (b = compressionStream.ReadByte()) != -1)
                            {
                                outXxhash.TransformBlock(new[] { (byte)b }, 0, 1, null, 0);
                                totalOutProcessedBytes++;
                            }
                            outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                            Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                            Assert.Equal(inDataStream.Position, compressionStream.PositionFullSize); //compression position is correct
                        }
                    }

                    return new TestResults
                    {
                        InHash = inXxhash.Hash!.ToHexString(),
                        CompressedHash = compXxhash.Hash!.ToHexString(),
                        OutHash = outXxhash.Hash!.ToHexString(),
                        CompressedBytes = compressedBytes
                    };
                }
            }
        }
    }
}

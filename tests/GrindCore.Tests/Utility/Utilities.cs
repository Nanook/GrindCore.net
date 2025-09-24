using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Xunit;

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

        public static TestResults TestStreamBlocks(Stream data, CompressionAlgorithm algorithm, CompressionType type, int dataSize, int bufferSize, int compSize, int? threads = null, string? version = null)
        {
            // Process in 1MiB blocks
            int total = dataSize; // Total bytes to process
            byte[] buffer = BufferPool.Rent(bufferSize);
            long compressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            int? threadCount = threads;

            CompressionOptions compOptions = new CompressionOptions()
            {
                Type = type,
                LeaveOpen = true,
                BufferSize = bufferSize,
                BlockSize = bufferSize,
                ThreadCount = threadCount,
                Version = CompressionVersion.Create(algorithm, version ?? "")
            };

            CompressionOptions decompOptions = new CompressionOptions()
            {
                Type = CompressionType.Decompress,
                LeaveOpen = true,
                BlockSize = bufferSize,
                Version = CompressionVersion.Create(algorithm, version ?? "")
            };

            try
            {
                //File.WriteAllBytes(@"d:\temp\zstd-non-good.bin", TestNonCompressibleDataStream.Create(6 * 1024 * 1024));

                using (var inXxhash = XXHash64.Create())
                using (var compXxhash = XXHash64.Create())
                using (var outXxhash = XXHash64.Create())
                {
                    using (var compMemoryStream = new MemoryStream(compSize)) //keep sizes down
                    {
                        // Hash raw input data and Process
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, compOptions))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = data.Read(buffer, 0, Math.Min(buffer.Length, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    cryptoStream.Write(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                    //Trace.WriteLine($"{totalInProcessedBytes} of {total} ({compMemoryStream.Position})");
                                }
                                decompOptions.InitProperties = compressionStream.Properties;
                                compressionStream.Complete(); //like dispose, but doesn't risk the object being garbage collected - sets final Positions

                                Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                                Assert.Equal(data.Position, compressionStream.PositionFullSize); //compression position is correct
                            }
                        }

                       // File.WriteAllBytes(@"d:\temp\out.bin", compMemoryStream.ToArray());

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
                            int fixTotal = total == 0 ? 1 : total;
                            while (totalOutProcessedBytes < fixTotal && (bytesRead = compressionStream.Read(buffer, 0, Math.Min(buffer.Length, (int)(fixTotal - totalOutProcessedBytes)))) > 0)
                            {
                                outXxhash.TransformBlock(buffer, 0, bytesRead, null, 0);
                                totalOutProcessedBytes += bytesRead;
                                fixTotal = total; //reset
                            }
                            outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                            int adjust = compMemoryStream.Position > 1 && algorithm == CompressionAlgorithm.Lzma2 ? -1 : 0; //null terminator isn't read
                            if (algorithm == CompressionAlgorithm.Lzma2 && compMemoryStream.Position != compressionStream.Position)
                                Assert.Equal(compMemoryStream.Position, compressionStream.Position + 1); //add missing null
                            else
                                Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                            Assert.Equal(data.Position, compressionStream.PositionFullSize); //compression position is correct
                        }
                        compMemoryStream.SetLength(0);
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
            finally
            {
                BufferPool.Return(buffer);
            }
        }

        public static async Task<TestResults> TestStreamBlocksAsync(Stream data, CompressionAlgorithm algorithm, CompressionType type, int dataSize, int bufferSize, int compSize)
        {
            // Process in 1MiB blocks
            int total = dataSize; // Total bytes to process
            byte[] buffer = BufferPool.Rent(bufferSize);
            long compressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            int? threadCount = 0;

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
                    using (var compMemoryStream = new MemoryStream(compSize))
                    {
                        // Hash raw input data and Process
                        using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, compOptions))
                        {
                            using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                            {
                                int bytesRead;
                                while (totalInProcessedBytes < total && (bytesRead = await data.ReadAsync(buffer, 0, Math.Min(bufferSize, (int)(total - totalInProcessedBytes)))) > 0)
                                {
                                    await cryptoStream.WriteAsync(buffer, 0, bytesRead);
                                    totalInProcessedBytes += bytesRead;
                                }
                                decompOptions.InitProperties = compressionStream.Properties;
                                await compressionStream.CompleteAsync();
                                Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                                Assert.Equal(data.Position, compressionStream.PositionFullSize); //compression position is correct
                            }
                        }

                        // Hash Compressed data
                        compressedBytes = compMemoryStream.Position;
                        compMemoryStream.Position = 0; //reset for reading
                        using (var cryptoStream = new CryptoStream(Stream.Null, compXxhash, CryptoStreamMode.Write, true))
                            await compMemoryStream.CopyToAsync(cryptoStream);

                        // DecodeData and hash 
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
                            Assert.Equal(data.Position, compressionStream.PositionFullSize); //compression position is correct
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
            finally
            {
                BufferPool.Return(buffer);
            }
        }

        public static TestResults TestStreamBytes(Stream data, CompressionAlgorithm algorithm, CompressionType type, int dataSize, int bufferSize, int compSize)
        {
            int total = dataSize; // Total bytes to process
            long compressedBytes = 0;
            long totalInProcessedBytes = 0;
            long totalOutProcessedBytes = 0;

            byte[] by = new byte[1];
            int? threadCount = 0;

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
                using (var compMemoryStream = new MemoryStream(compSize))
                {
                    // Hash raw input data and Process
                    using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, compOptions))
                    {
                        using (var cryptoStream = new CryptoStream(compressionStream, inXxhash, CryptoStreamMode.Write, true))
                        {
                            while (totalInProcessedBytes < total)
                            {
                                int b = data.ReadByte();
                                if (b == -1) break;
                                cryptoStream.WriteByte((byte)b);
                                totalInProcessedBytes++;
                                //Trace.WriteLine($"{totalInProcessedBytes} of {total} ({compMemoryStream.Position})");
                            }
                        }
                        decompOptions.InitProperties = compressionStream.Properties;
                        compressionStream.Complete(); //like dispose, but doesn't risk the object being garbage collected - sets final Positions

                        Assert.Equal(compMemoryStream.Position, compressionStream.Position); //compression position is correct
                        Assert.Equal(data.Position, compressionStream.PositionFullSize); //compression position is correct
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

                    // DecodeData and hash
                    compMemoryStream.Position = 0; //reset for reading
                    using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, decompOptions))
                    {
                        int b;
                        while ((b = compressionStream.ReadByte()) != -1 || totalOutProcessedBytes < total) //OR due to allow extra stream bytes to be read
                        {
                            by[0] = (byte)b;
                            outXxhash.TransformBlock(by, 0, 1, null, 0);
                            totalOutProcessedBytes++;
                        }
                        outXxhash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

                        Assert.Equal(compMemoryStream.Position, compressionStream.Position);
                        Assert.Equal(data.Position, compressionStream.PositionFullSize); //compression position is correct
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

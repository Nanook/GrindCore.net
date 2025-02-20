using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using System.Diagnostics;
using System.Security.Cryptography;
using Nanook.GrindCore.ZLib;
using DN=System.IO.Compression;
using Xunit;
using System.Reflection.Emit;
using System.IO.Compression;

namespace GrindCore.Tests
{
#if DEBUG //just for debugging against the DotNet versions
    public sealed class CompressionStreamDotNetTests
    {
        private static byte[] _dataEmpty;
        private static byte[] _data64KiB;
        private static byte[] _text64KiB;

        private static readonly Dictionary<CompressionAlgorithm, Func<Stream, CompressionMode, CompressionLevel, bool, Stream>> streamCreators = new Dictionary<CompressionAlgorithm, Func<Stream, CompressionMode, CompressionLevel, bool, Stream>>()
        {
            { CompressionAlgorithm.GZip, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new DN.GZipStream(stream, mode, leaveOpen) : new DN.GZipStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.ZLib, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new DN.ZLibStream(stream, mode, leaveOpen) : new DN.ZLibStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.Deflate, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new DN.DeflateStream(stream, mode, leaveOpen) : new DN.DeflateStream(stream, level) },
            { CompressionAlgorithm.GZipNg, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new DN.GZipStream(stream, mode, leaveOpen) : new DN.GZipStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.ZLibNg, (stream, mode, level, leaveOpen) =>mode == CompressionMode.Decompress ? new DN.ZLibStream(stream, mode, leaveOpen) :  new DN.ZLibStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.DeflateNg, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new DN.DeflateStream(stream, mode, leaveOpen) : new DN.DeflateStream(stream, level, leaveOpen) },
            { CompressionAlgorithm.Brotli, (stream, mode, level, leaveOpen) => mode == CompressionMode.Decompress ? new DN.BrotliStream(stream, mode, leaveOpen) : new DN.BrotliStream(stream, level, leaveOpen) }
        };

        public static Stream Create(CompressionAlgorithm algorithm, Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen = false)
        {
            return create(algorithm, stream, mode, level, leaveOpen);
        }

        private static Stream create(CompressionAlgorithm algorithm, Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        {
            if (streamCreators.TryGetValue(algorithm, out var creator))
                return creator(stream, mode, level, leaveOpen);

            throw new ArgumentException("Unsupported stream algorithm", nameof(algorithm));
        }
        static CompressionStreamDotNetTests()
        {
            _dataEmpty = new byte[0];
            _data64KiB = TestDataStream.Create(64 * 1024);
            _text64KiB = TestPseudoTextStream.Create(64 * 1024);
        }


        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Fastest, 0x6b44, "6d522dca7d96dfe8", "879665c04f8d526d")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Optimal, 0x1b6, "6d522dca7d96dfe8", "cf0f006564a490d7")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.SmallestSize, 0x1a7, "6d522dca7d96dfe8", "35c4c25d8a95aa8c")]
        //[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.Fastest, 0x25582, "6d522dca7d96dfe8", "f6f7ebd36ab15670")]
        //[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.Optimal, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        //[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Fastest, 0x4097b, "6d522dca7d96dfe8", "0e9009f7ff7f372d")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Optimal, 0x16758, "6d522dca7d96dfe8", "9567de3b29629820")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Fastest, 0x25582, "6d522dca7d96dfe8", "f6f7ebd36ab15670")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.Optimal, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionType.SmallestSize, 0x1674f, "6d522dca7d96dfe8", "f2985ec622a43a65")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Fastest, 0x4098d, "6d522dca7d96dfe8", "572527c6643e493c")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Optimal, 0x1676a, "6d522dca7d96dfe8", "57b4bf88afef1001")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.SmallestSize, 0x16761, "6d522dca7d96dfe8", "4aaadffba8313a94")]
        //[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.Fastest, 0x25588, "6d522dca7d96dfe8", "52939e62ee507885")]
        //[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.Optimal, 0x16755, "6d522dca7d96dfe8", "a7fe8e5eb6ce2b02")]
        //[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.SmallestSize, 0x16755, "6d522dca7d96dfe8", "0cf63fbf2648d734")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Fastest, 0x40981, "6d522dca7d96dfe8", "63423a67659ba6ca")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Optimal, 0x1675e, "6d522dca7d96dfe8", "976b095b1f42c3e1")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.SmallestSize, 0x16755, "6d522dca7d96dfe8", "0cf63fbf2648d734")]

        public void Data_Stream20MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionLevel level, long compressedSize, string rawXxH64, string compXxH64)
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
                        // using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, type, true))
                        using (var compressionStream = Create(algorithm, compMemoryStream, CompressionMode.Compress, level, true))
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
                        using (var compressionStream = Create(algorithm, compMemoryStream, CompressionMode.Decompress, level, true))
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
                    //Trace.WriteLine($"[InlineData(CompressionAlgorithm.{type}, CompressionType.{level}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected
                    Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                    Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
                    Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected

                }
            }

        }

        [Theory]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Fastest, 0xaba29, "c668fabe6e6e9235", "b81723649b82a53d")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Optimal, 0x4b9, "c668fabe6e6e9235", "c61dc24b52781f66")]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.SmallestSize, 0x4c7, "c668fabe6e6e9235", "2781a4b8151afdab")]
        //[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.Fastest, 0x3b9193, "c668fabe6e6e9235", "d660c36b53d5724a")]
        //[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.Optimal, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        //[InlineData(CompressionAlgorithm.Deflate, CompressionLevel.SmallestSize, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Fastest, 0x673327, "c668fabe6e6e9235", "f0980ebcdcde5d8b")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Optimal, 0x23c252, "c668fabe6e6e9235", "a332285f97856aa5")]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.SmallestSize, 0x23c125, "c668fabe6e6e9235", "5119ef157d67232a")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionLevel.Fastest, 0x3b91a5, "c668fabe6e6e9235", "71c2677e0d742cee")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionLevel.Optimal, 0x23c137, "c668fabe6e6e9235", "d4055f148c70a44c")]
        //[InlineData(CompressionAlgorithm.GZip, CompressionLevel.SmallestSize, 0x23c137, "c668fabe6e6e9235", "8139b6563f0a1e18")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Fastest, 0x673339, "c668fabe6e6e9235", "82cdb7c74478eca4")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Optimal, 0x23c264, "c668fabe6e6e9235", "6bbd6a89515111a4")]
        //[InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.SmallestSize, 0x23c137, "c668fabe6e6e9235", "8139b6563f0a1e18")]
        //[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.Fastest, 0x3b9199, "c668fabe6e6e9235", "77e08be9bcdb4e41")]
        //[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.Optimal, 0x23c12b, "c668fabe6e6e9235", "b5ae77c847a84a88")]
        //[InlineData(CompressionAlgorithm.ZLib, CompressionLevel.SmallestSize, 0x23c12b, "c668fabe6e6e9235", "89fb4ce3386045e9")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Fastest, 0x67332d, "c668fabe6e6e9235", "d682cb103ee16df0")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Optimal, 0x23c258, "c668fabe6e6e9235", "6bfec589b36154e5")]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.SmallestSize, 0x23c12b, "c668fabe6e6e9235", "89fb4ce3386045e9")]
        public void Data_Stream512MiB_Chunk1MiB(CompressionAlgorithm algorithm, CompressionLevel level, long compressedSize, string rawXxH64, string compXxH64)
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
                        // using (var compressionStream = CompressionStreamFactory.Create(algorithm, compMemoryStream, type, true))
                        using (var compressionStream = Create(algorithm, compMemoryStream, CompressionMode.Compress, level, true))
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
                        using (var compressionStream = Create(algorithm, compMemoryStream, CompressionMode.Decompress, level, true))
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
                    //Trace.WriteLine($"[InlineData(CompressionAlgorithm.{type}, CompressionType.{level}, 0x{totalCompressedBytes:x}, \"{hashInString}\", \"{hashCompString}\")]");
                    Assert.Equal(compressedSize, totalCompressedBytes); //test compressed data size matches expected
                    Assert.Equal(hashInString, hashOutString); //test IN and decompressed data hashes match
                    Assert.Equal(rawXxH64, hashInString); //test raw data hash matches expected
                    Assert.Equal(compXxH64, hashCompString); //test compressed data hash matches expected

                }
            }

        }
    }
#endif
}

using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Nanook.GrindCore.Lzma;
using Nanook.GrindCore.Lz4;
using Nanook.GrindCore.ZStd;
using Nanook.GrindCore.DeflateZLib;
using Nanook.GrindCore.GZip;
using Nanook.GrindCore.ZLib;
using Nanook.GrindCore.Brotli;
using Nanook.GrindCore.FastLzma2;
using Nanook.GrindCore.Copy;
using Nanook.GrindCore.XXHash;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace GrindCore.Tests
{
    /// <summary>
    /// Tests for async compression and decompression operations to verify async I/O without blocking.
    /// The tests use conditional fallbacks so they can run on older TFMs (e.g., .NET Framework 4.8)
    /// which do not provide the Span/Memory-based Stream overloads.
    /// </summary>
    public sealed class CompressionStreamAsyncTests
    {
        private static byte[] _Data64KiB;
        private static byte[] _Text64KiB;

        static CompressionStreamAsyncTests()
        {
            _Data64KiB = TestDataStream.Create(64 * 1024);
            _Text64KiB = TestPseudoTextStream.Create(64 * 1024);
        }

        #region Small Data Async Tests (64 KiB)

        [Theory]
        #if !IS_32BIT
        [InlineData(CompressionAlgorithm.Lzma, CompressionLevel.Fastest)]
        [InlineData(CompressionAlgorithm.Lzma2, CompressionLevel.Fastest)]
        [InlineData(CompressionAlgorithm.FastLzma2, CompressionLevel.Fastest)]
        #endif
        [InlineData(CompressionAlgorithm.Lz4, CompressionLevel.Fastest)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionLevel.Fastest)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Fastest)]
        [InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Fastest)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Fastest)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Fastest)]
        public async Task AsyncCompress_SmallData_MatchesSyncResults(CompressionAlgorithm algorithm, CompressionLevel level)
        {
            byte[]? properties = null;

            // Compress using sync API
            byte[] syncCompressed;
            using (var syncOutput = new MemoryStream())
            {
                using (var syncStream = createCompressionStream(algorithm, syncOutput, CompressionMode.Compress, level))
                {
                    syncStream.Write(_Data64KiB, 0, _Data64KiB.Length);
                    if (syncStream is CompressionStream cs)
                        properties = cs.Properties;
                }
                syncCompressed = syncOutput.ToArray();
            }

            // Compress using async API
            byte[] asyncCompressed;
            using (var asyncOutput = new MemoryStream())
            {
                using (var asyncStream = createCompressionStream(algorithm, asyncOutput, CompressionMode.Compress, level))
                {
                    await asyncStream.WriteAsync(_Data64KiB, 0, _Data64KiB.Length);
                    if (asyncStream is CompressionStream cs && properties == null)
                        properties = cs.Properties;
                }
                asyncCompressed = asyncOutput.ToArray();
            }

            // Results should be identical
            Assert.Equal(syncCompressed.Length, asyncCompressed.Length);
            Assert.Equal(syncCompressed, asyncCompressed);

            // Verify decompression works
            byte[] decompressed;
            using (var inputStream = new MemoryStream(asyncCompressed))
            using (var decompressionStream = createCompressionStream(algorithm, inputStream, CompressionMode.Decompress, level, properties!))
            using (var outputStream = new MemoryStream())
            {
                await decompressionStream.CopyToAsync(outputStream);
                decompressed = outputStream.ToArray();
            }

            Assert.Equal(_Data64KiB, decompressed);
        }

        [Theory]
        #if !IS_32BIT
        [InlineData(CompressionAlgorithm.Lzma)]
        [InlineData(CompressionAlgorithm.Lzma2)]
        [InlineData(CompressionAlgorithm.FastLzma2)]
        #endif
        [InlineData(CompressionAlgorithm.Lz4)]
        [InlineData(CompressionAlgorithm.ZStd)]
        [InlineData(CompressionAlgorithm.DeflateNg)]
        [InlineData(CompressionAlgorithm.GZipNg)]
        [InlineData(CompressionAlgorithm.ZLibNg)]
        [InlineData(CompressionAlgorithm.Brotli)]
        [InlineData(CompressionAlgorithm.Copy)]
        public async Task AsyncDecompress_SmallData_MatchesSyncResults(CompressionAlgorithm algorithm)
        {
            var level = CompressionLevel.Optimal;
            byte[]? properties = null;

            // First compress the data
            byte[] compressed;
            using (var output = new MemoryStream())
            {
                using (var stream = createCompressionStream(algorithm, output, CompressionMode.Compress, level))
                {
                    stream.Write(_Data64KiB, 0, _Data64KiB.Length);
                    if (stream is CompressionStream cs)
                        properties = cs.Properties;
                }
                compressed = output.ToArray();
            }

            // Decompress using sync API
            byte[] syncDecompressed;
            using (var input = new MemoryStream(compressed))
            using (var stream = createCompressionStream(algorithm, input, CompressionMode.Decompress, level, properties!))
            using (var output = new MemoryStream())
            {
                stream.CopyTo(output);
                syncDecompressed = output.ToArray();
            }

            // Decompress using async API
            byte[] asyncDecompressed;
            using (var input = new MemoryStream(compressed))
            using (var stream = createCompressionStream(algorithm, input, CompressionMode.Decompress, level, properties!))
            using (var output = new MemoryStream())
            {
                await stream.CopyToAsync(output);
                asyncDecompressed = output.ToArray();
            }

            // Results should be identical
            Assert.Equal(syncDecompressed, asyncDecompressed);
            Assert.Equal(_Data64KiB, asyncDecompressed);
        }

        #endregion

        #region Streaming Async Tests (Large Data)

        [Theory]
        //[InlineData(CompressionAlgorithm.Lzma, CompressionLevel.Fastest, 1 * 1024 * 1024)] // LZMA requires properties - skipped for large streaming
        [InlineData(CompressionAlgorithm.Lz4, CompressionLevel.Fastest, 1 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionLevel.Fastest, 1 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Fastest, 1 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Fastest, 1 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Fastest, 1 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Fastest, 1 * 1024 * 1024)]
        //[InlineData(CompressionAlgorithm.Lzma, CompressionLevel.Optimal, 5 * 1024 * 1024)] // LZMA requires properties - skipped for large streaming
        [InlineData(CompressionAlgorithm.Lz4, CompressionLevel.Optimal, 10 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.ZStd, CompressionLevel.Optimal, 10 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.DeflateNg, CompressionLevel.Optimal, 5 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.GZipNg, CompressionLevel.Optimal, 5 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.ZLibNg, CompressionLevel.Optimal, 5 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.Brotli, CompressionLevel.Optimal, 5 * 1024 * 1024)]
        public async Task AsyncCompress_LargeStreaming_ProducesValidOutput(
            CompressionAlgorithm algorithm, 
            CompressionLevel level, 
            int totalBytes)
        {
            int blockSize = 64 * 1024; // 64 KiB chunks
            byte[] buffer = new byte[blockSize];
            long totalCompressedBytes = 0;
            ulong inputHash;
            ulong outputHash;

            using (var inHasher = XXHash64.Create())
            using (var outHasher = XXHash64.Create())
            {
                // Compress in chunks
                using (var inputStream = new TestDataStream())
                using (var compressedStream = new MemoryStream())
                {
                    byte[]? properties = null;
                    using (var compressionStream = createCompressionStream(algorithm, compressedStream, CompressionMode.Compress, level))
                    using (var hashStream = new CryptoStream(compressionStream, inHasher, CryptoStreamMode.Write, true))
                    {
                        long totalRead = 0;
                        int bytesRead;
                        while (totalRead < totalBytes && (bytesRead = await inputStream.ReadAsync(buffer, 0, Math.Min(blockSize, totalBytes - (int)totalRead))) > 0)
                        {
                            await hashStream.WriteAsync(buffer, 0, bytesRead);
                            totalRead += bytesRead;
                        }
                    }
                    
                    // Capture properties after completion but before disposal
                    if (compressedStream.Position > 0)
                    {
                        compressedStream.Position = 0;
                        // Read compressed stream to get properties if needed for LZMA
                        // For now we'll just use null since properties aren't accessible after disposal
                        properties = null;  // Note: LZMA properties would need to be captured differently
                    }
                    
                    totalCompressedBytes = compressedStream.Position;
                    inputHash = BitConverter.ToUInt64(inHasher.Hash!, 0);

                    // Decompress and verify
                    compressedStream.Position = 0;
                    using (var decompressionStream = createCompressionStream(algorithm, compressedStream, CompressionMode.Decompress, level, properties!))
                    {
                        long totalDecompressed = 0;
                        int bytesRead;
                        while (totalDecompressed < totalBytes && (bytesRead = await decompressionStream.ReadAsync(buffer, 0, Math.Min(blockSize, totalBytes - (int)totalDecompressed))) > 0)
                        {
                            outHasher.TransformBlock(buffer, 0, bytesRead, null, 0);
                            totalDecompressed += bytesRead;
                        }
                        outHasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    }
                    outputHash = BitConverter.ToUInt64(outHasher.Hash!, 0);
                }
            }

            // Verify compression happened (output should be smaller than input)
            Assert.True(totalCompressedBytes < totalBytes, $"Compressed size {totalCompressedBytes} should be less than input size {totalBytes}");
            
            // Verify data integrity
            Assert.Equal(inputHash, outputHash);
            
            Trace.WriteLine($"[{algorithm}] [{level}] Input: {totalBytes:N0} bytes, Compressed: {totalCompressedBytes:N0} bytes, Ratio: {(double)totalCompressedBytes / totalBytes:P2}");
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma, 2 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.Lzma2, 2 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.FastLzma2, 2 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.Lz4, 5 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.ZStd, 5 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.DeflateNg, 2 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.GZipNg, 2 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.ZLibNg, 2 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.Brotli, 2 * 1024 * 1024)]
        [InlineData(CompressionAlgorithm.Copy, 5 * 1024 * 1024)]
        public async Task AsyncReadWrite_InterleavedOperations_WorksCorrectly(
            CompressionAlgorithm algorithm,
            int totalBytes)
        {
            var level = CompressionLevel.Optimal;
            int blockSize = 128 * 1024; // 128 KiB chunks
            byte[] writeBuffer = new byte[blockSize];
            byte[] readBuffer = new byte[blockSize];
            ulong inputHash;
            ulong outputHash;

            using (var inHasher = XXHash64.Create())
            using (var outHasher = XXHash64.Create())
            {
                // Create compressed data
                using (var inputStream = new TestDataStream())
                using (var compressedStream = new MemoryStream())
                {
                    byte[]? properties = null;
                    // Compress with async writes
                    using (var compressionStream = createCompressionStream(algorithm, compressedStream, CompressionMode.Compress, level))
                    {
                        long totalWritten = 0;
                        int bytesRead;
                        while (totalWritten < totalBytes && (bytesRead = await inputStream.ReadAsync(writeBuffer, 0, Math.Min(blockSize, totalBytes - (int)totalWritten))) > 0)
                        {
                            await compressionStream.WriteAsync(writeBuffer, 0, bytesRead);
                            inHasher.TransformBlock(writeBuffer, 0, bytesRead, null, 0);
                            totalWritten += bytesRead;
                        }
                        inHasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        if (compressionStream is CompressionStream cs)
                            properties = cs.Properties;
                    }
                    inputHash = BitConverter.ToUInt64(inHasher.Hash!, 0);

                    // Decompress with async reads
                    compressedStream.Position = 0;
                    using (var decompressionStream = createCompressionStream(algorithm, compressedStream, CompressionMode.Decompress, level, properties!))
                    {
                        long totalRead = 0;
                        int bytesRead;
                        while (totalRead < totalBytes && (bytesRead = await decompressionStream.ReadAsync(readBuffer, 0, Math.Min(blockSize, totalBytes - (int)totalRead))) > 0)
                        {
                            outHasher.TransformBlock(readBuffer, 0, bytesRead, null, 0);
                            totalRead += bytesRead;
                        }
                        outHasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    }
                    outputHash = BitConverter.ToUInt64(outHasher.Hash!, 0);
                }
            }

            Assert.Equal(inputHash, outputHash);
        }

        #endregion

        #region Cancellation Tests

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma)]
        [InlineData(CompressionAlgorithm.Lzma2)]
        [InlineData(CompressionAlgorithm.FastLzma2)]
        [InlineData(CompressionAlgorithm.Lz4)]
        [InlineData(CompressionAlgorithm.ZStd)]
        [InlineData(CompressionAlgorithm.DeflateNg)]
        [InlineData(CompressionAlgorithm.GZipNg)]
        [InlineData(CompressionAlgorithm.ZLibNg)]
        [InlineData(CompressionAlgorithm.Brotli)]
        [InlineData(CompressionAlgorithm.Copy)]
        public async Task AsyncCompress_WithCancellation_ThrowsOperationCanceledException(CompressionAlgorithm algorithm)
        {
            using (var cts = new CancellationTokenSource())
            {
                var level = CompressionLevel.Optimal;
                int totalBytes = 10 * 1024 * 1024; // 10 MiB
                int blockSize = 64 * 1024;
                byte[] buffer = new byte[blockSize];

                using (var inputStream = new TestDataStream())
                using (var outputStream = new MemoryStream())
                using (var compressionStream = createCompressionStream(algorithm, outputStream, CompressionMode.Compress, level))
                {
                    // Cancel after writing some data
                    long totalWritten = 0;
                    bool cancelled = false;

                    try
                    {
                        int bytesRead;
                        while (totalWritten < totalBytes && (bytesRead = await inputStream.ReadAsync(buffer, 0, Math.Min(blockSize, totalBytes - (int)totalWritten))) > 0)
                        {
                            // Cancel after writing ~1 MiB
                            if (totalWritten > 1 * 1024 * 1024 && !cancelled)
                            {
                                cts.Cancel();
                                cancelled = true;
                            }

                            await compressionStream.WriteAsync(buffer, 0, bytesRead, cts.Token);
                            totalWritten += bytesRead;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected - cancellation was requested
                        Assert.True(cancelled, "OperationCanceledException should only occur after cancellation was requested");
                        return;
                    }
                }

                // If we get here without exception, the test should fail (unless we wrote all data before cancelling)
                // xUnit does not provide Assert.Fail; use Assert.False(false, message) to indicate failure.
                Assert.False(true, "Expected OperationCanceledException but operation completed");
            }
        }

        #endregion

        #region Memory<T> Overload Tests

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma)]
        [InlineData(CompressionAlgorithm.Lzma2)]
        [InlineData(CompressionAlgorithm.FastLzma2)]
        [InlineData(CompressionAlgorithm.Lz4)]
        [InlineData(CompressionAlgorithm.ZStd)]
        [InlineData(CompressionAlgorithm.DeflateNg)]
        [InlineData(CompressionAlgorithm.GZipNg)]
        [InlineData(CompressionAlgorithm.ZLibNg)]
        [InlineData(CompressionAlgorithm.Brotli)]
        [InlineData(CompressionAlgorithm.Copy)]
        public async Task AsyncMemoryOverloads_ProduceSameResultsAsArrayOverloads(CompressionAlgorithm algorithm)
        {
            var level = CompressionLevel.Optimal;

            byte[]? properties = null;
            
            // Compress using byte[] overload
            byte[] arrayCompressed;
            using (var output = new MemoryStream())
            {
                using (var stream = createCompressionStream(algorithm, output, CompressionMode.Compress, level))
                {
                    await stream.WriteAsync(_Data64KiB, 0, _Data64KiB.Length);
                    if (stream is CompressionStream cs)
                        properties = cs.Properties;
                }
                arrayCompressed = output.ToArray();
            }

            // Compress using Memory<byte> overload
            byte[] memoryCompressed;
            using (var output = new MemoryStream())
            {
                using (var stream = createCompressionStream(algorithm, output, CompressionMode.Compress, level))
                {
#if NET8_0_OR_GREATER
                    await stream.WriteAsync(_Data64KiB.AsMemory());
#else
                    await stream.WriteAsync(_Data64KiB, 0, _Data64KiB.Length);
#endif
                }
                memoryCompressed = output.ToArray();
            }

            // Both should produce identical results
            Assert.Equal(arrayCompressed, memoryCompressed);

            // Decompress using byte[] overload
            byte[] arrayDecompressed;
            using (var input = new MemoryStream(arrayCompressed))
            using (var stream = createCompressionStream(algorithm, input, CompressionMode.Decompress, level, properties!))
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, bytesRead);
                }
                arrayDecompressed = output.ToArray();
            }

            // Decompress using Memory<byte> overload
            byte[] memoryDecompressed;
            using (var input = new MemoryStream(memoryCompressed))
            using (var stream = createCompressionStream(algorithm, input, CompressionMode.Decompress, level, properties!))
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int bytesRead;
#if NET8_0_OR_GREATER
                while ((bytesRead = await stream.ReadAsync(buffer.AsMemory())) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, bytesRead));
                }
#else
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, bytesRead);
    }
#endif
                memoryDecompressed = output.ToArray();
            }

            // All decompressed results should match original
            Assert.Equal(_Data64KiB, arrayDecompressed);
            Assert.Equal(_Data64KiB, memoryDecompressed);
        }

        #endregion

        #region Helper Methods

        private static Stream createCompressionStream(
            CompressionAlgorithm algorithm,
            Stream baseStream,
            CompressionMode mode,
            CompressionLevel level,
            byte[]? initProperties = null)
        {
            var options = new CompressionOptions
            {
                Type = mode == CompressionMode.Compress ? (CompressionType)level : CompressionType.Decompress,
                LeaveOpen = true,
                InitProperties = initProperties
            };

            return algorithm switch
            {
                CompressionAlgorithm.Lzma => new LzmaStream(baseStream, options),
                CompressionAlgorithm.Lzma2 => new Lzma2Stream(baseStream, options),
                CompressionAlgorithm.FastLzma2 => new FastLzma2Stream(baseStream, options),
                CompressionAlgorithm.Lz4 => new Lz4Stream(baseStream, options),
                CompressionAlgorithm.ZStd => new ZStdStream(baseStream, options),
                CompressionAlgorithm.DeflateNg => new Nanook.GrindCore.DeflateZLib.DeflateStream(baseStream, options),
                CompressionAlgorithm.GZipNg => new Nanook.GrindCore.GZip.GZipStream(baseStream, options),
                CompressionAlgorithm.ZLibNg => new Nanook.GrindCore.ZLib.ZLibStream(baseStream, options),
                CompressionAlgorithm.Brotli => new Nanook.GrindCore.Brotli.BrotliStream(baseStream, options),
                CompressionAlgorithm.Copy => new Nanook.GrindCore.Copy.CopyStream(baseStream, options),
                _ => throw new ArgumentException($"Unsupported algorithm: {algorithm}")
            };
        }

        #endregion

        #region Complete Async Tests

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma)]
        [InlineData(CompressionAlgorithm.Lzma2)]
        [InlineData(CompressionAlgorithm.FastLzma2)]
        [InlineData(CompressionAlgorithm.Lz4)]
        [InlineData(CompressionAlgorithm.ZStd)]
        [InlineData(CompressionAlgorithm.DeflateNg)]
        [InlineData(CompressionAlgorithm.GZipNg)]
        [InlineData(CompressionAlgorithm.ZLibNg)]
        [InlineData(CompressionAlgorithm.Brotli)]
        [InlineData(CompressionAlgorithm.Copy)]
        public async Task CompleteAsync_FinalizesCompressionCorrectly(CompressionAlgorithm algorithm)
        {
            var level = CompressionLevel.Optimal;
            byte[] compressed;
            byte[]? properties = null;

            // Compress and call CompleteAsync
            using (var output = new MemoryStream())
            {
                var stream = createCompressionStream(algorithm, output, CompressionMode.Compress, level);
                await stream.WriteAsync(_Data64KiB, 0, _Data64KiB.Length);
                
                // Complete without disposing
                await ((CompressionStream)stream).CompleteAsync(CancellationToken.None);
                
                if (stream is CompressionStream cs)
                    properties = cs.Properties;
                
                compressed = output.ToArray();
                
                // Now dispose
                stream.Dispose();
            }

            // Verify decompression works
            byte[] decompressed;
            using (var input = new MemoryStream(compressed))
            using (var stream = createCompressionStream(algorithm, input, CompressionMode.Decompress, level, properties!))
            using (var output = new MemoryStream())
            {
                await stream.CopyToAsync(output);
                decompressed = output.ToArray();
            }

            Assert.Equal(_Data64KiB, decompressed);
        }

        #endregion

        #region Concurrent Operations Test

        [Fact]
        public async Task AsyncOperations_MultipleAlgorithmsConcurrently_AllSucceed()
        {
            var algorithms = new[] { 
                CompressionAlgorithm.Lzma, 
                CompressionAlgorithm.Lzma2,
                CompressionAlgorithm.FastLzma2,
                CompressionAlgorithm.Lz4, 
                CompressionAlgorithm.ZStd,
                CompressionAlgorithm.DeflateNg,
                CompressionAlgorithm.GZipNg,
                CompressionAlgorithm.ZLibNg,
                CompressionAlgorithm.Brotli,
                CompressionAlgorithm.Copy
            };
            var tasks = new List<Task<bool>>();

            foreach (var algorithm in algorithms)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var level = CompressionLevel.Optimal;
                    byte[]? properties = null;
                    
                    // Compress
                    byte[] compressed;
                    using (var output = new MemoryStream())
                    {
                        using (var stream = createCompressionStream(algorithm, output, CompressionMode.Compress, level))
                        {
                            await stream.WriteAsync(_Data64KiB, 0, _Data64KiB.Length);
                            if (stream is CompressionStream cs)
                                properties = cs.Properties;
                        }
                        compressed = output.ToArray();
                    }

                    // Decompress
                    byte[] decompressed;
                    using (var input = new MemoryStream(compressed))
                    using (var stream = createCompressionStream(algorithm, input, CompressionMode.Decompress, level, properties!))
                    using (var output = new MemoryStream())
                    {
                        await stream.CopyToAsync(output);
                        decompressed = output.ToArray();
                    }

                    return decompressed.AsSpan().SequenceEqual(_Data64KiB);
                }));
            }

            var results = await Task.WhenAll(tasks);
            Assert.All(results, result => Assert.True(result, "All concurrent operations should succeed"));
        }

        #endregion
    }
}

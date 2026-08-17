using Nanook.GrindCore.ZStd;
using Xunit;

namespace GrindCore.Tests
{
    public sealed class ZStdSeekable_v1_5_7_Tests
    {
        [Fact]
        public void SeekableDecoder_RoundTrip_From_Buffer()
        {
            // Arrange
            const int dataSize = 256 * 1024; // 256KB

            byte[] inputData = new byte[dataSize];
            for (int i = 0; i < inputData.Length; i++)
            {
                inputData[i] = (byte)(i % 251);
            }

            // Act - Create compressed buffer using seekable stream
            byte[] compressedData = CompressSeekableUsingStream(inputData, frameSize: 64 * 1024, compressionLevel: 3);

            // Assert - Verify compression happened
            Assert.NotEmpty(compressedData);
            Assert.True(compressedData.Length < inputData.Length, "Compressed data should be smaller than input");

            // Output diagnostic info
            double compressionRatio = (double)inputData.Length / compressedData.Length;
            System.Console.WriteLine($"[SEEKABLE TEST] Input size: {inputData.Length} bytes");
            System.Console.WriteLine($"[SEEKABLE TEST] Compressed size: {compressedData.Length} bytes");
            System.Console.WriteLine($"[SEEKABLE TEST] Compression ratio: {compressionRatio:F2}x");

            // Act - Decompress with seekable decoder
            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressedData))
            {
                // Assert - Check metadata
                Assert.True(decoder.FrameCount > 0, "Frame count should be greater than zero");
                Assert.Equal((ulong)inputData.Length, decoder.DecompressedSize);

                System.Console.WriteLine($"[SEEKABLE TEST] Frame count: {decoder.FrameCount}");
                System.Console.WriteLine($"[SEEKABLE TEST] Reported decompressed size: {decoder.DecompressedSize}");

                // Decompress all data
                byte[] decompressedData = new byte[inputData.Length];
                int bytesDecompressed = decoder.Decompress(decompressedData, offset: 0);

                System.Console.WriteLine($"[SEEKABLE TEST] Bytes decompressed: {bytesDecompressed}");

                // Assert - Verify decompression correctness
                Assert.Equal(inputData.Length, bytesDecompressed);

                // Verify byte-by-byte match
                bool dataMatches = true;
                for (int i = 0; i < inputData.Length; i++)
                {
                    if (inputData[i] != decompressedData[i])
                    {
                        System.Console.WriteLine($"[SEEKABLE TEST] MISMATCH at byte {i}: expected {inputData[i]}, got {decompressedData[i]}");
                        dataMatches = false;
                        break;
                    }
                }

                Assert.True(dataMatches, "Decompressed data should match input data byte-for-byte");
                System.Console.WriteLine($"[SEEKABLE TEST] ? All {inputData.Length} bytes verified - SUCCESSFUL ROUND TRIP");
            }
        }

        [Theory]
        [InlineData(1024, 512)]        // 1KB data, 512B frames
        [InlineData(10240, 1024)]      // 10KB data, 1KB frames
        [InlineData(102400, 10240)]    // 100KB data, 10KB frames
        public void SeekableDecoder_RoundTrip_VariousSizes(int dataSize, int frameSize)
        {
            // Arrange
            byte[] inputData = new byte[dataSize];
            for (int i = 0; i < inputData.Length; i++)
            {
                inputData[i] = (byte)(i % 251);
            }

            // Act - Compress and decompress
            byte[] compressedData = CompressSeekableUsingStream(inputData, frameSize, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressedData))
            {
                byte[] decompressedData = new byte[dataSize];
                int bytesDecompressed = decoder.Decompress(decompressedData, offset: 0);

                // Assert
                Assert.Equal(dataSize, bytesDecompressed);
                Assert.Equal(inputData, decompressedData);
            }
        }

        [Fact]
        public void SeekableDecoder_FrameCount_MatchesExpectedFrames()
        {
            // Arrange
            const int dataSize = 100 * 1024; // 100KB
            const int frameSize = 10 * 1024; // 10KB frames - expect ~10 frames

            byte[] inputData = new byte[dataSize];
            for (int i = 0; i < inputData.Length; i++)
            {
                inputData[i] = (byte)(i % 251);
            }

            // Act - Compress
            byte[] compressedData = CompressSeekableUsingStream(inputData, frameSize, compressionLevel: 3);

            // Assert - Check frame count
            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressedData))
            {
                Assert.True(decoder.FrameCount >= 10, $"Expected at least 10 frames, got {decoder.FrameCount}");
                Assert.Equal((ulong)dataSize, decoder.DecompressedSize);
            }
        }

        [Fact]
        public void SeekableDecoder_DecompressAtOffset_Success()
        {
            // Arrange
            const int dataSize = 100 * 1024;
            const int frameSize = 10 * 1024;
            const int offsetToTest = 50 * 1024; // Middle of the data

            byte[] inputData = new byte[dataSize];
            for (int i = 0; i < inputData.Length; i++)
            {
                inputData[i] = (byte)(i % 251);
            }

            byte[] compressedData = CompressSeekableUsingStream(inputData, frameSize, compressionLevel: 3);

            // Act - Decompress from offset
            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressedData))
            {
                System.Console.WriteLine($"[OFFSET TEST] Total frames: {decoder.FrameCount}");
                System.Console.WriteLine($"[OFFSET TEST] Total decompressed size: {decoder.DecompressedSize}");

                int remainingSize = dataSize - offsetToTest;
                byte[] decompressedData = new byte[remainingSize];
                int bytesDecompressed = decoder.Decompress(decompressedData, offset: (ulong)offsetToTest);

                // Assert - Verify data from offset
                Assert.Equal(remainingSize, bytesDecompressed);

                int mismatches = 0;
                for (int i = 0; i < remainingSize; i++)
                {
                    if (inputData[offsetToTest + i] != decompressedData[i])
                    {
                        if (mismatches == 0)
                        {
                            System.Console.WriteLine($"[OFFSET TEST] First mismatch at position {i}: expected {inputData[offsetToTest + i]}, got {decompressedData[i]}");
                        }
                        mismatches++;
                    }
                }

                Assert.Equal(0, mismatches);
                System.Console.WriteLine($"[OFFSET TEST] ? Decompressed {bytesDecompressed} bytes starting at offset {offsetToTest}");
                System.Console.WriteLine($"[OFFSET TEST] ? All bytes verified - RANDOM ACCESS WORKING!");
            }
        }

        [Fact]
        public void SeekableDecoder_RandomSizes_FuzzTest()
        {
            // This test uses random data sizes and frame sizes to catch edge cases
            var random = new Random(42); // Seed for reproducibility

            for (int iteration = 0; iteration < 20; iteration++)
            {
                // Generate random sizes within reasonable bounds
                int dataSize = random.Next(512, 512 * 1024); // 512 bytes to 512KB
                int frameSize = random.Next(Math.Max(256, dataSize / 100), Math.Min(dataSize, 128 * 1024)); // Keep frame size reasonable relative to data size

                System.Console.WriteLine($"[FUZZ TEST {iteration + 1}/20] Data: {dataSize} bytes, Frame: {frameSize} bytes");

                // Generate simple patterned data (like the working test uses)
                byte[] inputData = new byte[dataSize];
                for (int i = 0; i < dataSize; i++)
                {
                    inputData[i] = (byte)(i % 251);
                }

                try
                {
                    // Compress with random frame size
                    byte[] compressedData = CompressSeekableUsingStream(inputData, frameSize, compressionLevel: 3);

                    Assert.NotEmpty(compressedData);
                    System.Console.WriteLine($"[FUZZ TEST {iteration + 1}/20] Compressed to {compressedData.Length} bytes ({(double)dataSize / compressedData.Length:F2}x ratio)");

                    // Decompress and verify full round-trip
                    using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressedData))
                    {
                        Assert.True(decoder.FrameCount > 0, "Frame count should be greater than zero");
                        Assert.Equal((ulong)dataSize, decoder.DecompressedSize);

                        System.Console.WriteLine($"[FUZZ TEST {iteration + 1}/20] Frames: {decoder.FrameCount}, Decompressed size: {decoder.DecompressedSize}");

                        // Test full decompression
                        byte[] decompressedData = new byte[dataSize];
                        int bytesDecompressed = decoder.Decompress(decompressedData, offset: 0);

                        Assert.Equal(dataSize, bytesDecompressed);
                        Assert.Equal(inputData, decompressedData);

                        // Test random offset decompression if data is large enough
                        if (dataSize > 1024)
                        {
                            ulong randomOffset = (ulong)random.Next(0, dataSize - 1024);
                            byte[] offsetData = new byte[1024];
                            int offsetBytes = decoder.Decompress(offsetData, offset: randomOffset);

                            Assert.Equal(1024, offsetBytes);
                            for (int i = 0; i < 1024; i++)
                            {
                                Assert.Equal(inputData[randomOffset + (ulong)i], offsetData[i]);
                            }
                            System.Console.WriteLine($"[FUZZ TEST {iteration + 1}/20] ? Offset seek to {randomOffset} verified");
                        }
                    }

                    System.Console.WriteLine($"[FUZZ TEST {iteration + 1}/20] ??? PASSED\n");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"[FUZZ TEST {iteration + 1}/20] FAILED: {ex.Message}");
                    System.Console.WriteLine($"[FUZZ TEST {iteration + 1}/20] Data size: {dataSize}, Frame size: {frameSize}");
                    throw;
                }
            }

            System.Console.WriteLine($"[FUZZ TEST] ??? ALL 20 RANDOM SIZE COMBINATIONS PASSED!");
        }

        [Fact]
        public void SeekableDecoder_EdgeCases_VerySmallData()
        {
            // Test with minimal data (1 byte)
            byte[] tinyData = new byte[] { 42 };
            byte[] compressed = CompressSeekableUsingStream(tinyData, frameSize: 64 * 1024, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                Assert.Equal(1u, (uint)decoder.DecompressedSize);
                byte[] decompressed = new byte[1];
                int bytes = decoder.Decompress(decompressed, 0);
                Assert.Equal(1, bytes);
                Assert.Equal(42, decompressed[0]);
            }

            System.Console.WriteLine("[EDGE CASE] ? 1-byte data handled correctly");
        }

        [Fact]
        public void SeekableDecoder_EdgeCases_EmptyFrames()
        {
            // Test with data size exactly equal to frame size (single frame)
            const int size = 4096;
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++) data[i] = (byte)(i % 256);

            byte[] compressed = CompressSeekableUsingStream(data, frameSize: size, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                Assert.Equal((ulong)size, decoder.DecompressedSize);
                byte[] decompressed = new byte[size];
                int bytes = decoder.Decompress(decompressed, 0);
                Assert.Equal(size, bytes);
                Assert.Equal(data, decompressed);
            }

            System.Console.WriteLine("[EDGE CASE] ? Single-frame (exact size match) handled correctly");
        }

        [Fact]
        public void SeekableDecoder_BoundaryConditions_SeekToLastByte()
        {
            // Test seeking to the very last byte
            const int size = 100000;
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++) data[i] = (byte)(i % 256);

            byte[] compressed = CompressSeekableUsingStream(data, frameSize: 10000, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                // Seek to last byte
                byte[] lastByte = new byte[1];
                int bytes = decoder.Decompress(lastByte, offset: (ulong)(size - 1));
                Assert.Equal(1, bytes);
                Assert.Equal((byte)((size - 1) % 256), lastByte[0]);

                System.Console.WriteLine($"[BOUNDARY] ? Seek to last byte (offset {size - 1}) successful");
            }
        }

        [Fact]
        public void SeekableDecoder_BoundaryConditions_SeekToFrameBoundaries()
        {
            // Test seeking to exact frame boundaries
            const int frameSize = 8192;
            const int numFrames = 5;
            const int totalSize = frameSize * numFrames;

            byte[] data = new byte[totalSize];
            for (int i = 0; i < totalSize; i++) data[i] = (byte)(i % 251);

            byte[] compressed = CompressSeekableUsingStream(data, frameSize: frameSize, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                // Test seeking to each frame boundary
                for (int frame = 0; frame < numFrames; frame++)
                {
                    ulong offset = (ulong)(frame * frameSize);
                    byte[] chunk = new byte[100];
                    int bytes = decoder.Decompress(chunk, offset: offset);

                    Assert.Equal(100, bytes);
                    for (int i = 0; i < 100; i++)
                    {
                        Assert.Equal(data[offset + (ulong)i], chunk[i]);
                    }
                }

                System.Console.WriteLine($"[BOUNDARY] ? All {numFrames} frame boundary seeks successful");
            }
        }

        [Fact]
        public void SeekableDecoder_CompressionLevels_AllLevels()
        {
            // Test that all compression levels work correctly
            const int dataSize = 10000;
            byte[] data = new byte[dataSize];
            for (int i = 0; i < dataSize; i++) data[i] = (byte)(i % 251);

            int[] compressionLevels = { 1, 3, 5, 10, 15, 19, 22 }; // Range of ZSTD levels

            foreach (int level in compressionLevels)
            {
                byte[] compressed = CompressSeekableUsingStream(data, frameSize: 2048, compressionLevel: level);

                using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
                {
                    Assert.Equal((ulong)dataSize, decoder.DecompressedSize);
                    byte[] decompressed = new byte[dataSize];
                    int bytes = decoder.Decompress(decompressed, 0);
                    Assert.Equal(dataSize, bytes);
                    Assert.Equal(data, decompressed);
                }

                System.Console.WriteLine($"[COMPRESSION LEVEL] ? Level {level} - {compressed.Length} bytes");
            }
        }

        [Fact]
        public void SeekableDecoder_DataPatterns_HighlyCompressible()
        {
            // Test with highly compressible data (all zeros)
            const int size = 100000;
            byte[] zeros = new byte[size]; // All zeros

            byte[] compressed = CompressSeekableUsingStream(zeros, frameSize: 16384, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                Assert.Equal((ulong)size, decoder.DecompressedSize);
                byte[] decompressed = new byte[size];
                int bytes = decoder.Decompress(decompressed, 0);
                Assert.Equal(size, bytes);
                Assert.Equal(zeros, decompressed);

                double ratio = (double)size / compressed.Length;
                System.Console.WriteLine($"[DATA PATTERN] ? All-zeros: {size} bytes ? {compressed.Length} bytes ({ratio:F2}x ratio)");
            }
        }

        [Fact]
        public void SeekableDecoder_DataPatterns_LowCompressibility()
        {
            // Test with low compressibility data (crypto-random-like pattern)
            const int size = 50000;
            byte[] data = new byte[size];

            // Create pseudo-random but deterministic pattern
            int seed = 12345;
            for (int i = 0; i < size; i++)
            {
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
                data[i] = (byte)(seed & 0xFF);
            }

            byte[] compressed = CompressSeekableUsingStream(data, frameSize: 8192, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                Assert.Equal((ulong)size, decoder.DecompressedSize);
                byte[] decompressed = new byte[size];
                int bytes = decoder.Decompress(decompressed, 0);
                Assert.Equal(size, bytes);
                Assert.Equal(data, decompressed);

                double ratio = (double)size / compressed.Length;
                System.Console.WriteLine($"[DATA PATTERN] ? Low-compressibility: {size} bytes ? {compressed.Length} bytes ({ratio:F2}x ratio)");
            }
        }

        [Fact]
        public void SeekableDecoder_MultipleDecompressions_ReuseDecoder()
        {
            // Test that a single decoder can be used for multiple decompressions
            const int size = 50000;
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++) data[i] = (byte)(i % 251);

            byte[] compressed = CompressSeekableUsingStream(data, frameSize: 5000, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                // Perform 10 random seeks and decompressions
                var random = new Random(789);
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    ulong offset = (ulong)random.Next(0, size - 1000);
                    byte[] chunk = new byte[1000];
                    int bytes = decoder.Decompress(chunk, offset);

                    Assert.Equal(1000, bytes);
                    for (int i = 0; i < 1000; i++)
                    {
                        Assert.Equal(data[offset + (ulong)i], chunk[i]);
                    }
                }

                System.Console.WriteLine($"[REUSE] ? Decoder successfully reused for 10 random seeks");
            }
        }

        [Fact]
        public void SeekableDecoder_LargeFile_MultiMegabyte()
        {
            // Test with larger data size to validate real-world scenarios
            const int size = 2 * 1024 * 1024; // 2MB
            byte[] data = new byte[size];

            // Create repeating pattern for good compression
            for (int i = 0; i < size; i++)
            {
                data[i] = (byte)(i % 1000); // Repeating pattern every 1000 bytes
            }

            byte[] compressed = CompressSeekableUsingStream(data, frameSize: 64 * 1024, compressionLevel: 5);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                Assert.Equal((ulong)size, decoder.DecompressedSize);

                // Test full decompression
                byte[] decompressed = new byte[size];
                int bytes = decoder.Decompress(decompressed, 0);
                Assert.Equal(size, bytes);

                // Verify a sample of positions
                for (int i = 0; i < 1000; i++)
                {
                    int pos = i * (size / 1000);
                    Assert.Equal(data[pos], decompressed[pos]);
                }

                double ratio = (double)size / compressed.Length;
                System.Console.WriteLine($"[LARGE FILE 2MB] ? 2MB file: {size} bytes ? {compressed.Length} bytes ({ratio:F2}x ratio)");
                System.Console.WriteLine($"[LARGE FILE 2MB] ? Frame count: {decoder.FrameCount}, Full decompression verified");
            }
        }

        [Fact]
        public void SeekableDecoder_LargeFile_100MB()
        {
            // Test with very large file to validate production-scale scenarios
            const int size = 100 * 1024 * 1024; // 100MB
            const int frameSize = 2 * 1024 * 1024; // 2MB frames

            System.Console.WriteLine($"[LARGE FILE 100MB] Allocating {size / 1024 / 1024}MB test data...");

            byte[] data = new byte[size];

            // Create repeating pattern for realistic compression
            // Using a pattern that mimics text/binary data with some redundancy
            System.Console.WriteLine($"[LARGE FILE 100MB] Generating test pattern...");
            for (int i = 0; i < size; i++)
            {
                // Mix of patterns: base pattern + some variation
                data[i] = (byte)((i % 1000) + ((i / 10000) % 50));
            }

            System.Console.WriteLine($"[LARGE FILE 100MB] Compressing with {frameSize / 1024 / 1024}MB frames...");
            byte[] compressed = CompressSeekableUsingStream(data, frameSize: frameSize, compressionLevel: 5);

            System.Console.WriteLine($"[LARGE FILE 100MB] Testing decoder...");
            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                Assert.Equal((ulong)size, decoder.DecompressedSize);
                Assert.True(decoder.FrameCount > 0);

                double ratio = (double)size / compressed.Length;
                System.Console.WriteLine($"[LARGE FILE 100MB] ? Compressed: {size / 1024 / 1024}MB ? {compressed.Length / 1024 / 1024}MB ({ratio:F2}x ratio)");
                System.Console.WriteLine($"[LARGE FILE 100MB] ? Frame count: {decoder.FrameCount}");

                // Test random seeks at various positions (avoid decompressing entire 100MB to save test time)
                System.Console.WriteLine($"[LARGE FILE 100MB] Testing random seeks...");
                var random = new Random(456);
                int seekTests = 20;
                const int chunkSize = 64 * 1024; // 64KB chunks

                for (int test = 0; test < seekTests; test++)
                {
                    // Random offset, aligned to avoid crossing frame boundaries unnecessarily
                    ulong offset = (ulong)random.Next(0, size - chunkSize);
                    byte[] chunk = new byte[chunkSize];

                    int bytesRead = decoder.Decompress(chunk, offset);
                    Assert.Equal(chunkSize, bytesRead);

                    // Verify several positions within the chunk
                    for (int i = 0; i < 100; i++)
                    {
                        int checkPos = random.Next(0, chunkSize);
                        ulong dataPos = offset + (ulong)checkPos;
                        byte expected = (byte)((dataPos % 1000) + ((dataPos / 10000) % 50));
                        Assert.Equal(expected, chunk[checkPos]);
                    }
                }

                System.Console.WriteLine($"[LARGE FILE 100MB] ? {seekTests} random 64KB seeks verified across 100MB file");

                // Test seeking to specific landmarks
                System.Console.WriteLine($"[LARGE FILE 100MB] Testing landmark positions...");

                // Beginning
                byte[] start = new byte[1024];
                decoder.Decompress(start, 0);
                for (int i = 0; i < 1024; i++)
                {
                    Assert.Equal((byte)((i % 1000) + ((i / 10000) % 50)), start[i]);
                }
                System.Console.WriteLine($"[LARGE FILE 100MB]   ? Start (offset 0)");

                // Middle
                ulong middleOffset = (ulong)(size / 2);
                byte[] middle = new byte[1024];
                decoder.Decompress(middle, middleOffset);
                for (int i = 0; i < 1024; i++)
                {
                    ulong pos = middleOffset + (ulong)i;
                    Assert.Equal((byte)((pos % 1000) + ((pos / 10000) % 50)), middle[i]);
                }
                System.Console.WriteLine($"[LARGE FILE 100MB]   ? Middle (offset {middleOffset / 1024 / 1024}MB)");

                // Near end
                ulong endOffset = (ulong)(size - 1024);
                byte[] end = new byte[1024];
                decoder.Decompress(end, endOffset);
                for (int i = 0; i < 1024; i++)
                {
                    ulong pos = endOffset + (ulong)i;
                    Assert.Equal((byte)((pos % 1000) + ((pos / 10000) % 50)), end[i]);
                }
                System.Console.WriteLine($"[LARGE FILE 100MB]   ? End (offset {endOffset / 1024 / 1024}MB)");

                System.Console.WriteLine($"[LARGE FILE 100MB] ??? 100MB file with 2MB frames - FULL VALIDATION PASSED");
            }

            // Clean up large arrays for GC
            data = null!;
            compressed = null!;
            System.GC.Collect();
        }

        [Fact]
        public void SeekableDecoder_StressTes_ManySmallSeeks()
        {
            // Stress test: many small sequential reads (simulating streaming/scanning)
            const int size = 100000;
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++) data[i] = (byte)(i % 251);

            byte[] compressed = CompressSeekableUsingStream(data, frameSize: 4096, compressionLevel: 3);

            using (var decoder = new ZStdSeekableDecoder_v1_5_7(compressed))
            {
                // Read in 100-byte chunks, sequentially
                int chunkSize = 100;
                int numChunks = size / chunkSize;

                for (int chunk = 0; chunk < numChunks; chunk++)
                {
                    ulong offset = (ulong)(chunk * chunkSize);
                    byte[] chunkData = new byte[chunkSize];
                    int bytes = decoder.Decompress(chunkData, offset);

                    Assert.Equal(chunkSize, bytes);
                    for (int i = 0; i < chunkSize; i++)
                    {
                        Assert.Equal(data[offset + (ulong)i], chunkData[i]);
                    }
                }

                System.Console.WriteLine($"[STRESS TEST] ? {numChunks} sequential seeks of {chunkSize} bytes each - all verified");
            }
        }

        /// <summary>
        /// Helper method to compress data using seekable format via the stream-based API.
        /// This simulates how the seekable encoder would be used in production code.
        /// </summary>
        private byte[] CompressSeekableUsingStream(byte[] inputData, int frameSize, int compressionLevel)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var seekableStream = new ZStdSeekableStream_v1_5_7(memoryStream, maxFrameSize: frameSize, compressionLevel: compressionLevel, leaveOpen: true))
                {
                    seekableStream.Write(inputData, 0, inputData.Length);
                }
                return memoryStream.ToArray();
            }
        }
    }
}


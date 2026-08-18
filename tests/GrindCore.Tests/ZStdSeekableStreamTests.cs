using Nanook.GrindCore;
using Nanook.GrindCore.ZStd;
using Xunit;

namespace GrindCore.Tests
{
    /// <summary>
    /// Tests for the unified ZStdSeekableStream class covering async methods,
    /// seek/position operations, and integration with skippable frames.
    /// </summary>
    public sealed class ZStdSeekableStreamTests
    {
        #region Async Write/Flush Tests

        [Fact]
        public async Task WriteAsync_RoundTrip_Success()
        {
            // Arrange
            const int dataSize = 64 * 1024;
            byte[] inputData = GenerateTestData(dataSize);

            // Act - Compress using WriteAsync
            byte[] compressedData;
            using (var memoryStream = new MemoryStream())
            {
                using (var seekableStream = new ZStdSeekableStream(memoryStream, maxFrameSize: 16 * 1024, compressionLevel: 3, leaveOpen: true))
                {
                    await seekableStream.WriteAsync(inputData, 0, inputData.Length, CancellationToken.None);
                }
                compressedData = memoryStream.ToArray();
            }

            // Assert - Decompress and verify
            Assert.NotEmpty(compressedData);
            using (var decoder = new ZStdSeekableDecoder(compressedData))
            {
                byte[] decompressed = new byte[dataSize];
                int bytesRead = decoder.Decompress(decompressed, 0);
                Assert.Equal(dataSize, bytesRead);
                Assert.Equal(inputData, decompressed);
            }
        }

        [Fact]
        public async Task WriteAsync_MultipleChunks_Success()
        {
            // Arrange
            const int chunkSize = 8 * 1024;
            const int numChunks = 10;
            byte[] inputData = GenerateTestData(chunkSize * numChunks);

            // Act - Write in multiple async chunks
            byte[] compressedData;
            using (var memoryStream = new MemoryStream())
            {
                using (var seekableStream = new ZStdSeekableStream(memoryStream, maxFrameSize: 16 * 1024, compressionLevel: 3, leaveOpen: true))
                {
                    for (int i = 0; i < numChunks; i++)
                    {
                        await seekableStream.WriteAsync(inputData, i * chunkSize, chunkSize, CancellationToken.None);
                    }
                }
                compressedData = memoryStream.ToArray();
            }

            // Assert
            using (var decoder = new ZStdSeekableDecoder(compressedData))
            {
                Assert.Equal((ulong)(chunkSize * numChunks), decoder.DecompressedSize);
                byte[] decompressed = new byte[inputData.Length];
                int bytesRead = decoder.Decompress(decompressed, 0);
                Assert.Equal(inputData.Length, bytesRead);
                Assert.Equal(inputData, decompressed);
            }
        }

        [Fact]
        public async Task FlushAsync_FinalizesStream_Success()
        {
            // Arrange
            byte[] inputData = GenerateTestData(32 * 1024);

            // Act - Write then FlushAsync explicitly before dispose
            byte[] compressedData;
            using (var memoryStream = new MemoryStream())
            {
                var seekableStream = new ZStdSeekableStream(memoryStream, maxFrameSize: 8 * 1024, compressionLevel: 3, leaveOpen: true);
                await seekableStream.WriteAsync(inputData, 0, inputData.Length, CancellationToken.None);
                await seekableStream.FlushAsync(CancellationToken.None);
                seekableStream.Dispose(); // Should not double-finalize
                compressedData = memoryStream.ToArray();
            }

            // Assert
            using (var decoder = new ZStdSeekableDecoder(compressedData))
            {
                Assert.Equal((ulong)inputData.Length, decoder.DecompressedSize);
                byte[] decompressed = new byte[inputData.Length];
                int bytesRead = decoder.Decompress(decompressed, 0);
                Assert.Equal(inputData.Length, bytesRead);
                Assert.Equal(inputData, decompressed);
            }
        }

        [Fact]
        public async Task WriteAsync_Cancellation_ThrowsOperationCanceled()
        {
            byte[] inputData = GenerateTestData(1024);
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            using var memoryStream = new MemoryStream();
            using var seekableStream = new ZStdSeekableStream(memoryStream, maxFrameSize: 1024 * 1024, compressionLevel: 3, leaveOpen: true);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await seekableStream.WriteAsync(inputData, 0, inputData.Length, cts.Token));
        }

#if NETCOREAPP3_0_OR_GREATER
        [Fact]
        public async Task WriteAsync_Memory_RoundTrip_Success()
        {
            // Arrange
            const int dataSize = 32 * 1024;
            byte[] inputData = GenerateTestData(dataSize);
            ReadOnlyMemory<byte> memory = inputData.AsMemory();

            // Act - Compress using Memory-based WriteAsync
            byte[] compressedData;
            using (var memoryStream = new MemoryStream())
            {
                using (var seekableStream = new ZStdSeekableStream(memoryStream, maxFrameSize: 8 * 1024, compressionLevel: 3, leaveOpen: true))
                {
                    await seekableStream.WriteAsync(memory, CancellationToken.None);
                }
                compressedData = memoryStream.ToArray();
            }

            // Assert
            using (var decoder = new ZStdSeekableDecoder(compressedData))
            {
                byte[] decompressed = new byte[dataSize];
                int bytesRead = decoder.Decompress(decompressed, 0);
                Assert.Equal(dataSize, bytesRead);
                Assert.Equal(inputData, decompressed);
            }
        }

        [Fact]
        public async Task ReadAsync_Memory_Success()
        {
            // Arrange
            const int dataSize = 32 * 1024;
            byte[] inputData = GenerateTestData(dataSize);
            byte[] compressedData = CompressSeekable(inputData, 8 * 1024);

            // Act - Decompress using Memory-based ReadAsync
            using var ms = new MemoryStream(compressedData);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            byte[] decompressed = new byte[dataSize];
            Memory<byte> memory = decompressed.AsMemory();
            int bytesRead = await seekableStream.ReadAsync(memory, CancellationToken.None);

            // Assert
            Assert.Equal(dataSize, bytesRead);
            Assert.Equal(inputData, decompressed);
        }
#endif

        [Fact]
        public async Task ReadAsync_ByteArray_Success()
        {
            // Arrange
            const int dataSize = 32 * 1024;
            byte[] inputData = GenerateTestData(dataSize);
            byte[] compressedData = CompressSeekable(inputData, 8 * 1024);

            // Act - Decompress using Task-based ReadAsync
            using var ms = new MemoryStream(compressedData);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            byte[] decompressed = new byte[dataSize];
            int bytesRead = await seekableStream.ReadAsync(decompressed, 0, dataSize, CancellationToken.None);

            // Assert
            Assert.Equal(dataSize, bytesRead);
            Assert.Equal(inputData, decompressed);
        }

        #endregion

        #region Seek and Position Tests

        [Fact]
        public void Seek_Begin_SetsPosition()
        {
            // Arrange
            byte[] inputData = GenerateTestData(100000);
            byte[] compressed = CompressSeekable(inputData, 10000);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            // Act
            long newPos = seekableStream.Seek(50000, SeekOrigin.Begin);

            // Assert
            Assert.Equal(50000, newPos);
            Assert.Equal(50000, seekableStream.Position);
        }

        [Fact]
        public void Seek_Current_AdvancesPosition()
        {
            // Arrange
            byte[] inputData = GenerateTestData(100000);
            byte[] compressed = CompressSeekable(inputData, 10000);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            // Act - Read some, then seek relative
            byte[] buffer = new byte[1000];
            seekableStream.Read(buffer, 0, 1000); // position = 1000

            long newPos = seekableStream.Seek(5000, SeekOrigin.Current);

            // Assert
            Assert.Equal(6000, newPos);
            Assert.Equal(6000, seekableStream.Position);
        }

        [Fact]
        public void Seek_End_SetsPositionFromEnd()
        {
            // Arrange
            byte[] inputData = GenerateTestData(100000);
            byte[] compressed = CompressSeekable(inputData, 10000);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            // Act
            long newPos = seekableStream.Seek(-1000, SeekOrigin.End);

            // Assert
            Assert.Equal(99000, newPos);
            Assert.Equal(99000, seekableStream.Position);
        }

        [Fact]
        public void Seek_ThenRead_ReturnsCorrectData()
        {
            // Arrange
            byte[] inputData = GenerateTestData(100000);
            byte[] compressed = CompressSeekable(inputData, 10000);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            // Act - Seek to middle, then read
            seekableStream.Seek(50000, SeekOrigin.Begin);
            byte[] buffer = new byte[1000];
            int bytesRead = seekableStream.Read(buffer, 0, 1000);

            // Assert
            Assert.Equal(1000, bytesRead);
            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(inputData[50000 + i], buffer[i]);
            }
        }

        [Fact]
        public void Seek_MultipleJumps_ReturnsCorrectData()
        {
            // Arrange
            byte[] inputData = GenerateTestData(100000);
            byte[] compressed = CompressSeekable(inputData, 10000);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            // Act - Jump around and verify data at each position
            int[] offsets = { 0, 99000, 50000, 25000, 75000 };
            foreach (int offset in offsets)
            {
                seekableStream.Position = offset;
                byte[] buffer = new byte[100];
                int bytesRead = seekableStream.Read(buffer, 0, 100);
                Assert.Equal(100, bytesRead);
                for (int i = 0; i < 100; i++)
                {
                    Assert.Equal(inputData[offset + i], buffer[i]);
                }
            }
        }

        [Fact]
        public void Seek_BeforeBeginning_Throws()
        {
            byte[] inputData = GenerateTestData(10000);
            byte[] compressed = CompressSeekable(inputData, 5000);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            Assert.Throws<ArgumentOutOfRangeException>(() => seekableStream.Seek(-1, SeekOrigin.Begin));
        }

        [Fact]
        public void Seek_BeyondEnd_Throws()
        {
            byte[] inputData = GenerateTestData(10000);
            byte[] compressed = CompressSeekable(inputData, 5000);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            Assert.Throws<ArgumentOutOfRangeException>(() => seekableStream.Seek(10001, SeekOrigin.Begin));
        }

        [Fact]
        public void Seek_NotSupportedInCompressMode()
        {
            using var ms = new MemoryStream();
            using var seekableStream = new ZStdSeekableStream(ms, maxFrameSize: 1024 * 1024, compressionLevel: 3, leaveOpen: true);

            Assert.Throws<NotSupportedException>(() => seekableStream.Seek(0, SeekOrigin.Begin));
        }

        [Fact]
        public void Position_Set_SeeksToPosition()
        {
            byte[] inputData = GenerateTestData(50000);
            byte[] compressed = CompressSeekable(inputData, 10000);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            seekableStream.Position = 25000;
            Assert.Equal(25000, seekableStream.Position);

            byte[] buffer = new byte[100];
            seekableStream.Read(buffer, 0, 100);
            for (int i = 0; i < 100; i++)
                Assert.Equal(inputData[25000 + i], buffer[i]);
        }

        [Fact]
        public void Length_ReturnsDecompressedSize()
        {
            byte[] inputData = GenerateTestData(12345);
            byte[] compressed = CompressSeekable(inputData, 4096);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            Assert.Equal(12345, seekableStream.Length);
        }

        [Fact]
        public void CanSeek_TrueInDecompressMode()
        {
            byte[] inputData = GenerateTestData(1024);
            byte[] compressed = CompressSeekable(inputData, 512);

            using var ms = new MemoryStream(compressed);
            using var seekableStream = new ZStdSeekableStream(ms, leaveOpen: false, version: null);

            Assert.True(seekableStream.CanSeek);
            Assert.True(seekableStream.CanRead);
            Assert.False(seekableStream.CanWrite);
        }

        [Fact]
        public void CanSeek_FalseInCompressMode()
        {
            using var ms = new MemoryStream();
            using var seekableStream = new ZStdSeekableStream(ms, maxFrameSize: 1024 * 1024, compressionLevel: 3, leaveOpen: true);

            Assert.False(seekableStream.CanSeek);
            Assert.False(seekableStream.CanRead);
            Assert.True(seekableStream.CanWrite);
        }

        #endregion

        #region Skippable Frame Integration Tests

        [Fact]
        public void SkippableFrame_WriteAndRead_RoundTrip()
        {
            // Arrange
            byte[] userData = System.Text.Encoding.UTF8.GetBytes("metadata: seekable archive v1.0");
            byte[] frameBuffer = new byte[1024];
            uint variant = 7;

            // Act - Write a skippable frame
            int frameSize = ZStdSkippable.WriteSkippableFrame(frameBuffer, userData, variant);

            // Assert - Read it back
            byte[] readBuffer = new byte[userData.Length];
            int bytesRead = ZStdSkippable.ReadSkippableFrame(readBuffer, frameBuffer, out uint readVariant);

            Assert.Equal(userData.Length, bytesRead);
            Assert.Equal(variant, readVariant);
            Assert.Equal(userData, readBuffer);
        }

        [Fact]
        public void SkippableFrame_IsSkippableFrame_Detects()
        {
            // Arrange
            byte[] userData = new byte[] { 1, 2, 3, 4 };
            byte[] frameBuffer = new byte[64];

            ZStdSkippable.WriteSkippableFrame(frameBuffer, userData, 0);

            // Act & Assert
            Assert.True(ZStdSkippable.IsSkippableFrame(frameBuffer));
        }

        [Fact]
        public void SkippableFrame_RegularData_NotSkippable()
        {
            byte[] regularData = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04 };
            Assert.False(ZStdSkippable.IsSkippableFrame(regularData));
        }

        [Fact]
        public void SkippableFrame_AllVariants_Work()
        {
            byte[] data = new byte[] { 42, 43, 44 };
            byte[] frameBuffer = new byte[64];

            for (uint variant = 0; variant <= 15; variant++)
            {
                int written = ZStdSkippable.WriteSkippableFrame(frameBuffer, data, variant);
                Assert.True(written > 0);

                byte[] readBuffer = new byte[data.Length];
                int bytesRead = ZStdSkippable.ReadSkippableFrame(readBuffer, frameBuffer, out uint readVariant);

                Assert.Equal(data.Length, bytesRead);
                Assert.Equal(variant, readVariant);
                Assert.Equal(data, readBuffer);
            }
        }

        [Fact]
        public void SkippableFrame_V152_WriteAndRead_RoundTrip()
        {
            byte[] userData = System.Text.Encoding.UTF8.GetBytes("v1.5.2 skippable test");
            byte[] frameBuffer = new byte[1024];
            uint variant = 3;
            var v152 = CompressionVersion.ZStd(ZStdVersion.v1_5_2);

            int frameSize = ZStdSkippable.WriteSkippableFrame(frameBuffer, userData, variant, v152);
            Assert.True(frameSize > 0);

            byte[] readBuffer = new byte[userData.Length];
            int bytesRead = ZStdSkippable.ReadSkippableFrame(readBuffer, frameBuffer, out uint readVariant, v152);

            Assert.Equal(userData.Length, bytesRead);
            Assert.Equal(variant, readVariant);
            Assert.Equal(userData, readBuffer);
        }

        [Fact]
        public void SkippableFrame_InvalidVariant_Throws()
        {
            byte[] data = new byte[] { 1, 2, 3 };
            byte[] buffer = new byte[64];

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ZStdSkippable.WriteSkippableFrame(buffer, data, 16));
        }

        #endregion

        #region ZStdConstants Tests

        [Fact]
        public void Constants_GetSkippableMagicNumber_ReturnsCorrectValues()
        {
            Assert.Equal(0x184D2A50u, ZStdConstants.GetSkippableMagicNumber(0));
            Assert.Equal(0x184D2A5Fu, ZStdConstants.GetSkippableMagicNumber(15));
        }

        [Fact]
        public void Constants_IsSkippableFrame_DetectsCorrectly()
        {
            Assert.True(ZStdConstants.IsSkippableFrame(0x184D2A50));
            Assert.True(ZStdConstants.IsSkippableFrame(0x184D2A5F));
            Assert.False(ZStdConstants.IsSkippableFrame(0xFD2FB528)); // Standard frame
            Assert.False(ZStdConstants.IsSkippableFrame(0x12345678)); // Random
        }

        [Fact]
        public void Constants_IsStandardFrame_DetectsCorrectly()
        {
            Assert.True(ZStdConstants.IsStandardFrame(0xFD2FB528));
            Assert.False(ZStdConstants.IsStandardFrame(0x184D2A50)); // Skippable
        }

        [Fact]
        public void Constants_GetSkippableVariant_ReturnsCorrectValues()
        {
            Assert.Equal(0u, ZStdConstants.GetSkippableVariant(0x184D2A50));
            Assert.Equal(15u, ZStdConstants.GetSkippableVariant(0x184D2A5F));
            Assert.Null(ZStdConstants.GetSkippableVariant(0xFD2FB528)); // Not skippable
        }

        #endregion

        #region Version Selection Tests

        [Fact]
        public async Task WriteAsync_V152_RoundTrip_Success()
        {
            // Arrange
            const int dataSize = 32 * 1024;
            byte[] inputData = GenerateTestData(dataSize);

            // Act - Compress using v1.5.2
            byte[] compressedData;
            using (var memoryStream = new MemoryStream())
            {
                using (var seekableStream = new ZStdSeekableStream(memoryStream, maxFrameSize: 8 * 1024, compressionLevel: 3, leaveOpen: true, version: CompressionVersion.ZStd(ZStdVersion.v1_5_2)))
                {
                    await seekableStream.WriteAsync(inputData, 0, inputData.Length, CancellationToken.None);
                }
                compressedData = memoryStream.ToArray();
            }

            // Assert - Decompress with v1.5.2 decoder
            using (var decoder = new ZStdSeekableDecoderV1_5_2(compressedData))
            {
                byte[] decompressed = new byte[dataSize];
                int bytesRead = decoder.Decompress(decompressed, 0);
                Assert.Equal(dataSize, bytesRead);
                Assert.Equal(inputData, decompressed);
            }
        }

        [Fact]
        public void SeekableStream_DefaultVersion_UsesLatest()
        {
            byte[] inputData = GenerateTestData(10000);
            byte[] compressed = CompressSeekable(inputData, 5000);

            // Should work with the latest decoder (default)
            using var decoder = new ZStdSeekableDecoder(compressed);
            Assert.Equal((ulong)10000, decoder.DecompressedSize);
        }

        #endregion

        #region Helpers

        private static byte[] GenerateTestData(int size)
        {
            byte[] data = new byte[size];
            for (int i = 0; i < size; i++)
                data[i] = (byte)(i % 251);
            return data;
        }

        private static byte[] CompressSeekable(byte[] inputData, int frameSize, CompressionVersion? version = null)
        {
            using var memoryStream = new MemoryStream();
            using (var seekableStream = new ZStdSeekableStream(memoryStream, maxFrameSize: frameSize, compressionLevel: 3, leaveOpen: true, version: version))
            {
                seekableStream.Write(inputData, 0, inputData.Length);
            }
            return memoryStream.ToArray();
        }

        #endregion
    }
}

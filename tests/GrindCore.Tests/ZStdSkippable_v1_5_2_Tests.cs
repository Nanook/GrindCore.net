using System;
using System.Text;
using Nanook.GrindCore;
using Nanook.GrindCore.ZStd;
using Xunit;

namespace GrindCore.Tests
{
    public class ZStdSkippable_v1_5_2_Tests
    {
        private static readonly CompressionVersion _version = CompressionVersion.ZStd(ZStdVersion.v1_5_2);

        [Fact]
        public void WriteAndReadSkippableFrame_WithByteArray_Success()
        {
            // Arrange
            string testData = "Hello, v1.5.2 Skippable Frame!";
            byte[] sourceData = Encoding.UTF8.GetBytes(testData);
            byte[] destination = new byte[1024];
            uint magicVariant = 5;

            // Act - Write skippable frame
            int bytesWritten = ZStdSkippable.WriteSkippableFrame(destination, sourceData, magicVariant, _version);

            // Assert - Verify write succeeded
            Assert.True(bytesWritten > 0);
            Assert.True(bytesWritten > sourceData.Length); // Should include frame header

            // Act - Read skippable frame
            byte[] readBuffer = new byte[sourceData.Length];
            int bytesRead = ZStdSkippable.ReadSkippableFrame(readBuffer, destination, out uint readVariant, _version);

            // Assert - Verify read succeeded and data matches
            Assert.Equal(sourceData.Length, bytesRead);
            Assert.Equal(magicVariant, readVariant);
            Assert.Equal(testData, Encoding.UTF8.GetString(readBuffer, 0, bytesRead));
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        [Fact]
        public void WriteAndReadSkippableFrame_WithSpan_Success()
        {
            // Arrange
            string testData = "Span-based v1.5.2 skippable frame test!";
            byte[] sourceData = Encoding.UTF8.GetBytes(testData);
            Span<byte> destination = stackalloc byte[1024];
            uint magicVariant = 10;

            // Act - Write skippable frame
            int bytesWritten = ZStdSkippable.WriteSkippableFrame(destination, sourceData, magicVariant, _version);

            // Assert - Verify write succeeded
            Assert.True(bytesWritten > 0);
            Assert.True(bytesWritten > sourceData.Length);

            // Act - Read skippable frame
            Span<byte> readBuffer = stackalloc byte[sourceData.Length];
            int bytesRead = ZStdSkippable.ReadSkippableFrame(readBuffer, destination, out uint readVariant, _version);

            // Assert - Verify read succeeded and data matches
            Assert.Equal(sourceData.Length, bytesRead);
            Assert.Equal(magicVariant, readVariant);
            Assert.Equal(testData, Encoding.UTF8.GetString(readBuffer));
        }
#endif

        [Fact]
        public void IsSkippableFrame_WithValidFrame_ReturnsTrue()
        {
            // Arrange
            byte[] sourceData = Encoding.UTF8.GetBytes("Test data");
            byte[] frameBuffer = new byte[1024];
            uint magicVariant = 3;

            // Act - Write a skippable frame
            int bytesWritten = ZStdSkippable.WriteSkippableFrame(frameBuffer, sourceData, magicVariant, _version);

            // Trim buffer to actual frame size
            byte[] actualFrame = new byte[bytesWritten];
            Array.Copy(frameBuffer, actualFrame, bytesWritten);

            // Assert - Verify frame is detected as skippable
            Assert.True(ZStdSkippable.IsSkippableFrame(actualFrame, _version));
        }

        [Fact]
        public void IsSkippableFrame_WithNonSkippableData_ReturnsFalse()
        {
            // Arrange
            byte[] normalData = Encoding.UTF8.GetBytes("This is not a skippable frame");

            // Act & Assert
            Assert.False(ZStdSkippable.IsSkippableFrame(normalData, _version));
        }

        [Fact]
        public void WriteSkippableFrame_WithInvalidVariant_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            byte[] sourceData = new byte[10];
            byte[] destination = new byte[100];
            uint invalidVariant = 16; // Max is 15

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ZStdSkippable.WriteSkippableFrame(destination, sourceData, invalidVariant, _version));
        }

        [Fact]
        public void WriteSkippableFrame_WithNullDestination_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] sourceData = new byte[10];

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                ZStdSkippable.WriteSkippableFrame(null!, sourceData, 0, _version));
        }

        [Fact]
        public void WriteSkippableFrame_WithNullSource_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] destination = new byte[100];

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                ZStdSkippable.WriteSkippableFrame(destination, null!, 0, _version));
        }

        [Fact]
        public void ReadSkippableFrame_WithNullDestination_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] sourceFrame = new byte[100];

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                ZStdSkippable.ReadSkippableFrame(null!, sourceFrame, out _, _version));
        }

        [Fact]
        public void ReadSkippableFrame_WithNullSource_ThrowsArgumentNullException()
        {
            // Arrange
            byte[] destination = new byte[100];

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                ZStdSkippable.ReadSkippableFrame(destination, null!, out _, _version));
        }

        [Fact]
        public void IsSkippableFrame_WithNullBuffer_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                ZStdSkippable.IsSkippableFrame((byte[])null!, _version));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(5)]
        [InlineData(10)]
        [InlineData(15)]
        public void WriteAndReadSkippableFrame_AllVariants_Success(uint variant)
        {
            // Arrange
            string testData = $"Testing variant {variant}";
            byte[] sourceData = Encoding.UTF8.GetBytes(testData);
            byte[] destination = new byte[1024];

            // Act - Write and read
            int bytesWritten = ZStdSkippable.WriteSkippableFrame(destination, sourceData, variant, _version);
            byte[] readBuffer = new byte[sourceData.Length];
            int bytesRead = ZStdSkippable.ReadSkippableFrame(readBuffer, destination, out uint readVariant, _version);

            // Assert
            Assert.Equal(sourceData.Length, bytesRead);
            Assert.Equal(variant, readVariant);
            Assert.Equal(testData, Encoding.UTF8.GetString(readBuffer, 0, bytesRead));
        }

        [Fact]
        public void WriteAndReadSkippableFrame_LargeData_Success()
        {
            // Arrange - 10KB of data
            byte[] sourceData = new byte[10240];
            for (int i = 0; i < sourceData.Length; i++)
            {
                sourceData[i] = (byte)(i % 256);
            }
            byte[] destination = new byte[sourceData.Length + 100]; // Extra room for frame header
            uint magicVariant = 7;

            // Act
            int bytesWritten = ZStdSkippable.WriteSkippableFrame(destination, sourceData, magicVariant, _version);
            byte[] readBuffer = new byte[sourceData.Length];
            int bytesRead = ZStdSkippable.ReadSkippableFrame(readBuffer, destination, out uint readVariant, _version);

            // Assert
            Assert.Equal(sourceData.Length, bytesRead);
            Assert.Equal(magicVariant, readVariant);
            Assert.Equal(sourceData, readBuffer);
        }

        [Fact]
        public void WriteAndReadSkippableFrame_EmptyData_Success()
        {
            // Arrange
            byte[] sourceData = Array.Empty<byte>();
            byte[] destination = new byte[100];
            uint magicVariant = 0;

            // Act
            int bytesWritten = ZStdSkippable.WriteSkippableFrame(destination, sourceData, magicVariant, _version);
            byte[] readBuffer = new byte[0];
            int bytesRead = ZStdSkippable.ReadSkippableFrame(readBuffer, destination, out uint readVariant, _version);

            // Assert
            Assert.Equal(0, bytesRead);
            Assert.Equal(magicVariant, readVariant);
            Assert.True(bytesWritten > 0); // Still has frame header
        }
    }
}


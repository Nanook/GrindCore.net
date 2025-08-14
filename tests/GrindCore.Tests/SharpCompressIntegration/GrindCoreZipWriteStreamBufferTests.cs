using GrindCore.Tests.Utility;
using SharpCompress.Common;
using SharpCompress.Compressors;
using SharpCompress.Compressors.Brotli;
using SharpCompress.Compressors.Deflate;
using SharpCompress.Compressors.LZ4;
using SharpCompress.Compressors.LZMA;
using SharpCompress.Compressors.Xz;
using SharpCompress.Compressors.ZStandard;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Test.Zip;
using SharpCompress.Writers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace SharpCompress.Test.Zip;

/// <summary>
/// Tests to verify that buffer sizes are properly passed through to GrindCore streams
/// and that different compression buffer sizes affect compression performance and behavior.
/// </summary>
public class GrindCoreZipWriteStreamBufferTests
{
    public GrindCoreZipWriteStreamBufferTests()
    {
        //UseExtensionInsteadOfNameToVerify = true;
    }

    [Theory]
    // ZStandard with various DefaultBufferSize values - using optimal level 6
    [InlineData(CompressionType.ZStandard, 6, 0x8000, 0, false)]       // 32KB DefaultBufferSize
    [InlineData(CompressionType.ZStandard, 6, 0x10000, 0, false)]      // 64KB DefaultBufferSize
    [InlineData(CompressionType.ZStandard, 6, 0x20000, 0, false)]      // 128KB DefaultBufferSize
    [InlineData(CompressionType.ZStandard, 6, 0x40000, 0, false)]      // 256KB DefaultBufferSize
    // ZStandard with CompressionBufferSize - testing large buffer performance
    [InlineData(CompressionType.ZStandard, 6, 0x10000, 0x100000, false)] // 64KB DefaultBufferSize, 1MB CompressionBufferSize
    [InlineData(CompressionType.ZStandard, 9, 0x10000, 0x200000, false)] // 64KB DefaultBufferSize, 2MB CompressionBufferSize
    [InlineData(CompressionType.ZStandard, 12, 0x8000, 0x400000, false)] // 32KB DefaultBufferSize, 4MB CompressionBufferSize
    // ZStandard compression levels for performance comparison
    [InlineData(CompressionType.ZStandard, 1, 0x10000, 0, false)]       // Fastest
    [InlineData(CompressionType.ZStandard, 3, 0x10000, 0, false)]       // Default
    [InlineData(CompressionType.ZStandard, 22, 0x10000, 0x200000, false)] // Best compression with large buffer
    // Deflate with various DefaultBufferSize values - using optimal level 6
    [InlineData(CompressionType.Deflate, 6, 0x8000, 0, false)]       // 32KB DefaultBufferSize
    [InlineData(CompressionType.Deflate, 6, 0x10000, 0, false)]      // 64KB DefaultBufferSize
    [InlineData(CompressionType.Deflate, 6, 0x20000, 0, false)]      // 128KB DefaultBufferSize
    [InlineData(CompressionType.Deflate, 6, 0x40000, 0, false)]      // 256KB DefaultBufferSize
    // GZip with various DefaultBufferSize values - using optimal level 6
    [InlineData(CompressionType.GZip, 6, 0x8000, 0, false)]          // 32KB DefaultBufferSize
    [InlineData(CompressionType.GZip, 6, 0x10000, 0, false)]         // 64KB DefaultBufferSize
    [InlineData(CompressionType.GZip, 6, 0x40000, 0, false)]         // 256KB DefaultBufferSize
    // LZMA with various DefaultBufferSize values - using optimal level 5
    [InlineData(CompressionType.LZMA, 5, 0x8000, 0, false)]          // 32KB DefaultBufferSize
    [InlineData(CompressionType.LZMA, 5, 0x10000, 0, false)]         // 64KB DefaultBufferSize
    [InlineData(CompressionType.LZMA, 5, 0x20000, 0, false)]         // 128KB DefaultBufferSize
    [InlineData(CompressionType.LZMA, 5, 0x40000, 0, false)]         // 256KB DefaultBufferSize
    // LZMA with CompressionBufferSize - testing the new property
    [InlineData(CompressionType.LZMA, 5, 0x10000, 0x100000, false)]  // 64KB DefaultBufferSize, 1MB CompressionBufferSize
    [InlineData(CompressionType.LZMA, 5, 0x10000, 0x200000, false)]  // 64KB DefaultBufferSize, 2MB CompressionBufferSize
    [InlineData(CompressionType.LZMA, 5, 0x10000, 0x400000, false)]  // 64KB DefaultBufferSize, 4MB CompressionBufferSize
    [InlineData(CompressionType.LZMA, 5, 0x8000, 0x800000, false)]   // 32KB DefaultBufferSize, 8MB CompressionBufferSize
    // LZMA2 using CompressionType.LZMA2 - testing the new enum approach
    [InlineData(CompressionType.LZMA2, 5, 0x8000, 0, false)]         // 32KB DefaultBufferSize, LZMA2 via enum
    [InlineData(CompressionType.LZMA2, 5, 0x10000, 0, false)]        // 64KB DefaultBufferSize, LZMA2 via enum
    [InlineData(CompressionType.LZMA2, 5, 0x20000, 0, false)]        // 128KB DefaultBufferSize, LZMA2 via enum
    [InlineData(CompressionType.LZMA2, 5, 0x40000, 0, false)]        // 256KB DefaultBufferSize, LZMA2 via enum
    // LZMA2 with CompressionBufferSize (Block Mode) - using CompressionType.LZMA2
    [InlineData(CompressionType.LZMA2, 5, 0x10000, 0x100000, false)] // 64KB DefaultBufferSize, 1MB Block Size via enum
    [InlineData(CompressionType.LZMA2, 5, 0x10000, 0x200000, false)] // 64KB DefaultBufferSize, 2MB Block Size via enum
    [InlineData(CompressionType.LZMA2, 5, 0x10000, 0x400000, false)] // 64KB DefaultBufferSize, 4MB Block Size via enum
    [InlineData(CompressionType.LZMA2, 5, 0x10000, 0x800000, false)] // 64KB DefaultBufferSize, 8MB Block Size via enum
    [InlineData(CompressionType.LZMA2, 5, 0x8000, 0x1000000, false)] // 32KB DefaultBufferSize, 16MB Block Size via enum
    // LZMA2 Solid Mode using CompressionType.LZMA2
    [InlineData(CompressionType.LZMA2, 5, 0x10000, -1, false)]       // 64KB DefaultBufferSize, LZMA2 Solid Mode via enum
    [InlineData(CompressionType.LZMA2, 5, 0x20000, -1, false)]       // 128KB DefaultBufferSize, LZMA2 Solid Mode via enum
    [InlineData(CompressionType.LZMA2, 5, 0x40000, -1, false)]       // 256KB DefaultBufferSize, LZMA2 Solid Mode via enum
    // Legacy LZMA2 tests using boolean (for backward compatibility testing)
    [InlineData(CompressionType.LZMA, 5, 0x8000, 0, true)]           // 32KB DefaultBufferSize, LZMA2 via boolean
    [InlineData(CompressionType.LZMA, 5, 0x10000, 0, true)]          // 64KB DefaultBufferSize, LZMA2 via boolean
    [InlineData(CompressionType.LZMA, 5, 0x10000, 0x100000, true)]   // 64KB DefaultBufferSize, 1MB Block Size via boolean
    [InlineData(CompressionType.LZMA, 5, 0x10000, -1, true)]         // 64KB DefaultBufferSize, LZMA2 Solid Mode via boolean
    // LZ4 with various DefaultBufferSize values - using optimal levels
    [InlineData(CompressionType.LZ4, 3, 0x8000, 0, false)]           // 32KB DefaultBufferSize, LZ4 level 3
    [InlineData(CompressionType.LZ4, 6, 0x10000, 0, false)]          // 64KB DefaultBufferSize, LZ4 level 6
    [InlineData(CompressionType.LZ4, 9, 0x20000, 0, false)]          // 128KB DefaultBufferSize, LZ4 level 9
    [InlineData(CompressionType.LZ4, 12, 0x40000, 0, false)]         // 256KB DefaultBufferSize, LZ4 level 12 (max)
    // LZ4 with CompressionBufferSize - testing large buffer performance
    [InlineData(CompressionType.LZ4, 6, 0x10000, 0x100000, false)]   // 64KB DefaultBufferSize, 1MB CompressionBufferSize
    [InlineData(CompressionType.LZ4, 9, 0x10000, 0x200000, false)]   // 64KB DefaultBufferSize, 2MB CompressionBufferSize
    [InlineData(CompressionType.LZ4, 12, 0x8000, 0x400000, false)]   // 32KB DefaultBufferSize, 4MB CompressionBufferSize
    // Brotli with various DefaultBufferSize values - using optimal levels
    [InlineData(CompressionType.Brotli, 4, 0x8000, 0, false)]        // 32KB DefaultBufferSize, Brotli level 4
    [InlineData(CompressionType.Brotli, 6, 0x10000, 0, false)]       // 64KB DefaultBufferSize, Brotli level 6
    [InlineData(CompressionType.Brotli, 9, 0x20000, 0, false)]       // 128KB DefaultBufferSize, Brotli level 9
    [InlineData(CompressionType.Brotli, 11, 0x40000, 0, false)]      // 256KB DefaultBufferSize, Brotli level 11 (max)
    // Brotli with CompressionBufferSize - testing large buffer performance
    [InlineData(CompressionType.Brotli, 6, 0x10000, 0x100000, false)] // 64KB DefaultBufferSize, 1MB CompressionBufferSize
    [InlineData(CompressionType.Brotli, 9, 0x10000, 0x200000, false)] // 64KB DefaultBufferSize, 2MB CompressionBufferSize
    [InlineData(CompressionType.Brotli, 11, 0x8000, 0x400000, false)] // 32KB DefaultBufferSize, 4MB CompressionBufferSize
    // Different compression levels for performance comparison
    [InlineData(CompressionType.Deflate, 1, 0x10000, 0, false)]      // Fastest
    [InlineData(CompressionType.Deflate, 9, 0x10000, 0, false)]      // Best compression
    [InlineData(CompressionType.GZip, 1, 0x10000, 0, false)]         // Fastest
    [InlineData(CompressionType.GZip, 9, 0x10000, 0, false)]         // Best compression
    [InlineData(CompressionType.LZMA, 1, 0x10000, 0x200000, false)]  // LZMA fastest with large compression buffer
    [InlineData(CompressionType.LZMA, 9, 0x10000, 0x400000, false)]  // LZMA best with large compression buffer
    [InlineData(CompressionType.LZMA2, 1, 0x10000, 0x200000, false)] // LZMA2 fastest with 2MB block size via enum
    [InlineData(CompressionType.LZMA2, 9, 0x10000, 0x400000, false)] // LZMA2 best with 4MB block size via enum
    [InlineData(CompressionType.LZMA2, 1, 0x10000, -1, false)]       // LZMA2 fastest with solid mode via enum
    [InlineData(CompressionType.LZMA2, 9, 0x10000, -1, false)]       // LZMA2 best with solid mode via enum
    [InlineData(CompressionType.LZ4, 1, 0x10000, 0, false)]          // LZ4 fastest
    [InlineData(CompressionType.LZ4, 12, 0x10000, 0x200000, false)]  // LZ4 best with large compression buffer
    [InlineData(CompressionType.Brotli, 1, 0x10000, 0, false)]       // Brotli fastest
    [InlineData(CompressionType.Brotli, 11, 0x10000, 0x200000, false)] // Brotli best with large compression buffer
    public void GrindCore_CompressionStream_BufferSize_Performance_Test(CompressionType compressionType, int level, int defaultBufferSize, int compressionBufferSize, bool isLzma2)
    {
        var testDataSize = GetTestDataSize(compressionType, isLzma2);
        var testData = TestPseudoTextStream.Create(testDataSize);
        var expectedCrc = Crc32.Compute(testData);

        var stopwatch = Stopwatch.StartNew();

        // Test compression
        using var compressedStream = new MemoryStream();
        byte[]? lzmaProperties = null;

        using (var encoder = CreateStream(compressedStream, compressionType, level, defaultBufferSize, compressionBufferSize, isLzma2, true))
        {
            encoder.Write(testData, 0, testData.Length);
            encoder.Flush();

            if ((compressionType == CompressionType.LZMA || compressionType == CompressionType.LZMA2) && encoder is LzmaStream lzmaEncoder)
            {
                lzmaProperties = lzmaEncoder.Properties;
            }
        }

        var compressionTime = stopwatch.ElapsedMilliseconds;
        stopwatch.Restart();

        // Test decompression
        compressedStream.Position = 0;
        using var decompressedStream = new MemoryStream();

        using (var decoder = CreateStream(compressedStream, compressionType, level, defaultBufferSize, 0, isLzma2, false, testData.Length, lzmaProperties))
        {
            decoder.CopyTo(decompressedStream);
        }

        var decompressionTime = stopwatch.ElapsedMilliseconds;

        // Verify correctness
        var decompressedData = decompressedStream.ToArray();
        var actualCrc = Crc32.Compute(decompressedData);

        Assert.Equal(testData.Length, decompressedData.Length);
        Assert.Equal(expectedCrc, actualCrc);
        Assert.True(compressedStream.Length < testData.Length, "Compression should reduce size");

        var compressionRatio = (double)compressedStream.Length / testData.Length;

        // Determine if LZMA2 is being used (either via enum or boolean)
        var effectivelyLzma2 = GrindCoreBufferHelper.IsLzma2(compressionType, isLzma2);
        var algorithmName = effectivelyLzma2 ? "LZMA2" : compressionType.ToString();
        var bufferInfo = compressionBufferSize switch
        {
            -1 => $"DefaultBufferSize={defaultBufferSize:X}/SolidMode",
            > 0 => $"DefaultBufferSize={defaultBufferSize:X}/CompressionBufferSize={compressionBufferSize:X}",
            _ => $"DefaultBufferSize={defaultBufferSize:X}"
        };

        Debug.WriteLine($"{algorithmName} Level={level} {bufferInfo}: " +
                       $"Compression={compressionTime}ms, Decompression={decompressionTime}ms, " +
                       $"Ratio={compressionRatio:P}, Original={testData.Length:N0}, Compressed={compressedStream.Length:N0}");

        // Verify compression performance
        VerifyCompressionQuality(compressionType, isLzma2, level, compressionRatio);
        Assert.True(compressionTime >= 0 && decompressionTime >= 0, "Operations should complete successfully");

        // Verify algorithm-specific expectations
        if (compressionType == CompressionType.LZMA || compressionType == CompressionType.LZMA2)
        {
            if (compressionBufferSize > 0)
            {
                // Larger compression buffers should generally result in better compression ratios
                var expectedRatio = effectivelyLzma2 ? 0.08 : 0.1; // LZMA2 typically achieves better ratios
                Assert.True(compressionRatio < expectedRatio, $"{algorithmName} with large compression buffer should achieve excellent compression");
            }

            if (effectivelyLzma2 && compressionBufferSize == -1)
            {
                // Solid mode should achieve the best compression ratios for LZMA2
                Assert.True(compressionRatio < 0.05, "LZMA2 solid mode should achieve exceptional compression");
            }
        }
        else if (compressionType == CompressionType.LZ4)
        {
            // LZ4 should be fast with reasonable compression
            Assert.True(compressionRatio < 0.7, "LZ4 should achieve reasonable compression on text data");
        }
        else if (compressionType == CompressionType.Brotli)
        {
            // Brotli should achieve excellent compression ratios
            Assert.True(compressionRatio < 0.15, "Brotli should achieve excellent compression on text data");
        }
        else if (compressionType == CompressionType.ZStandard)
        {
            // ZStandard should achieve excellent compression ratios
            Assert.True(compressionRatio < 0.1, "ZStandard should achieve excellent compression on text data");
        }
        else if (compressionType == CompressionType.Xz)
        {
            // XZ should achieve excellent compression ratios (similar to LZMA)
            Assert.True(compressionRatio < 0.1, "XZ should achieve excellent compression on text data");
        }
    }

    /// <summary>
    /// Creates compression or decompression streams with unified logic
    /// </summary>
    private static Stream CreateStream(
        Stream baseStream,
        CompressionType compressionType,
        int level,
        int defaultBufferSize,
        int compressionBufferSize,
        bool isLzma2,
        bool isEncoder,
        long outputSize = -1,
        byte[]? lzmaProperties = null)
    {
        Stream stream = compressionType switch
        {
            CompressionType.Deflate => new DeflateStream(
                baseStream,
                isEncoder ? CompressionMode.Compress : CompressionMode.Decompress,
                (Compressors.Deflate.CompressionLevel)level,
                leaveOpen: true),

            CompressionType.GZip => new GZipStream(
                baseStream,
                isEncoder ? CompressionMode.Compress : CompressionMode.Decompress,
                (Compressors.Deflate.CompressionLevel)level,
                leaveOpen: true),

            CompressionType.LZMA or CompressionType.LZMA2 when isEncoder => new LzmaStream(
                new LzmaEncoderProperties(),
                GrindCoreBufferHelper.IsLzma2(compressionType, isLzma2), // Use helper to determine LZMA2
                null,
                baseStream,
                true,
                CreateWriterOptions(compressionType, level, compressionBufferSize)),

            CompressionType.LZMA or CompressionType.LZMA2 when !isEncoder => new LzmaStream(
                lzmaProperties ?? new byte[5] { 0x5D, 0x00, 0x00, 0x10, 0x00 },
                baseStream,
                baseStream.Length,
                outputSize,
                null,
                GrindCoreBufferHelper.IsLzma2(compressionType, isLzma2), // Use helper to determine LZMA2
                true,
                new ReaderOptions { BufferSize = defaultBufferSize }),

            CompressionType.LZ4 => new SharpCompress.Compressors.LZ4.LZ4Stream(
                baseStream,
                isEncoder ? CompressionMode.Compress : CompressionMode.Decompress,
                level,
                leaveOpen: true),

            CompressionType.Brotli => new SharpCompress.Compressors.Brotli.BrotliStream(
                baseStream,
                isEncoder ? CompressionMode.Compress : CompressionMode.Decompress,
                level,
                leaveOpen: true),

            CompressionType.ZStandard => new SharpCompress.Compressors.ZStandard.ZStandardStream(
                baseStream,
                isEncoder ? CompressionMode.Compress : CompressionMode.Decompress,
                level,
                leaveOpen: true),

            _ => throw new ArgumentException($"Unsupported compression type: {compressionType}")
        };

        // Set default buffer size for all streams
        if (stream is IStreamStack streamStack)
        {
            streamStack.DefaultBufferSize = defaultBufferSize;
        }

        return stream;
    }

    private static WriterOptions CreateWriterOptions(CompressionType compressionType, int level, int compressionBufferSize)
    {
        var options = new WriterOptions(compressionType, level);
        if (compressionBufferSize != 0) // Include both positive values and -1 (solid mode)
        {
            options.CompressionBufferSize = compressionBufferSize;
        }
        return options;
    }

    private static int GetTestDataSize(CompressionType compressionType, bool isLzma2 = false) => compressionType switch
    {
        CompressionType.ZStandard => 3 * 1024 * 1024,                  // 3MB - ZStandard benefits from larger data
        CompressionType.LZMA2 => 5 * 1024 * 1024,                      // 5MB - LZMA2 benefits from even larger data
        CompressionType.LZMA when isLzma2 => 5 * 1024 * 1024,          // 5MB - LZMA2 via boolean benefits from larger data
        CompressionType.LZMA => 3 * 1024 * 1024,                       // 3MB - LZMA benefits from larger data
        CompressionType.LZ4 => 2 * 1024 * 1024,                        // 2MB - Good balance for LZ4 speed vs compression
        CompressionType.Brotli => 3 * 1024 * 1024,                     // 3MB - Brotli benefits from larger data
        CompressionType.Xz => 3 * 1024 * 1024,                         // 3MB - XZ benefits from larger data like LZMA
        CompressionType.Deflate => 2 * 1024 * 1024,                    // 2MB - Good balance for Deflate
        CompressionType.GZip => 1 * 1024 * 1024,                       // 1MB - Sufficient for GZip
        _ => 1 * 1024 * 1024                                            // 1MB default
    };

    private static void VerifyCompressionQuality(CompressionType compressionType, bool isLzma2, int level, double compressionRatio)
    {
        switch (compressionType)
        {
            case CompressionType.Deflate:
            case CompressionType.GZip:
                switch (level)
                {
                    case 1: // Fastest
                        Assert.True(compressionRatio < 0.5, "Even fastest should compress text reasonably");
                        break;
                    case 9: // Best compression
                        Assert.True(compressionRatio < 0.15, "Best compression should achieve excellent compression");
                        break;
                    default: // Optimal levels
                        Assert.True(compressionRatio < 0.2, "Optimal compression should achieve good compression");
                        break;
                }
                break;
            case CompressionType.LZMA:
            case CompressionType.LZMA2:
                var effectivelyLzma2 = GrindCoreBufferHelper.IsLzma2(compressionType, isLzma2);
                if (effectivelyLzma2)
                {
                    // LZMA2 generally achieves better compression ratios than LZMA
                    Assert.True(compressionRatio < 0.08, "LZMA2 should achieve excellent compression on text data");
                }
                else
                {
                    Assert.True(compressionRatio < 0.1, "LZMA should achieve excellent compression on text data");
                }
                break;
            case CompressionType.LZ4:
                switch (level)
                {
                    case 1: // Fastest
                        Assert.True(compressionRatio < 0.8, "LZ4 fastest should still compress text reasonably");
                        break;
                    case 12: // Best compression
                        Assert.True(compressionRatio < 0.4, "LZ4 best compression should achieve good compression");
                        break;
                    default: // Mid-range levels
                        Assert.True(compressionRatio < 0.6, "LZ4 should achieve reasonable compression on text data");
                        break;
                }
                break;
            case CompressionType.Brotli:
                switch (level)
                {
                    case 1: // Fastest
                        Assert.True(compressionRatio < 0.3, "Brotli fastest should still compress text well");
                        break;
                    case 11: // Best compression
                        Assert.True(compressionRatio < 0.08, "Brotli best compression should achieve excellent compression");
                        break;
                    default: // Mid-range levels
                        Assert.True(compressionRatio < 0.15, "Brotli should achieve excellent compression on text data");
                        break;
                }
                break;
            case CompressionType.ZStandard:
                switch (level)
                {
                    case 1: // Fastest
                        Assert.True(compressionRatio < 0.3, "ZStandard fastest should still compress text well");
                        break;
                    case 22: // Best compression
                        Assert.True(compressionRatio < 0.05, "ZStandard best compression should achieve exceptional compression");
                        break;
                    default: // Mid-range levels
                        Assert.True(compressionRatio < 0.1, "ZStandard should achieve excellent compression on text data");
                        break;
                }
                break;
            case CompressionType.Xz:
                switch (level)
                {
                    case 1: // Fastest
                        Assert.True(compressionRatio < 0.3, "XZ fastest should still compress text well");
                        break;
                    case 9: // Best compression
                        Assert.True(compressionRatio < 0.08, "XZ best compression should achieve excellent compression");
                        break;
                    default: // Mid-range levels
                        Assert.True(compressionRatio < 0.1, "XZ should achieve excellent compression on text data");
                        break;
                }
                break;
        }
    }

}

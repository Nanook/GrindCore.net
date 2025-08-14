#nullable disable

using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Common;
using NGC = Nanook.GrindCore;

namespace SharpCompress.Compressors;

/// <summary>
/// Internal helper class for consistent buffer size handling across all GrindCore stream wrappers.
/// Provides centralized logic for the compression buffer size priority system.
/// </summary>
internal static class GrindCoreBufferHelper
{
    /// <summary>
    /// Determines if LZMA2 should be used based on CompressionType or isLzma2 boolean.
    /// </summary>
    /// <param name="compressionType">The compression type from WriterOptions.</param>
    /// <param name="isLzma2">The legacy boolean parameter.</param>
    /// <returns>True if LZMA2 should be used.</returns>
    public static bool IsLzma2(CompressionType? compressionType, bool isLzma2)
    {
        return compressionType == CompressionType.LZMA2 || isLzma2;
    }

    /// <summary>
    /// Applies buffer size configuration to GrindCore options with the priority system.
    /// Priority: WriterOptions.CompressionBufferSize -> ReaderOptions.BufferSize -> IStreamStack.DefaultBufferSize -> GrindCore default
    /// </summary>
    /// <param name="options">The GrindCore options to configure.</param>
    /// <param name="streamStack">The stream implementing IStreamStack for DefaultBufferSize fallback.</param>
    /// <param name="isEncoder">True if this is for compression, false for decompression.</param>
    /// <param name="writerOptions">Optional writer options.</param>
    /// <param name="readerOptions">Optional reader options.</param>
    /// <returns>The compression buffer size if CompressionBufferSize was used, otherwise 0.</returns>
    public static int ApplyBufferSizeOptions(
        NGC.CompressionOptions options,
        IStreamStack streamStack,
        bool isEncoder,
        WriterOptions writerOptions = null,
        ReaderOptions readerOptions = null)
    {
        int bufferSize = 0;
        int compressionBufferSize = 0;

        // Priority 1: WriterOptions.CompressionBufferSize (for encoders only) - capture for algorithm-specific handling
        if (isEncoder && (writerOptions?.CompressionBufferSize ?? 0) != 0)
        {
            compressionBufferSize = writerOptions.CompressionBufferSize;
            // Only use positive values for actual buffer size setting
            if (compressionBufferSize > 0)
            {
                bufferSize = compressionBufferSize;
            }
        }
        // Priority 2: ReaderOptions.BufferSize (for decoders)
        else if (!isEncoder && readerOptions?.BufferSize > 0)
        {
            bufferSize = readerOptions.BufferSize;
        }
        // Priority 3: IStreamStack.DefaultBufferSize
        else
        {
            var defaultBufferSize = streamStack.DefaultBufferSize;
            if (defaultBufferSize > 0)
            {
                bufferSize = defaultBufferSize;
            }
        }

        // Apply the buffer size if one was determined (only positive values)
        if (bufferSize > 0)
        {
            options.BufferSize = bufferSize;
        }

        // Return the original CompressionBufferSize value (including -1 for solid mode)
        return compressionBufferSize;
    }

    /// <summary>
    /// Applies algorithm-specific extensions for CompressionBufferSize usage.
    /// Currently supports LZMA2 block size setting and solid mode.
    /// </summary>
    /// <param name="options">The GrindCore options to extend.</param>
    /// <param name="compressionBufferSize">The compression buffer size to apply. Use -1 for solid mode.</param>
    /// <param name="compressionType">The compression type from WriterOptions.</param>
    /// <param name="isLzma2">The legacy boolean parameter.</param>
    public static void ApplyCompressionBufferSizeExtensions(
        NGC.CompressionOptions options,
        int compressionBufferSize,
        CompressionType? compressionType = null,
        bool isLzma2 = false)
    {
        if (IsLzma2(compressionType, isLzma2))
        {
            if (compressionBufferSize == -1)
            {
                // Solid mode: Set BlockSize to indicate solid compression
                options.BlockSize = -1;
            }
            else if (compressionBufferSize > 0)
            {
                // Block mode: Set BlockSize to the specified compression buffer size
                options.BlockSize = compressionBufferSize;
            }
        }
    }

    /// <summary>
    /// Overload for backward compatibility - uses only the boolean parameter.
    /// </summary>
    /// <param name="options">The GrindCore options to extend.</param>
    /// <param name="compressionBufferSize">The compression buffer size to apply. Use -1 for solid mode.</param>
    /// <param name="isLzma2">The legacy boolean parameter.</param>
    public static void ApplyCompressionBufferSizeExtensions(
        NGC.CompressionOptions options,
        int compressionBufferSize,
        bool isLzma2 = false)
    {
        ApplyCompressionBufferSizeExtensions(options, compressionBufferSize, null, isLzma2);
    }
}

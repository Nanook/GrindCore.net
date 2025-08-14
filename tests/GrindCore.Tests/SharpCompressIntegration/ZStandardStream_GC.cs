using System;
using System.IO;
using SharpCompress.Compressors;
using SharpCompress.IO;

#if GRINDCORE
using Nanook.GrindCore;
using GrindCoreZStdStream = Nanook.GrindCore.ZStd.ZStdStream;
#endif

namespace SharpCompress.Compressors.ZStandard;

/// <summary>
/// Lightweight wrapper for GrindCore ZStandard Stream that implements SharpCompress interfaces
/// </summary>
public sealed class ZStandardStream : Stream, IStreamStack
{
#if GRINDCORE
    private readonly GrindCoreZStdStream _grindCoreStream;
    private readonly bool _leaveOpen;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ZStandardStream class for compression
    /// </summary>
    /// <param name="baseStream">The stream to write compressed data to</param>
    /// <param name="compressionLevel">The compression level (1-22)</param>
    /// <param name="leaveOpen">Whether to leave the base stream open when disposing</param>
    public ZStandardStream(Stream baseStream, int compressionLevel, bool leaveOpen = false)
    {
        _grindCoreStream = new GrindCoreZStdStream(baseStream, new CompressionOptions
        {
            Type = (Nanook.GrindCore.CompressionType)compressionLevel,
            BufferSize = DefaultBufferSize > 0 ? DefaultBufferSize : 0x10000
        });
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Initializes a new instance of the ZStandardStream class for decompression
    /// </summary>
    /// <param name="baseStream">The stream to read compressed data from</param>
    /// <param name="leaveOpen">Whether to leave the base stream open when disposing</param>
    public ZStandardStream(Stream baseStream, bool leaveOpen = false)
    {
        _grindCoreStream = new GrindCoreZStdStream(baseStream, new CompressionOptions
        {
            Type = Nanook.GrindCore.CompressionType.Decompress,
            BufferSize = DefaultBufferSize > 0 ? DefaultBufferSize : 0x10000
        });
        _leaveOpen = leaveOpen;
    }

    /// <summary>
    /// Initializes a new instance of the ZStandardStream class with compression mode
    /// </summary>
    /// <param name="baseStream">The base stream</param>
    /// <param name="mode">Compression or decompression mode</param>
    /// <param name="compressionLevel">The compression level (ignored for decompression)</param>
    /// <param name="leaveOpen">Whether to leave the base stream open when disposing</param>
    public ZStandardStream(Stream baseStream, CompressionMode mode, int compressionLevel = 3, bool leaveOpen = false)
    {
        var options = new CompressionOptions
        {
            Type = mode == CompressionMode.Compress
                ? (Nanook.GrindCore.CompressionType)compressionLevel
                : Nanook.GrindCore.CompressionType.Decompress,
            BufferSize = DefaultBufferSize > 0 ? DefaultBufferSize : 0x10000
        };

        _grindCoreStream = new GrindCoreZStdStream(baseStream, options);
        _leaveOpen = leaveOpen;
    }
#else
    /// <summary>
    /// Initializes a new instance of the ZStandardStream class (fallback when GrindCore is not available)
    /// </summary>
    public ZStandardStream(Stream baseStream, CompressionMode mode, int compressionLevel = 3, bool leaveOpen = false)
    {
        throw new NotSupportedException("ZStandard compression requires GrindCore library");
    }

    public ZStandardStream(Stream baseStream, int compressionLevel, bool leaveOpen = false)
    {
        throw new NotSupportedException("ZStandard compression requires GrindCore library");
    }

    public ZStandardStream(Stream baseStream, bool leaveOpen = false)
    {
        throw new NotSupportedException("ZStandard compression requires GrindCore library");
    }
#endif

    #region IStreamStack Implementation

    public int DefaultBufferSize { get; set; } = 0x10000;

    public Stream BaseStream()
    {
#if GRINDCORE
        return _grindCoreStream?.BaseStream ?? Stream.Null;
#else
        return Stream.Null;
#endif
    }

    public int BufferSize
    {
        get => DefaultBufferSize;
        set => DefaultBufferSize = value;
    }

    public int BufferPosition { get; set; }

    public void SetPosition(long position)
    {
        // ZStandard streams typically don't support seeking, so this is a no-op
    }

#if DEBUG_STREAMS
    public long InstanceId { get; set; }
#endif

    #endregion

    #region Stream Implementation

    public override bool CanRead =>
#if GRINDCORE
        _grindCoreStream?.CanRead ?? false;
#else
        false;
#endif

    public override bool CanSeek =>
#if GRINDCORE
        _grindCoreStream?.CanSeek ?? false;
#else
        false;
#endif

    public override bool CanWrite =>
#if GRINDCORE
        _grindCoreStream?.CanWrite ?? false;
#else
        false;
#endif

    public override long Length =>
#if GRINDCORE
        _grindCoreStream?.Length ?? 0;
#else
        throw new NotSupportedException();
#endif

    public override long Position
    {
        get =>
#if GRINDCORE
            _grindCoreStream?.Position ?? 0;
#else
            throw new NotSupportedException();
#endif
        set =>
#if GRINDCORE
            _grindCoreStream.Position = value;
#else
            throw new NotSupportedException();
#endif
    }

    public override void Flush()
    {
#if GRINDCORE
        _grindCoreStream?.Flush();
#endif
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
#if GRINDCORE
        return _grindCoreStream?.Read(buffer, offset, count) ?? 0;
#else
        throw new NotSupportedException("ZStandard compression requires GrindCore library");
#endif
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
#if GRINDCORE
        return _grindCoreStream?.Seek(offset, origin) ?? 0;
#else
        throw new NotSupportedException();
#endif
    }

    public override void SetLength(long value)
    {
#if GRINDCORE
        _grindCoreStream?.SetLength(value);
#else
        throw new NotSupportedException();
#endif
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
#if GRINDCORE
        _grindCoreStream?.Write(buffer, offset, count);
#else
        throw new NotSupportedException("ZStandard compression requires GrindCore library");
#endif
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
#if GRINDCORE
            if (!_leaveOpen)
            {
                _grindCoreStream?.Dispose();
            }
#endif
            _disposed = true;
        }
        base.Dispose(disposing);
    }

    #endregion
}
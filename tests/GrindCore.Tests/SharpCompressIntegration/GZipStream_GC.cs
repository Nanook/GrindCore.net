#nullable disable

using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using NGC = Nanook.GrindCore;

namespace SharpCompress.Compressors.Deflate;

public class GZipStream : Stream, IStreamStack
{
    internal static readonly DateTime UNIX_EPOCH = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }
    Stream IStreamStack.BaseStream() => _inputStream;
    int IStreamStack.BufferSize { get => 0; set { } }
    int IStreamStack.BufferPosition { get => 0; set { } }
    void IStreamStack.SetPosition(long position) { }

    private readonly Stream _inputStream;
    private readonly bool _leaveOpen;
    private readonly bool _isEncoder;
    private readonly Nanook.GrindCore.CompressionStream _grindCoreStream;
    private readonly CompressionLevel _compressionLevel;
    private readonly Encoding _encoding;
    private bool _isDisposed;
    private bool _firstReadDone;

    // GZip-specific properties
    private string _comment;
    private string _fileName;
    private DateTime? _lastModified;

    public GZipStream(Stream stream, CompressionMode mode)
        : this(stream, mode, CompressionLevel.Default, Encoding.UTF8, false) { }

    public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level)
        : this(stream, mode, level, Encoding.UTF8, false) { }

    public GZipStream(Stream stream, CompressionMode mode, bool leaveOpen)
        : this(stream, mode, CompressionLevel.Default, Encoding.UTF8, leaveOpen) { }

    public GZipStream(Stream stream, CompressionMode mode, CompressionLevel level, bool leaveOpen)
        : this(stream, mode, level, Encoding.UTF8, leaveOpen) { }

    public GZipStream(
        Stream stream,
        CompressionMode mode,
        CompressionLevel level,
        Encoding encoding,
        bool leaveOpen = false,
        WriterOptions writerOptions = null,
        ReaderOptions readerOptions = null
    )
    {
        _inputStream = stream;
        _leaveOpen = leaveOpen;
        _isEncoder = mode == CompressionMode.Compress;
        _compressionLevel = level;
        _encoding = encoding;

        var options = new NGC.CompressionOptions()
        {
            Type = _isEncoder ? (NGC.CompressionType)level : NGC.CompressionType.Decompress,
            BufferSize = 0x10000,
            LeaveOpen = _leaveOpen
        };

        // Apply buffer size options using the helper
        GrindCoreBufferHelper.ApplyBufferSizeOptions(options, this, _isEncoder, writerOptions, readerOptions);

        _grindCoreStream = new NGC.GZip.GZipStream(stream, options);

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(GZipStream));
#endif
    }

    /// <summary>
    /// Gets or sets the comment for the GZip file.
    /// </summary>
    public string Comment
    {
        get => _comment;
        set
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GZipStream));
            }
            _comment = value;
        }
    }

    /// <summary>
    /// Gets or sets the last modified time for the GZip file.
    /// </summary>
    public DateTime? LastModified
    {
        get => _lastModified;
        set
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GZipStream));
            }
            _lastModified = value;
        }
    }

    /// <summary>
    /// Gets or sets the filename for the GZip file.
    /// </summary>
    public string FileName
    {
        get => _fileName;
        set
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(GZipStream));
            }
            _fileName = value;
            if (_fileName is null)
            {
                return;
            }
            if (_fileName.Contains('/'))
            {
                _fileName = _fileName.Replace('/', '\\');
            }
            if (_fileName.EndsWith('\\'))
            {
                throw new InvalidOperationException("Illegal filename");
            }

            if (_fileName.Contains('\\'))
            {
                // trim any leading path
                _fileName = System.IO.Path.GetFileName(_fileName);
            }
        }
    }

    /// <summary>
    /// Gets the CRC32 checksum of the data processed.
    /// </summary>
    public int Crc32 { get; private set; }

    /// <summary>
    /// Gets or sets the flush behavior. This property is not fully supported by the GrindCore wrapper.
    /// </summary>
    public virtual FlushType FlushMode { get; set; }

    /// <summary>
    /// Gets the size of the internal buffer from the GrindCore stream.
    /// Setting this property has no effect as the buffer size is managed by GrindCore.
    /// </summary>
    public int BufferSize
    {
        get => _grindCoreStream?.BufferedBytesTotal ?? 0;
        set
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException("GZipStream");
            }
        }
    }

    /// <summary>
    /// Gets the total number of bytes input so far.
    /// </summary>
    internal virtual long TotalIn => _grindCoreStream?.BasePosition ?? 0;

    /// <summary>
    /// Gets the total number of bytes output so far.
    /// </summary>
    internal virtual long TotalOut => _grindCoreStream?.PositionFullSize ?? 0;

    public override bool CanRead => !_isEncoder && _grindCoreStream?.CanRead == true;

    public override bool CanSeek => false;

    public override bool CanWrite => _isEncoder && _grindCoreStream?.CanWrite == true;

    public override void Flush()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }

        if (!_isEncoder)
            ((IStreamStack)this).Rewind((int)(_grindCoreStream.BasePosition - _grindCoreStream.Position)); //seek back to the bytes used
        else
            _grindCoreStream?.Flush();
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

#if DEBUG_STREAMS
        this.DebugDispose(typeof(GZipStream));
#endif

        if (disposing)
        {
            // Extract CRC32 if available before disposing
            if (_grindCoreStream != null)
            {
                // GrindCore should provide CRC32, but for now we'll leave it as 0
                Crc32 = 0; // TODO: Extract from GrindCore when available
            }

            _grindCoreStream?.Dispose();

            if (!_leaveOpen)
            {
                _inputStream?.Dispose();
            }
        }
        base.Dispose(disposing);
    }

    public override long Length => _grindCoreStream?.Length ?? 0;

    public override long Position
    {
        get => _grindCoreStream?.Position ?? 0;
        set => throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }

        if (_isEncoder || _grindCoreStream == null)
        {
            return 0;
        }

        var n = _grindCoreStream.Read(buffer, offset, count);

        // Extract GZip metadata on first read for decompression
        if (!_firstReadDone && n > 0)
        {
            _firstReadDone = true;
            // TODO: Extract FileName, Comment, and LastModified from GrindCore stream
            // For now, these would need to be extracted from the GrindCore implementation
            // or parsed manually from the GZip header
        }

        return n;
    }

    public override int ReadByte()
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }

        if (_isEncoder || _grindCoreStream == null)
        {
            return -1;
        }

        return _grindCoreStream.ReadByte();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }

        if (!_isEncoder || _grindCoreStream == null)
        {
            throw new NotSupportedException("Stream is not in write mode.");
        }

        // Handle GZip header emission on first write for compression
        if (!_firstReadDone && _isEncoder)
        {
            _firstReadDone = true;
            // TODO: Emit GZip header with filename, comment, and timestamp if needed
            // The GrindCore implementation should handle this automatically
        }

        _grindCoreStream.Write(buffer, offset, count);
    }

    public override void WriteByte(byte value)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException("GZipStream");
        }

        if (!_isEncoder || _grindCoreStream == null)
        {
            throw new NotSupportedException("Stream is not in write mode.");
        }

        // Handle GZip header emission on first write for compression
        if (!_firstReadDone && _isEncoder)
        {
            _firstReadDone = true;
            // TODO: Emit GZip header with filename, comment, and timestamp if needed
            // The GrindCore implementation should handle this automatically
        }

        _grindCoreStream.WriteByte(value);
    }

    public byte[] Properties { get; }
}

#nullable disable

using System;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Xml.Linq;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Common;
using SharpCompress.Compressors;

namespace SharpCompress.Compressors.LZMA;

public class LzmaStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }
    Stream IStreamStack.BaseStream() => _inputStream;
    int IStreamStack.BufferSize { get => 0; set { } }
    int IStreamStack.BufferPosition { get => 0; set { } }
    void IStreamStack.SetPosition(long position) { }

    private readonly Stream _inputStream;
    private readonly bool _isLzma2;
    private readonly bool _isEncoder;
    private readonly Nanook.GrindCore.CompressionStream _grindCoreStream;
    private bool _leaveOpen;
    private bool _isDisposed;

    public LzmaStream(byte[] properties, Stream inputStream, bool leaveOpen = false)
        : this(properties, inputStream, -1, -1, null, properties.Length < 5, leaveOpen) { }

    public LzmaStream(byte[] properties, Stream inputStream, long inputSize, bool leaveOpen = false)
        : this(properties, inputStream, inputSize, -1, null, properties.Length < 5, leaveOpen) { }

    public LzmaStream(byte[] properties, Stream inputStream, long inputSize, long outputSize, bool leaveOpen = false)
        : this(properties, inputStream, inputSize, outputSize, null, properties.Length < 5, leaveOpen) { }

    public LzmaStream(
        byte[] properties,
        Stream inputStream,
        long inputSize,
        long outputSize,
        Stream presetDictionary,
        bool isLzma2,
        bool leaveOpen = false,
        Readers.ReaderOptions readerOptions = null
    )
    {
        _inputStream = inputStream;
        _isLzma2 = isLzma2;
        _isEncoder = false;
        _leaveOpen = leaveOpen;

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(LzmaStream));
#endif

        var options = new Nanook.GrindCore.CompressionOptions()
        {
            Type = Nanook.GrindCore.CompressionType.Decompress,
            InitProperties = properties,
            LeaveOpen = _leaveOpen,
            BufferSize = 0x10000
        };

        // Apply buffer size options using the helper
        GrindCoreBufferHelper.ApplyBufferSizeOptions(options, this, false, null, readerOptions);

        if (inputSize >= 0)
        {
            options.PositionLimit = inputSize;
        }

        long length = 0;
        try
        {
            length = _inputStream.Length;
        }
        catch { }
        if (inputSize == -1 && /*_inputStream is BufferedSubStream &&*/ length != 0)
        {
            options.PositionLimit = length;
        }
        else if (outputSize >= 0)
        {
            options.PositionFullSizeLimit = outputSize;
        }

        // Create the appropriate GrindCore stream - use helper to determine LZMA2
        var shouldUseLzma2 = GrindCoreBufferHelper.IsLzma2(null, _isLzma2);
        if (shouldUseLzma2)
        {
            _grindCoreStream = new Nanook.GrindCore.Lzma.Lzma2Stream(inputStream, options);
        }
        else
        {
            _grindCoreStream = new Nanook.GrindCore.Lzma.LzmaStream(inputStream, options);
        }

        Properties = properties;
    }

    public LzmaStream(LzmaEncoderProperties properties, bool isLzma2, Stream outputStream, bool leaveOpen = false)
        : this(properties, isLzma2, null, outputStream, leaveOpen) { }

    public LzmaStream(
        LzmaEncoderProperties properties,
        bool isLzma2,
        Stream presetDictionary,
        Stream outputStream,
        bool leaveOpen = false,
        WriterOptions writerOptions = null
    )
    {
        _inputStream = outputStream;
        _isLzma2 = isLzma2;
        _isEncoder = true;
        _leaveOpen = leaveOpen;

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(LzmaStream));
#endif

        var options = new Nanook.GrindCore.CompressionOptions
        {
            Type = convertToCompressionType(properties), //Convert LzmaEncoderProperties to GrindCore compression level
            LeaveOpen = _leaveOpen
        };

        // Apply buffer size options using the helper and get compression buffer size for extensions
        var compressionBufferSize = GrindCoreBufferHelper.ApplyBufferSizeOptions(options, this, true, writerOptions, null);

        // Apply LZMA2-specific extensions (block size setting) - check both enum and boolean
        GrindCoreBufferHelper.ApplyCompressionBufferSizeExtensions(options, compressionBufferSize, writerOptions?.CompressionType, _isLzma2);

        // Create the appropriate GrindCore stream - use helper to determine LZMA2
        var shouldUseLzma2 = GrindCoreBufferHelper.IsLzma2(writerOptions?.CompressionType, _isLzma2);
        if (shouldUseLzma2)
        {
            _grindCoreStream = new Nanook.GrindCore.Lzma.Lzma2Stream(outputStream, options);
        }
        else
        {
            _grindCoreStream = new Nanook.GrindCore.Lzma.LzmaStream(outputStream, options);
        }

        Properties = _grindCoreStream.Properties;
    }

    private static Nanook.GrindCore.CompressionType convertToCompressionType(LzmaEncoderProperties properties)
    {
        // The algorithm/level is stored at index 4 in the Properties array
        if (properties.Properties != null && properties.Properties.Length > 4)
        {
            var algorithmValue = properties.Properties[4];

            // Convert to int - the algorithm value is the compression level
            if (algorithmValue is int level)
            {
                return level switch
                {
                    0 => Nanook.GrindCore.CompressionType.NoCompression,
                    1 => Nanook.GrindCore.CompressionType.Fastest,
                    2 => Nanook.GrindCore.CompressionType.Level2,
                    3 => Nanook.GrindCore.CompressionType.Level3,
                    4 => Nanook.GrindCore.CompressionType.Level4,
                    5 => Nanook.GrindCore.CompressionType.Optimal, // Default level
                    6 => Nanook.GrindCore.CompressionType.Level6,
                    7 => Nanook.GrindCore.CompressionType.Level7,
                    8 => Nanook.GrindCore.CompressionType.Level8,
                    9 => Nanook.GrindCore.CompressionType.SmallestSize,
                    _ => level <= 9 ? (Nanook.GrindCore.CompressionType)level : Nanook.GrindCore.CompressionType.SmallestSize
                };
            }
        }

        // For LZMA2 or when properties are not available, try to extract from dictionary size
        // This is a fallback approach - not as accurate but better than arbitrary mapping
        if (properties.Properties != null && properties.Properties.Length > 0)
        {
            var dictionarySize = properties.Properties[0];
            if (dictionarySize is int dictSize)
            {
                // Map dictionary size to compression level (rough approximation)
                return dictSize switch
                {
                    <= (1 << 16) => Nanook.GrindCore.CompressionType.Fastest,      // 64KB
                    <= (1 << 20) => Nanook.GrindCore.CompressionType.Optimal,      // 1MB  
                    <= (1 << 24) => Nanook.GrindCore.CompressionType.Level7,       // 16MB
                    _ => Nanook.GrindCore.CompressionType.SmallestSize              // >16MB
                };
            }
        }

        // Final fallback
        return Nanook.GrindCore.CompressionType.Optimal;
    }

    public override bool CanRead => !_isEncoder && _grindCoreStream?.CanRead == true;

    public override bool CanSeek => false;

    public override bool CanWrite => _isEncoder && _grindCoreStream?.CanWrite == true;

    public override void Flush()
    {
        if (!_isEncoder)
            ((IStreamStack)this).Rewind((int)(_grindCoreStream.BasePosition - _grindCoreStream.Position)); //seek back to the bytes used
        else
        {
            _grindCoreStream?.Flush();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            return;
        }
        _isDisposed = true;

#if DEBUG_STREAMS
        this.DebugDispose(typeof(LzmaStream));
#endif

        if (disposing)
        {
            _grindCoreStream?.Dispose();
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
        if (_isEncoder || _grindCoreStream == null)
        {
            return 0;
        }

        return _grindCoreStream.Read(buffer, offset, count);
    }

    public override int ReadByte()
    {
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
        if (!_isEncoder || _grindCoreStream == null)
        {
            throw new NotSupportedException("Stream is not in write mode.");
        }

        _grindCoreStream.Write(buffer, offset, count);
    }

    public byte[] Properties { get; }
}

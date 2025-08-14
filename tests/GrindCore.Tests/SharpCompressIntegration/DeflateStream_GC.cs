using System;
using System.IO;
using System.Text;
using SharpCompress.IO;
using SharpCompress.Readers;
using SharpCompress.Writers;
using NGC = Nanook.GrindCore;

namespace SharpCompress.Compressors.Deflate;

/// <summary>
/// A wrapper around the GrindCore deflate implementation that provides deflate compression and decompression.
/// This class adapts the GrindCore.DeflateZLib.DeflateStream to the SharpCompress stream interface.
/// Supports both traditional write-to-compress and read-from-compress modes.
/// </summary>
public class DeflateStream : Stream, IStreamStack
{
#if DEBUG_STREAMS
    long IStreamStack.InstanceId { get; set; }
#endif
    int IStreamStack.DefaultBufferSize { get; set; }
    Stream IStreamStack.BaseStream() => _baseStream;
    int IStreamStack.BufferSize { get => 0; set { } }
    int IStreamStack.BufferPosition { get => 0; set { } }
    void IStreamStack.SetPosition(long position) { }

    private readonly Stream _baseStream;
    private readonly bool _leaveOpen;
    private bool _disposed;
    private MemoryStream? _internalBuffer;
    private bool _isEncoder;
    private NGC.DeflateZLib.DeflateStream? _grindCoreStream;
    private readonly CompressionLevel _compressionLevel;

    // For read-to-compress mode
    private MemoryStream? _compressionBuffer;
    private bool _readToCompressMode;
    private bool _compressionComplete;
    private bool _modeInitialized;

    // Buffer options storage for deferred initialization
    private WriterOptions? _writerOptions;
    private ReaderOptions? _readerOptions;

    /// <summary>
    /// Initializes a new instance of the DeflateStream class using the GrindCore deflate implementation.
    /// </summary>
    /// <param name="stream">The underlying stream to read from or write to.</param>
    /// <param name="mode">The compression mode (compress or decompress).</param>
    /// <param name="level">The compression level (used only for compression mode).</param>
    /// <param name="forceEncoding">Encoding parameter (currently unused).</param>
    /// <param name="leaveOpen">true to leave the stream open after the DeflateStream object is disposed; otherwise, false.</param>
    /// <param name="writerOptions">Optional writer options for buffer size configuration when compressing.</param>
    /// <param name="readerOptions">Optional reader options for buffer size configuration when decompressing.</param>
    public DeflateStream(
        Stream stream,
        CompressionMode mode,
        CompressionLevel level = CompressionLevel.Default,
        Encoding? forceEncoding = null,
        bool leaveOpen = false,
        WriterOptions? writerOptions = null,
        ReaderOptions? readerOptions = null
    )
    {
        _baseStream = stream;
        _leaveOpen = leaveOpen;
        _isEncoder = mode == CompressionMode.Compress;
        _compressionLevel = level;
        _writerOptions = writerOptions;
        _readerOptions = readerOptions;

        // For decompression mode, initialize immediately
        if (!_isEncoder)
        {
            var options = new NGC.CompressionOptions()
            {
                Type = NGC.CompressionType.Decompress,
                BufferSize = 0x10000,
                LeaveOpen = _leaveOpen
            };

            // Apply buffer size options using the helper
            GrindCoreBufferHelper.ApplyBufferSizeOptions(options, this, false, null, _readerOptions);

            _grindCoreStream = new NGC.DeflateZLib.DeflateStream(_baseStream, options);
            _modeInitialized = true;
        }
        // For compression mode, defer initialization until first Read/Write call

#if DEBUG_STREAMS
        this.DebugConstruct(typeof(DeflateStream));
#endif
    }

    /// <summary>
    /// Gets the internal buffer from the underlying GrindCore stream.
    /// </summary>
    public MemoryStream InternalBuffer
    {
        get
        {
            EnsureModeInitialized();
            if (_internalBuffer == null)
                _internalBuffer = new MemoryStream(_grindCoreStream!.InternalBuffer);
            return _internalBuffer;
        }
    }

    /// <summary>
    /// Gets or sets the flush behavior. This property is not supported by the GrindCore wrapper.
    /// </summary>
    public virtual FlushType FlushMode
    {
        get => 0;
        set { }
    }

    /// <summary>
    /// Gets the size of the internal buffer from the GrindCore stream.
    /// Setting this property has no effect as the buffer size is managed by GrindCore.
    /// </summary>
    public int BufferSize
    {
        get => (int)this.InternalBuffer.Length;
        set { }
    }

    /// <summary>
    /// Gets a value indicating whether the stream supports reading.
    /// In compression mode, supports reading compressed data from an uncompressed input stream.
    /// In decompression mode, supports reading decompressed data from a compressed input stream.
    /// </summary>
    /// <remarks>
    /// The return value depends on whether the underlying GrindCore stream supports reading.
    /// </remarks>
    public override bool CanRead
    {
        get
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("DeflateStream");
            }

            if (!_modeInitialized)
            {
                // In compression mode, we can potentially read (compress-while-reading)
                return _isEncoder || _baseStream.CanRead;
            }

            return _readToCompressMode || _grindCoreStream!.CanRead;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the stream supports seeking.
    /// </summary>
    /// <remarks>
    /// Always returns false as deflate streams do not support seeking.
    /// </remarks>
    public override bool CanSeek => false;

    /// <summary>
    /// Gets a value indicating whether the stream supports writing.
    /// In compression mode, supports writing uncompressed data to produce compressed output.
    /// In decompression mode, supports writing compressed data to produce decompressed output.
    /// </summary>
    /// <remarks>
    /// The return value depends on whether the underlying GrindCore stream supports writing.
    /// </remarks>
    public override bool CanWrite
    {
        get
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("DeflateStream");
            }

            if (!_modeInitialized)
            {
                // In compression mode, we can potentially write (traditional compress-while-writing)
                return _isEncoder || _baseStream.CanWrite;
            }

            return !_readToCompressMode && _grindCoreStream!.CanWrite;
        }
    }

    /// <summary>
    /// Gets the length of the stream. This property is not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown as deflate streams do not support length queries.</exception>
    public override long Length => throw new NotSupportedException();

    /// <summary>
    /// Gets the current position in the stream based on the GrindCore implementation.
    /// Setting the position is not supported.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when attempting to set the position.</exception>
    public override long Position
    {
        get
        {
            if (_grindCoreStream == null)
                return 0;
            return _grindCoreStream!.PositionFullSize;
        }
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Releases the resources used by the DeflateStream and optionally the underlying stream.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        try
        {
            if (!_disposed)
            {
#if DEBUG_STREAMS
                this.DebugDispose(typeof(DeflateStream));
#endif
                if (disposing)
                {
                    if (_modeInitialized && _grindCoreStream!.BufferedBytesUnused != 0)
                        _grindCoreStream.Complete(); //finalise without dispose
                    _grindCoreStream?.Dispose();
                    _compressionBuffer?.Dispose();

                    if (!_leaveOpen)
                    {
                        _baseStream?.Dispose();
                    }
                }
                _disposed = true;
            }
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Flushes the stream, handling encoder and decoder modes differently.
    /// For decoders, rewinds to unused input bytes. For encoders, flushes the GrindCore stream.
    /// </summary>
    public override void Flush()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }

        if (!_modeInitialized)
        {
            return; // Nothing to flush if not initialized
        }

        if (_readToCompressMode)
        {
            // In read-to-compress mode, ensure any pending data is compressed
            EnsureCompressionComplete();
        }
        else if (!_isEncoder)
        {
            int unused = _grindCoreStream!.BufferedBytesUnused;
            ((IStreamStack)this).Rewind(unused); //seek back to the bytes used
        }
        else
        {
            _grindCoreStream!.Flush();
        }
    }

    /// <summary>
    /// Reads data from the GrindCore deflate stream.
    /// When in compression mode, reads from the uncompressed input stream and returns compressed data.
    /// When in decompression mode, reads from the compressed input stream and returns decompressed data.
    /// </summary>
    /// <param name="buffer">The buffer to read data into.</param>
    /// <param name="offset">The offset in the buffer to start reading data.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes actually read.</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }

        // Initialize read-to-compress mode on first read if in compression mode
        if (!_modeInitialized && _isEncoder)
        {
            InitializeReadToCompressMode();
        }
        else if (!_modeInitialized)
        {
            throw new InvalidOperationException("Stream not properly initialized");
        }

        if (_readToCompressMode)
        {
            return ReadCompressed(buffer, offset, count);
        }

        return _grindCoreStream!.Read(buffer, offset, count);
    }

    /// <summary>
    /// Reads a single byte from the GrindCore deflate stream.
    /// When in compression mode, reads from the uncompressed input stream and returns compressed data.
    /// When in decompression mode, reads from the compressed input stream and returns decompressed data.
    /// </summary>
    /// <returns>The byte read, or -1 if the end of the stream is reached.</returns>
    public override int ReadByte()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }

        // Initialize read-to-compress mode on first read if in compression mode
        if (!_modeInitialized && _isEncoder)
        {
            InitializeReadToCompressMode();
        }
        else if (!_modeInitialized)
        {
            throw new InvalidOperationException("Stream not properly initialized");
        }

        if (_readToCompressMode)
        {
            var buffer = new byte[1];
            int bytesRead = ReadCompressed(buffer, 0, 1);
            return bytesRead > 0 ? buffer[0] : -1;
        }

        return _grindCoreStream!.ReadByte();
    }

    /// <summary>
    /// Seeking is not supported by deflate streams.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <summary>
    /// Setting the length is not supported by deflate streams.
    /// </summary>
    /// <exception cref="NotSupportedException">Always thrown.</exception>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <summary>
    /// Writes data to the GrindCore deflate stream.
    /// When in compression mode, writes uncompressed data to be compressed and output to the underlying stream.
    /// When in decompression mode, writes compressed data to be decompressed and output to the underlying stream.
    /// </summary>
    /// <param name="buffer">The buffer containing data to write.</param>
    /// <param name="offset">The offset in the buffer to start writing from.</param>
    /// <param name="count">The number of bytes to write.</param>
    public override void Write(byte[] buffer, int offset, int count)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }

        // Initialize write-to-compress mode on first write if in compression mode
        if (!_modeInitialized && _isEncoder)
        {
            InitializeWriteToCompressMode();
        }
        else if (!_modeInitialized)
        {
            throw new InvalidOperationException("Stream not properly initialized");
        }

        if (_readToCompressMode)
        {
            throw new NotSupportedException("Cannot write to a read-to-compress stream");
        }

        _grindCoreStream!.Write(buffer, offset, count);
    }

    /// <summary>
    /// Writes a single byte to the GrindCore deflate stream.
    /// When in compression mode, writes uncompressed data to be compressed and output to the underlying stream.
    /// When in decompression mode, writes compressed data to be decompressed and output to the underlying stream.
    /// </summary>
    /// <param name="value">The byte to write.</param>
    public override void WriteByte(byte value)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException("DeflateStream");
        }

        // Initialize write-to-compress mode on first write if in compression mode
        if (!_modeInitialized && _isEncoder)
        {
            InitializeWriteToCompressMode();
        }
        else if (!_modeInitialized)
        {
            throw new InvalidOperationException("Stream not properly initialized");
        }

        if (_readToCompressMode)
        {
            throw new NotSupportedException("Cannot write to a read-to-compress stream");
        }

        _grindCoreStream!.WriteByte(value);
    }

    /// <summary>
    /// Ensures the mode is initialized before accessing GrindCore stream properties.
    /// </summary>
    private void EnsureModeInitialized()
    {
        if (!_modeInitialized && _isEncoder)
        {
            // Default to write-to-compress mode if accessing properties before any operations
            InitializeWriteToCompressMode();
        }
    }

    /// <summary>
    /// Initializes the stream for read-to-compress mode.
    /// </summary>
    private void InitializeReadToCompressMode()
    {
        if (_modeInitialized)
            return;

        _readToCompressMode = true;
        _compressionBuffer = new MemoryStream();

        var options = new NGC.CompressionOptions()
        {
            Type = (NGC.CompressionType)_compressionLevel,
            LeaveOpen = true
        };

        // Apply buffer size options using the helper
        GrindCoreBufferHelper.ApplyBufferSizeOptions(options, this, true, _writerOptions, null);

        _grindCoreStream = new NGC.DeflateZLib.DeflateStream(_compressionBuffer, options);
        _modeInitialized = true;
    }

    /// <summary>
    /// Initializes the stream for write-to-compress mode.
    /// </summary>
    private void InitializeWriteToCompressMode()
    {
        if (_modeInitialized)
            return;

        _readToCompressMode = false;

        var options = new NGC.CompressionOptions()
        {
            Type = (NGC.CompressionType)_compressionLevel,
            LeaveOpen = _leaveOpen
        };

        // Apply buffer size options using the helper
        GrindCoreBufferHelper.ApplyBufferSizeOptions(options, this, true, _writerOptions, null);

        _grindCoreStream = new NGC.DeflateZLib.DeflateStream(_baseStream, options);
        _modeInitialized = true;
    }

    /// <summary>
    /// Handles reading compressed data in read-to-compress mode.
    /// </summary>
    private int ReadCompressed(byte[] buffer, int offset, int count)
    {
        // Ensure we have compressed data available
        if (_compressionBuffer!.Position >= _compressionBuffer.Length && !_compressionComplete)
        {
            // Read more data from the input stream and compress it
            var inputBuffer = new byte[4096];
            int bytesRead = _baseStream.Read(inputBuffer, 0, inputBuffer.Length);

            if (bytesRead > 0)
            {
                // Write the data to the compressor
                _grindCoreStream!.Write(inputBuffer, 0, bytesRead);
                _grindCoreStream.Flush();
            }
            else
            {
                // No more input data - finalize compression
                EnsureCompressionComplete();
            }
        }

        // Read from the compressed output buffer
        int availableBytes = (int)(_compressionBuffer.Length - _compressionBuffer.Position);
        int bytesToRead = Math.Min(count, availableBytes);

        if (bytesToRead > 0)
        {
            _compressionBuffer.Read(buffer, offset, bytesToRead);
        }

        return bytesToRead;
    }

    /// <summary>
    /// Ensures compression is complete by finalizing the compressor.
    /// </summary>
    private void EnsureCompressionComplete()
    {
        if (!_compressionComplete)
        {
            _grindCoreStream!.Complete();
            _compressionComplete = true;
            _compressionBuffer!.Position = 0; // Reset to beginning for reading
        }
    }
}

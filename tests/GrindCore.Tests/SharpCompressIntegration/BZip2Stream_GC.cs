using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpCompress.Compressors;
using SharpCompress.Providers;

#if GRINDCORE
using Nanook.GrindCore;
using GrindCoreBZip2Stream = Nanook.GrindCore.BZip2.BZip2Stream;
#endif

namespace SharpCompress.Compressors.BZip2;

/// <summary>
/// Lightweight wrapper for GrindCore BZip2Stream that implements SharpCompress's real
/// BZip2Stream API shape (Create/CreateAsync factories + IFinishable + IAsyncDisposable), so
/// this file is a drop-in replacement for SharpCompress.Compressors.BZip2.BZip2Stream (see
/// GrindCore.SharpCompress/src/SharpCompress/Compressors/BZip2/BZip2Stream.cs and
/// BZip2Stream.Async.cs) rather than mirroring LZ4Stream_GC's plain-constructor, sync-only shape.
/// Re-verified against GrindCore.SharpCompress @ 2c2e2760 ("Update to latest SharpCompress
/// v0.50.4") after the fork was resynced with upstream. The ValueTask/IAsyncDisposable-based
/// members (CreateAsync/IsBZip2Async/FinishAsync/DisposeAsync/Memory{T} Read/WriteAsync) are only
/// available on netstandard2.1+/netcoreapp3.0+ - net48 has no ValueTask/IAsyncDisposable without
/// a polyfill package this project doesn't reference (matching every other _GC.cs shim here,
/// which are sync-only for the same reason); the byte[]-based Task ReadAsync/WriteAsync overrides
/// remain available everywhere since Stream has provided those since .NET 4.5.
/// </summary>
#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
public sealed class BZip2Stream : Stream, IFinishable, IAsyncDisposable
#else
public sealed class BZip2Stream : Stream, IFinishable
#endif
{
    private bool _disposed;

    /// <summary>
    /// Gets the compression mode this stream was created with.
    /// </summary>
    public CompressionMode Mode { get; }

#if GRINDCORE
    private readonly GrindCoreBZip2Stream _grindCoreStream;

    private BZip2Stream(Stream baseStream, CompressionMode compressionMode, bool leaveOpen)
    {
        Mode = compressionMode;
        _grindCoreStream = new GrindCoreBZip2Stream(baseStream, new CompressionOptions
        {
            // GrindCore's BZip2Stream has no separate "level" concept exposed here beyond
            // Optimal/SmallestSize/Fastest resolving to bzip2's blockSize100k - Optimal (which
            // resolves to level 9, matching bzip2's own CLI default) is the sensible default for
            // a generic Create() call with no level parameter, mirroring SharpCompress's own
            // BZip2CompressionProvider which ignores compressionLevel for BZip2 entirely.
            Type = compressionMode == CompressionMode.Compress
                ? Nanook.GrindCore.CompressionType.Optimal
                : Nanook.GrindCore.CompressionType.Decompress,
            LeaveOpen = leaveOpen
        });
    }
#else
    private BZip2Stream(Stream baseStream, CompressionMode compressionMode, bool leaveOpen)
    {
        throw new NotSupportedException("BZip2 compression requires GrindCore library");
    }
#endif

    /// <summary>
    /// Creates a BZip2Stream backed by GrindCore.
    /// </summary>
    /// <param name="stream">The stream to read from (decompress) or write to (compress).</param>
    /// <param name="compressionMode">Compression or decompression mode.</param>
    /// <param name="decompressConcatenated">
    /// Accepted for API compatibility with SharpCompress's own BZip2Stream.Create signature, but
    /// has no effect here - GrindCore's BZip2Decoder always transparently continues past a
    /// stream boundary into concatenated bzip2 data (multi-stream .bz2) when the following bytes
    /// look like another bzip2 header; there is no separate "stop at first stream end" mode to
    /// opt out of.
    /// </param>
    /// <param name="leaveOpen">Whether to leave the base stream open when disposing.</param>
    /// <param name="tolerateTruncatedStream">
    /// Not currently honored - GrindCore's decoder has no way to distinguish "clean EOF at a
    /// bzip2 block boundary" from any other truncation at the native BZ2_bzDecompress level (both
    /// surface as BZ_UNEXPECTED_EOF), so a genuinely truncated stream will still throw
    /// InvalidDataException regardless of this flag. Accepted only so callers written against
    /// the real BZip2Stream.Create signature still compile against this shim.
    /// </param>
    public static BZip2Stream Create(
        Stream stream,
        CompressionMode compressionMode,
        bool decompressConcatenated,
        bool leaveOpen = false,
        bool tolerateTruncatedStream = false) => new BZip2Stream(stream, compressionMode, leaveOpen);

    /// <summary>
    /// Consumes two bytes to test if there is a BZip2 header ("BZ").
    /// </summary>
    public static bool IsBZip2(Stream stream)
    {
        int b0 = stream.ReadByte();
        int b1 = stream.ReadByte();
        return b0 == 'B' && b1 == 'Z';
    }

    /// <summary>
    /// Finalizes compression, flushing any remaining buffered data and the bzip2 stream trailer.
    /// </summary>
    public void Finish()
    {
#if GRINDCORE
        _grindCoreStream?.Complete();
#endif
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Creates a BZip2Stream backed by GrindCore. GrindCore's own stream construction never
    /// performs I/O up front (unlike CBZip2InputStream.CreateAsync, which peeks the header), so
    /// this simply calls the synchronous <see cref="Create"/> and returns a completed ValueTask -
    /// provided for API compatibility with the real BZip2Stream.CreateAsync signature.
    /// </summary>
    public static ValueTask<BZip2Stream> CreateAsync(
        Stream stream,
        CompressionMode compressionMode,
        bool decompressConcatenated,
        bool leaveOpen = false,
        bool tolerateTruncatedStream = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<BZip2Stream>(Create(stream, compressionMode, decompressConcatenated, leaveOpen, tolerateTruncatedStream));
    }

    /// <summary>
    /// Asynchronously consumes two bytes to test if there is a BZip2 header ("BZ").
    /// </summary>
    public static async ValueTask<bool> IsBZip2Async(Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[] buffer = new byte[2];
        int bytesRead = await stream.ReadAsync(buffer, 0, 2, cancellationToken).ConfigureAwait(false);
        return bytesRead >= 2 && buffer[0] == (byte)'B' && buffer[1] == (byte)'Z';
    }

    /// <summary>
    /// Asynchronously finalizes compression. Use this instead of <see cref="Finish"/> when
    /// writing to an async-only stream.
    /// </summary>
    public async ValueTask FinishAsync(CancellationToken cancellationToken = default)
    {
#if GRINDCORE
        if (_grindCoreStream != null)
            await _grindCoreStream.CompleteAsync().ConfigureAwait(false);
#else
        await Task.CompletedTask.ConfigureAwait(false);
#endif
    }
#endif

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
        throw new NotSupportedException("BZip2 compression requires GrindCore library");
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
        throw new NotSupportedException("BZip2 compression requires GrindCore library");
#endif
    }

#if GRINDCORE && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _grindCoreStream!.ReadAsync(buffer, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        _grindCoreStream!.WriteAsync(buffer, cancellationToken);
#endif

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
#if GRINDCORE
        return _grindCoreStream!.ReadAsync(buffer, offset, count, cancellationToken);
#else
        throw new NotSupportedException("BZip2 compression requires GrindCore library");
#endif
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
#if GRINDCORE
        return _grindCoreStream!.WriteAsync(buffer, offset, count, cancellationToken);
#else
        throw new NotSupportedException("BZip2 compression requires GrindCore library");
#endif
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
#if GRINDCORE
            // _grindCoreStream itself already respects the LeaveOpen option passed at
            // construction (it only disposes the wrapped base stream when LeaveOpen is false),
            // matching the real BZip2Stream's current shape where leaveOpen lives on the wrapped
            // CBZip2Input/OutputStream rather than being special-cased here.
            _grindCoreStream?.Dispose();
#endif
            _disposed = true;
        }
        base.Dispose(disposing);
    }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
#if GRINDCORE
            if (_grindCoreStream != null)
                await _grindCoreStream.DisposeAsync().ConfigureAwait(false);
#endif
        }
        GC.SuppressFinalize(this);
    }
#endif
}

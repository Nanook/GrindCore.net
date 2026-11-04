using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Nanook.GrindCore;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Provides a stream implementation for LZMA2 compression and decompression.
    /// Inherits common <see cref="Stream"/> functionality from <see cref="CompressionStream"/>.
    /// Uses a customized LZMA2 implementation to allow the Stream.Write pattern for compression.
    /// </summary>
    public class Lzma2Stream : CompressionStream
    {
        // Decoding: at least 64 KiB, as LzmaStream has; with none, a BufferSize of 0 left the output buffer empty, so
        // nothing was decoded (the GrindCore audit's lzma.md 3.6.1 L6). Compression keeps its own sizing.
        protected override int MinimumBufferThreshold => IsCompress ? 0 : 0x10000;

        private Lzma2Decoder _decoder;
        private Lzma2Encoder _encoder;
        private CompressionBuffer _buffer;
        private bool _ended;

        /// <summary>
        /// Gets the input buffer size for LZMA2 operations.
        /// </summary>
        internal override int BufferSizeInput => 2 * 0x400 * 0x400;

        /// <summary>
        /// Gets the output buffer size for LZMA2 operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lzma2Stream"/> class with the specified stream and options.
        /// Dictionary size and fast-bytes are read from <see cref="CompressionOptions.Dictionary"/> when present.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="Exception">Thrown if <paramref name="options"/>.InitProperties is not set when decompressing.</exception>
        public Lzma2Stream(Stream stream, CompressionOptions options)
            : base(true, stream, CompressionAlgorithm.Lzma2, options)
        {
            _ended = false;

            if (IsCompress)
            {
                this.BufferSizeOutput = BufferThreshold + (BufferThreshold >> 1) + 0x200;
                if (this.BufferSizeOutput > int.MaxValue)
                    this.BufferSizeOutput = int.MaxValue;

                // Build merged dict options - only pass explicit dictionary options when actually provided
                CompressionDictionaryOptions? merged = null;
                var dictOpt = options?.Dictionary;

                if (dictOpt?.DictionarySize.HasValue == true && dictOpt.DictionarySize.Value != 0)
                {
                    // User explicitly set a dictionary size - pass all their dictionary options
                    merged = new CompressionDictionaryOptions
                    {
                        DictionarySize = dictOpt.DictionarySize,
                        FastBytes = dictOpt.FastBytes,
                        LiteralContextBits = dictOpt.LiteralContextBits,
                        LiteralPositionBits = dictOpt.LiteralPositionBits,
                        PositionBits = dictOpt.PositionBits,
                        Algorithm = dictOpt.Algorithm,
                        BinaryTreeMode = dictOpt.BinaryTreeMode,
                        HashBytes = dictOpt.HashBytes,
                        MatchCycles = dictOpt.MatchCycles
                    };
                }
                else
                {
                    // No explicit dictionary size - let native normalization choose based on compression level
                    // Only pass non-size-related dictionary options if they exist
                    if (dictOpt != null)
                    {
                        merged = new CompressionDictionaryOptions
                        {
                            // Don't set DictionarySize - let native normalization choose
                            FastBytes = dictOpt.FastBytes,
                            LiteralContextBits = dictOpt.LiteralContextBits,
                            LiteralPositionBits = dictOpt.LiteralPositionBits,
                            PositionBits = dictOpt.PositionBits,
                            Algorithm = dictOpt.Algorithm,
                            BinaryTreeMode = dictOpt.BinaryTreeMode,
                            HashBytes = dictOpt.HashBytes,
                            MatchCycles = dictOpt.MatchCycles
                        };
                    }
                    // else leave merged as null to use pure native defaults
                }

                // Fix: Pass proper parameters for solid mode
                // Solid mode should be used when: blockSize == -1 OR (threadCount == 1 AND blockSize == 0)
                int threads = options?.ThreadCount ?? 1;
                // Without a BlockSize, ThreadCount > 1 means blocks of automatic size (it used to stay solid, on one
                // thread: the solid path is the multi-call hook, which has one coder). An explicit -1 stays solid.
                long blockSize = options?.BlockSize ?? (threads > 1 ? 0 : -1);

                // Default to solid mode for single-threaded LZMA2 (matches 7-Zip behavior)
                if (threads == 1 && blockSize == 0)
                    blockSize = -1;

                // Pass merged dictionary options and thread/block settings into encoder.
                // BufferSize sizes the batch only for an explicit BlockSize of 0 (auto), as before
                _encoder = new Lzma2Encoder((int)CompressionType, threads, blockSize, merged,
                    options?.BlockSize == 0 ? options?.BufferSize ?? 0 : 0);

                // Block mode encodes whole batches, so the output buffer must hold one batch's worst case
                if (_encoder.BatchOutputBound > this.BufferSizeOutput)
                    this.BufferSizeOutput = _encoder.BatchOutputBound;

                this.Properties = new byte[] { _encoder.Properties };
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
            else
            {
                if (options.InitProperties == null)
                    throw new Exception("LZMA2 requires CompressionOptions.InitProperties to be set to an array when decompressing");

                this.Properties = options.InitProperties;
                _decoder = new Lzma2Decoder(options.InitProperties[0]);
                // the input buffer holds one whole chunk: up to 64 KiB of data plus its header (a BufferSize under that,
                // or 0, decoded nothing or threw; the GrindCore audit's lzma.md 3.6.1 L6)
                this.BufferSizeOutput = Math.Max(BufferThreshold, 0x10000) + 0x200;
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using LZMA2.
        /// Updates the position with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="data">The buffer to read decompressed data into.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesReadFromStream">The number of bytes read from the underlying stream.</param>
        /// <param name="length"> The maximum number of bytes to read.If 0, the method will fill the buffer if possible.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        /// <exception cref="NotSupportedException">Thrown if the stream is not in decompression mode.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream, int length = 0)
        {
            if (!CanRead)
                throw new NotSupportedException("Not for Compression mode");
            bytesReadFromStream = 0;
            int total = 0;
            if (length == 0 || length > data.AvailableWrite)
                length = data.AvailableWrite;

            // The stream ends where the decoder says so (the 0x00 end byte) or where the source does. A call that
            // writes nothing isn't the end: the decoder may have taken input without output yet (the GrindCore audit's
            // lzma.md 3.6.1 L2, L3)
            while (!_ended && total < length)
            {
                cancel.ThrowIfCancellationRequested();
                if (_buffer.AvailableRead == 0 && !readChunk())
                    break;
                int inSz = _buffer.AvailableRead;
                int decoded = _decoder.DecodeData(_buffer, ref inSz, data, length - total, out int status);
                bytesReadFromStream += inSz;
                total += decoded;
                if (status == LzmaStatusFinishedWithMark)
                    _ended = true;
                else if (decoded == 0 && inSz == 0)
                    break; // no progress
            }
            return total;
        }

        private const int LzmaStatusFinishedWithMark = 1; // LZMA_STATUS_FINISHED_WITH_MARK (7-Zip's LzmaDec.h)

        // BaseRead returns what one Read of the source gives, which can be less than asked for (a network stream, a
        // pipe, SharpCompress's BufferedSubStream at each refill), so this reads until there are n bytes or the source ends
        private int readFully(int n)
        {
            int total = 0;
            while (total < n)
            {
                int r = BaseRead(_buffer, n - total);
                if (r == 0)
                    break;
                total += r;
            }
            return total;
        }

        // Reads the next chunk whole into _buffer: its control byte, the rest of its header, then its data (the 0x00 end
        // byte has neither). Exactly one chunk, so the source isn't read past the stream's end byte. False when the source
        // has nothing more.
        private bool readChunk()
        {
            if (readFully(1) == 0)
                return false;
            byte control = _buffer.Data[_buffer.Size - 1];
            if (control != 0)
            {
                int hdrSz = _decoder.GetHeaderSize(control);
                int read = 1 + readFully(hdrSz - 1);
                if (read == hdrSz) // else the source ended in the header: the decoder gets what there is
                {
                    Lzma2BlockInfo info = _decoder.ReadSubBlockInfo(_buffer.Data, (ulong)(_buffer.Size - read), hdrSz);
                    if (info.BlockSize > read)
                        readFully(info.BlockSize - read);
                }
            }
            return true;
        }

        /// <summary>
        /// Compresses data using LZMA2 and writes it to the stream.
        /// Updating the position with the running total of bytes processed from the source stream.
        /// </summary>
        /// <param name="data">The buffer containing data to compress and write.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <exception cref="NotSupportedException">Thrown if the stream is not in compression mode.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = 0;
            cancel.ThrowIfCancellationRequested();
            int avRead = data.AvailableRead;
            long size = _encoder.EncodeData(data, _buffer, false, false, cancel);

            if (size > 0)
            {
                BaseWrite(_buffer, _buffer.AvailableRead);
                bytesWrittenToStream += (int)size;
            }
        }

        /// <summary>
        /// Flushes any remaining compressed data to the stream and finalizes the compression if requested.
        /// </summary>
        /// <param name="data">The buffer containing data to flush.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the underlying stream.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;
            if (IsCompress)
            {
                while (true)
                {
                    cancel.ThrowIfCancellationRequested();
                    // a Flush() is a sync flush: the stream goes on afterwards (audit/lzma.md 3.1)
                    long size = _encoder.EncodeData(data, _buffer, true, !complete, cancel);
                    if (size == 0)
                        break;
                    BaseWrite(_buffer, _buffer.AvailableRead);
                    bytesWrittenToStream += (int)size;
                }
                if (complete)
                {
                    _buffer.Pos = 0;
                    _buffer.Size = 0;
                    _buffer.Write(new byte[1] { 0x00 }, 0, 1);
                    bytesWrittenToStream += BaseWrite(_buffer, 1);
                }
            }
        }

        /// <summary>
        /// Disposes the <see cref="Lzma2Stream"/> and its resources.
        /// </summary>
        protected override void OnDispose()
        {
            if (IsCompress)
                try { _encoder.Dispose(); } catch { }
            else
                try { _decoder.Dispose(); } catch { }
            try { _buffer.Dispose(); } catch { }
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Asynchronously reads data from the stream and decompresses it using LZMA2.
        /// </summary>
        /// <param name="data">The buffer to read decompressed data into.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="length">The maximum number of bytes to read. If 0, the method will fill the buffer if possible.</param>
        /// <returns>A tuple containing (bytes decompressed, bytes read from stream).</returns>
        internal override async System.Threading.Tasks.ValueTask<(int result, int bytesRead)> OnReadAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken,
            int length = 0)
        {
            if (!CanRead)
                throw new NotSupportedException("Not for Compression mode");

            int bytesReadFromStream = 0;
            int total = 0;
            if (length == 0 || length > data.AvailableWrite)
                length = data.AvailableWrite;

            // as OnRead
            while (!_ended && total < length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_buffer.AvailableRead == 0 && !await readChunkAsync(cancellationToken).ConfigureAwait(false))
                    break;
                int inSz = _buffer.AvailableRead;
                int decoded = _decoder.DecodeData(_buffer, ref inSz, data, length - total, out int status);
                bytesReadFromStream += inSz;
                total += decoded;
                if (status == LzmaStatusFinishedWithMark)
                    _ended = true;
                else if (decoded == 0 && inSz == 0)
                    break; // no progress
            }
            return (total, bytesReadFromStream);
        }

        // as readFully and readChunk
        private async System.Threading.Tasks.ValueTask<int> readFullyAsync(int n, System.Threading.CancellationToken cancellationToken)
        {
            int total = 0;
            while (total < n)
            {
                int r = await BaseReadAsync(_buffer, n - total, cancellationToken).ConfigureAwait(false);
                if (r == 0)
                    break;
                total += r;
            }
            return total;
        }

        private async System.Threading.Tasks.ValueTask<bool> readChunkAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (await readFullyAsync(1, cancellationToken).ConfigureAwait(false) == 0)
                return false;
            byte control = _buffer.Data[_buffer.Size - 1];
            if (control != 0)
            {
                int hdrSz = _decoder.GetHeaderSize(control);
                int read = 1 + await readFullyAsync(hdrSz - 1, cancellationToken).ConfigureAwait(false);
                if (read == hdrSz)
                {
                    Lzma2BlockInfo info = _decoder.ReadSubBlockInfo(_buffer.Data, (ulong)(_buffer.Size - read), hdrSz);
                    if (info.BlockSize > read)
                        await readFullyAsync(info.BlockSize - read, cancellationToken).ConfigureAwait(false);
                }
            }
            return true;
        }

        /// <summary>
        /// Asynchronously compresses data using LZMA2 and writes it to the stream.
        /// </summary>
        /// <param name="data">The buffer containing data to compress and write.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The number of bytes written to the stream.</returns>
        internal override async System.Threading.Tasks.ValueTask<int> OnWriteAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            int bytesWrittenToStream = 0;
            cancellationToken.ThrowIfCancellationRequested();
            int avRead = data.AvailableRead;
            long size = _encoder.EncodeData(data, _buffer, false, false, new CancellableTask(cancellationToken));

            if (size > 0)
            {
                await BaseWriteAsync(_buffer, _buffer.AvailableRead, cancellationToken).ConfigureAwait(false);
                bytesWrittenToStream += (int)size;
            }
            return bytesWrittenToStream;
        }

        /// <summary>
        /// Asynchronously flushes any remaining compressed data to the stream and finalizes the compression if requested.
        /// </summary>
        /// <param name="data">The buffer containing data to flush.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="flush">Indicates if this is a flush operation.</param>
        /// <param name="complete">Indicates that there is no more data to compress.</param>
        /// <returns>The number of bytes written to the stream.</returns>
        internal override async System.Threading.Tasks.ValueTask<int> OnFlushAsync(
            CompressionBuffer data,
            System.Threading.CancellationToken cancellationToken,
            bool flush,
            bool complete)
        {
            int bytesWrittenToStream = 0;
            if (IsCompress)
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long size = _encoder.EncodeData(data, _buffer, true, !complete, new CancellableTask(cancellationToken));
                    if (size == 0)
                        break;
                    await BaseWriteAsync(_buffer, _buffer.AvailableRead, cancellationToken).ConfigureAwait(false);
                    bytesWrittenToStream += (int)size;
                }
                if (complete)
                {
                    _buffer.Pos = 0;
                    _buffer.Size = 0;
                    _buffer.Write(new byte[1] { 0x00 }, 0, 1);
                    bytesWrittenToStream += await BaseWriteAsync(_buffer, 1, cancellationToken).ConfigureAwait(false);
                }
            }
            return bytesWrittenToStream;
        }
#endif
    }
}

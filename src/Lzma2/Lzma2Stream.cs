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
        private Lzma2Decoder _decoder;
        private Lzma2Encoder _encoder;
        private CompressionBuffer _buffer;
        private bool _ended;
        private string _logPath;
        private StreamWriter _logWriter;

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
                this.BufferSizeOutput = BufferThreshold + (BufferThreshold >> 1) + 0x20;
                if (this.BufferSizeOutput > int.MaxValue)
                    this.BufferSizeOutput = int.MaxValue;
                
                this.BufferSizeOutput += 0x200;
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
                long blockSize = options?.BlockSize ?? -1;
                
                // Default to solid mode for single-threaded LZMA2 (matches 7-Zip behavior)
                if (threads == 1 && blockSize == 0)
                    blockSize = -1;

                // Pass merged dictionary options and thread/block settings into encoder.
                _encoder = new Lzma2Encoder((int)CompressionType, threads, blockSize, merged, options?.BufferSize ?? 0);

                this.Properties = new byte[] { _encoder.Properties };
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
            else
            {
                if (options.InitProperties == null)
                    throw new Exception("LZMA2 requires CompressionOptions.InitProperties to be set to an array when decompressing");

                this.Properties = options.InitProperties;
                _decoder = new Lzma2Decoder(options.InitProperties[0]);
                this.BufferSizeOutput = 0x3 * 0x400 * 0x400; // BufferThreshold;
                _buffer = new CompressionBuffer(this.BufferSizeOutput);

                try
                {
                    // Create a timestamped log file for testing and write directly to it
                    string fileName = $"Lzma2Stream_{DateTime.UtcNow:yyyyMMdd_HHmmssfff}.log";
                    _logPath = Path.Combine(Path.GetTempPath(), fileName);
                    _logWriter = new StreamWriter(File.Open(_logPath, FileMode.Create, FileAccess.Write, FileShare.Read)) { AutoFlush = true };
                    _logWriter.WriteLine($"Lzma2Stream: logging to {_logPath}");
                }
                catch
                {
                    // Swallow errors - logging is optional
                }
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
            int read = -1;
            int decoded = -1;
            if (length == 0 || length > data.AvailableWrite)
                length = data.AvailableWrite;

            while (!_ended && decoded != 0 && total < length)
            {
                read = 0;
                cancel.ThrowIfCancellationRequested();
                if (_buffer.AvailableRead == 0)
                {
                    read = BaseRead(_buffer, 1);
                    if (read == 1)
                    {
                        // If the newly-read control byte is 0x00 -> terminator/padding
                        if (_buffer.Data[_buffer.Size - 1] == 0)
                        {
                            // consume terminator byte and mark ended
                            _buffer.Read(1);
                            _logWriter?.WriteLine("Lzma2Stream: consumed terminator 0x00 (single-byte)");
                            _ended = true;
                            return total;
                        }

                        // Determine header length from control byte
                        byte controlPeek = _buffer.Data[_buffer.Size - 1];
                        _logWriter?.WriteLine($"Lzma2Stream: control byte peek=0x{controlPeek:X2}");
                        int headerTotal = (controlPeek & 0b10000000) != 0 ? (((controlPeek & 0b01000000) != 0) ? 6 : 5) : 3;

                        // Ensure full header
                        int got = ensureRead(_buffer, headerTotal);
                        _logWriter?.WriteLine($"Lzma2Stream: ensureRead header -> want={headerTotal}, got={got}, availableRead={_buffer.AvailableRead}");
                        if (got < headerTotal)
                            return total; // incomplete header - wait for more data

                        int headerOffset = _buffer.Size - headerTotal;
                        Lzma2BlockInfo info = _decoder.ReadSubBlockInfo(_buffer.Data, (ulong)headerOffset);
                        _logWriter?.WriteLine($"Lzma2Stream: parsed block info IsTerminator={info.IsTerminator}, IsControl={info.IsControlBlock}, InitProp={info.InitProp}, InitState={info.InitState}, Prop=0x{info.Prop:X2}, Unp={info.UncompressedSize}, Packed={info.CompressedSize}, HeaderSize={info.CompressedHeaderSize}, BlockSize={info.BlockSize}");

                        if (info.IsTerminator)
                        {
                            // consume terminator header and mark ended
                            _buffer.Read(info.CompressedHeaderSize);
                            _logWriter?.WriteLine("Lzma2Stream: consumed terminator after parse");
                            _ended = true;
                            return total;
                        }

                        // Ensure full block (header + payload) is available before decoding
                        int want = info.BlockSize;
                        if (want <= 0)
                            return total; // nothing to do or malformed

                        // Read exactly the block size (loop until all bytes are present or EOF)
                        got = ensureRead(_buffer, want);
                        _logWriter?.WriteLine($"Lzma2Stream: ensureRead block -> want={want}, got={got}, availableRead={_buffer.AvailableRead}");
                        if (_buffer.AvailableRead < want)
                            return total; // incomplete block - wait for more data

                        // Now decode exactly this block
                        int sz = info.BlockSize;
                        _logWriter?.WriteLine($"Lzma2Stream: calling DecodeData with inSz={sz}, outputWanted={length - total}");
                        decoded = _decoder.DecodeData(_buffer, ref sz, data, length - total, out var _);
                        _logWriter?.WriteLine($"Lzma2Stream: DecodeData returned decoded={decoded}, consumedIn={sz}, bufferAvailableRead={_buffer.AvailableRead}");
                        bytesReadFromStream += sz;
                        total += decoded;
                    }
                }
                if (_buffer.AvailableRead == 0)
                    return total;
                int inSz = _buffer.AvailableRead;
                decoded = _decoder.DecodeData(_buffer, ref inSz, data, length - total, out var _);
                bytesReadFromStream += inSz;
                total += decoded;
            }

            return total;
        }

        /// <summary>
        /// Ensure the internal buffer has at least <paramref name="want"/> bytes available to read.
        /// Calls BaseRead repeatedly until the requested amount is available or EOF is reached.
        /// Returns the number of bytes currently available to read after the attempt.
        /// </summary>
        /// <param name="inBuffer">The compression buffer to fill.</param>
        /// <param name="want">The desired number of available bytes.</param>
        /// <returns>Number of bytes available to read in <paramref name="inBuffer"/> after the call.</returns>
        private int ensureRead(CompressionBuffer inBuffer, int want)
        {
            if (want <= 0)
                return inBuffer.AvailableRead;

            // If we already have enough data, return immediately.
            if (inBuffer.AvailableRead >= want)
                return inBuffer.AvailableRead;

            // Attempt to read the remaining bytes needed.
            while (inBuffer.AvailableRead < want)
            {
                int toRead = want - inBuffer.AvailableRead;
                int r = BaseRead(inBuffer, toRead);
                if (r == 0)
                    break; // EOF or no more data available right now
            }

            return inBuffer.AvailableRead;
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
            long size = _encoder.EncodeData(data, _buffer, false, cancel);

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
                    long size = _encoder.EncodeData(data, _buffer, true, cancel);
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
            try
            {
                if (_logWriter != null)
                {
                    try { _logWriter.Flush(); } catch { }
                    try { _logWriter.Close(); } catch { }
                    _logWriter = null;
                }
            }
            catch { }
        }
    }
}

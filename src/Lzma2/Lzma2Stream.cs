using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

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

        /// <summary>
        /// Gets the input buffer size for LZMA2 operations.
        /// </summary>
        internal override int BufferSizeInput => 2 * 0x400 * 0x400;

        /// <summary>
        /// Gets the output buffer size for LZMA2 operations.
        /// </summary>
        internal override int BufferSizeOutput { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lzma2Stream"/> class with the specified stream, options, and optional dictionary size.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <param name="dictSize">The dictionary size to use for compression (default is 0).</param>
        /// <exception cref="Exception">Thrown if <paramref name="options"/>.InitProperties is not set when decompressing.</exception>
        public Lzma2Stream(Stream stream, CompressionOptions options, int dictSize = 0)
            : base(true, stream, CompressionAlgorithm.Lzma2, options)
        {
            _ended = false;

            if (IsCompress)
            {
                this.BufferSizeOutput = BufferThreshold + (BufferThreshold >> 1) + 0x20;
                if (this.BufferSizeOutput > int.MaxValue)
                    this.BufferSizeOutput = int.MaxValue;

                _encoder = new Lzma2Encoder((int)CompressionType, options.ThreadCount ?? -1, options.BlockSize ?? -1, dictSize, 0, options.BufferSize ?? 0);
                this.Properties = new byte[] { _encoder.Properties };
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
            else
            {
                if (options.InitProperties == null)
                    throw new Exception("LZMA requires CompressionOptions.InitProperties to be set to an array when decompressing");

                this.Properties = options.InitProperties;
                _decoder = new Lzma2Decoder(options.InitProperties[0]);
                this.BufferSizeOutput = BufferThreshold;
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lzma2Stream"/> class for compression or decompression.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        public Lzma2Stream(Stream stream, CompressionOptions options)
            : this(stream, options, 0)
        {
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
                        if (_buffer.Data[_buffer.Size - 1] != 0)
                        {
                            bool control = (_buffer.Data[_buffer.Size - 1] & 0b10000000) != 0;
                            read += BaseRead(_buffer, (control ? 6 : 5) - 1);
                            Lzma2BlockInfo info = _decoder.ReadSubBlockInfo(_buffer.Data, (ulong)(_buffer.Size - read));
                            if (info.CompressedSize != 0)
                                read += BaseRead(_buffer, info.BlockSize - read);
                        }
                        else
                            _ended = true;
                    }
                }
                if (_buffer.AvailableRead == 0)
                    return total;
                int inSz = _buffer.AvailableRead;
                decoded = _decoder.DecodeData(_buffer, ref inSz, data, length - total, out var _);
                bytesReadFromStream += inSz;
                total += decoded;
                _ended = _ended && decoded == 0;
            }

            return total;
        }

        /// <summary>
        /// Compresses data using LZMA2 and writes it to the stream.
        /// Updates the position with the running total of bytes processed from the source stream.
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
                cancel.ThrowIfCancellationRequested();
                long size = _encoder.EncodeData(data, _buffer, true, cancel);
                if (size > 0)
                {
                    BaseWrite(_buffer, _buffer.AvailableRead);
                    bytesWrittenToStream = (int)size;
                }
                if (complete)
                {
                    _buffer.Pos = 0;
                    _buffer.Size = 0;
                    _buffer.Write(new byte[1], 0, 1);
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
    }
}

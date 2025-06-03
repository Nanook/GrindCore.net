using System;
using System.IO;
using System.Reflection.Emit;
using System.Threading;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// A stream implementation for LZ4 compression and decompression.
    /// Inherits common Stream functionality from the CompressionStream class.
    /// Uses a customized LZ4 to allow Stream.Write() pattern for compression.
    /// </summary>
    public class ZStdStream : CompressionStream
    {
        private readonly ZStdDecoder _decoder;
        private readonly ZStdEncoder _encoder;
        private readonly CompressionBuffer _buffer;
        private bool _wroteData;

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.ZStd;
        internal override int BufferSizeInput => 0x20000; // * 0x400 * 0x400;
        internal override int BufferSizeOutput { get; }

        public ZStdStream(Stream stream, CompressionOptions options) : base(true, stream, options)
        {
            _wroteData = false;

            if (IsCompress)
            {
                this.BufferSizeOutput = CacheThreshold + (CacheThreshold >> 7) + 128;
                _encoder = new ZStdEncoder(CacheThreshold, (int)this.CompressionType);
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
            else
            {
                this.BufferSizeOutput = CacheThreshold;
                _decoder = new ZStdDecoder();
                _buffer = new CompressionBuffer(this.BufferSizeOutput);
            }
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using LZ4.
        /// Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, out int bytesReadFromStream)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            bytesReadFromStream = 0;
            int total = 0;

            while (data.AvailableWrite > total)
            {
                cancel.ThrowIfCancellationRequested();

                if (_buffer.Pos == 0)
                    _buffer.Write(BaseStream.Read(_buffer.Data, _buffer.Size, _buffer.AvailableWrite));

                if (_buffer.AvailableRead == 0)
                    return total;

                int decoded = (int)_decoder.DecodeData(_buffer, out int readSz, data, cancel);
                bytesReadFromStream += readSz;
                total += decoded;
            }

            return total;
        }

        /// <summary>
        /// Compresses data using LZ4 and writes it to the stream.
        /// Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = 0;
            cancel.ThrowIfCancellationRequested();

            int avRead = data.AvailableRead;
            long size = _encoder.EncodeData(data, _buffer, false, cancel);
            _wroteData = true;

            if (size > 0)
            {
                BaseStream.Write(_buffer.Data, _buffer.Pos, _buffer.AvailableRead);
                _buffer.Read(_buffer.AvailableRead);
                bytesWrittenToStream += (int)size;
            }
        }

        /// <summary>
        /// Flushes any remaining compressed data to the stream.
        /// </summary>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;

            if (IsCompress)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken
                long size = data.AvailableRead == 0 && _wroteData ? 0 : _encoder.EncodeData(data, _buffer, true, cancel);
                //if (flush || !_wroteData)
                    size += _encoder.Flush(_buffer);
                if (size > 0)
                {
                    BaseStream.Write(_buffer.Data, _buffer.Pos, _buffer.AvailableRead);
                    _buffer.Read(_buffer.AvailableRead);
                    bytesWrittenToStream = (int)size;
                }
            }
        }

        /// <summary>
        /// Disposes the Lz4Stream and its resources.
        /// </summary>
        protected override void OnDispose()
        {
            if (IsCompress)
                try { _encoder.Dispose(); } catch { }
            else
                try { _decoder.Dispose(); } catch { }
        }
    }
}
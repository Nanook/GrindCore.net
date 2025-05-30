using System;
using System.IO;
using System.Threading;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// A stream implementation for LZMA compression and decompression.
    /// Inherits common Stream functionality from the CompressionStream class.
    /// Uses a customised LZMA to allow Stream.Write() pattern for compression.
    /// </summary>
    public class LzmaStream : CompressionStream, ICompressionDefaults
    {
        private readonly LzmaDecoder _decoder;
        private readonly LzmaEncoder _encoder;
        private readonly CompressionBuffer _buffer;
        private bool _flushed;

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.Lzma;
        internal override int DefaultProcessSizeMin => 0x400 * 0x400;
        internal override int DefaultProcessSizeMax => 2 * 0x400 * 0x400;
        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level5;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxLzma2;

        private LzmaStream(Stream stream, CompressionOptions options, int dictionarySize = 0) : base(true, stream, options)
        {
            _flushed = false;
            if (IsCompress)
            {
                _encoder = new LzmaEncoder((int)CompressionType, (uint)dictionarySize, 0);
                Properties = _encoder.Properties;
                _buffer = new CompressionBuffer(options.ProcessSizeMax ?? _encoder.BlockSize << 1);
            }
            else
            {
                if (options.InitProperties == null)
                    throw new Exception("LZMA requires CompressionOptions.InitProperties to be set to an array when decompressing");

                _decoder = new LzmaDecoder(options.InitProperties);
                _buffer = new CompressionBuffer(options.ProcessSizeMax ?? this.DefaultProcessSizeMax);
            }
        }

        /// <summary>
        /// Initializes a new instance of LzmaStream for compression.
        /// </summary>
        public LzmaStream(Stream stream, CompressionOptions options) : this(stream, options, 0)
        {
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using LZMA. Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            bytesReadFromStream = 0;

            int total = 0;
            while (data.AvailableWrite > total)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                if (_buffer.Pos == 0) //will auto tidy and reset Pos to 0
                    _buffer.Write(BaseStream.Read(_buffer.Data, _buffer.Size, _buffer.AvailableWrite));

                if (_buffer.AvailableRead == 0)
                    return total;

                int decoded = _decoder.DecodeData(_buffer, out int readSz, data, out _);
                bytesReadFromStream += readSz; // returned as amount read
                total += decoded;
            }
            return total;
        }


        /// <summary>
        /// Compresses data using LZMA and writes it to the stream. Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = 0;
            cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

            // Use the DataBlock's properties for encoding
            int avRead = data.AvailableRead;
            long size = _encoder.EncodeData(data, _buffer, false, cancel);
            if (avRead != data.AvailableRead)
                _flushed = false;

            if (size > 0)
            {
                // Write the encoded data to the base stream
                BaseStream.Write(_buffer.Data, _buffer.Pos, _buffer.AvailableRead);
                _buffer.Read(_buffer.AvailableRead);

                // Update the position
                bytesWrittenToStream += (int)size;
            }
        }


        /// <summary>
        /// Flushes any remaining compressed data to the stream.
        /// </summary>
        internal override void OnFlush(CancellableTask cancel, out int bytesWrittenToStream)
        {

            bytesWrittenToStream = 0;
            if (IsCompress)
            {
                if (!_flushed)
                {
                    long size = _encoder.EncodeData(new CompressionBuffer(0), _buffer, true, cancel);
                    if (size > 0)
                    {
                        BaseStream.Write(_buffer.Data, _buffer.Pos, _buffer.AvailableRead);
                        _buffer.Read(_buffer.AvailableRead);
                        bytesWrittenToStream = (int)size;
                    }
                }
            }
            _flushed = true;
        }

        /// <summary>
        /// Disposes the LzmaStream and its resources.
        /// </summary>
        protected override void OnDispose(out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            OnFlush(new CancellableTask(), out bytesWrittenToStream);

            if (IsCompress)
                try { _encoder.Dispose(); } catch { }
            else
                try { _decoder.Dispose(); } catch { }
        }

    }
}
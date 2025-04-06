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
        private readonly byte[] _buffer;
        private MemoryStream _bufferStream;

        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level5;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxLzma2;

        /// <summary>
        /// Compression properties used for LZMA compression.
        /// </summary>
        public byte[] Properties { get; }

        private LzmaStream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null, int dictionarySize = 0, byte[]? decompressProperties = null) : base(true, stream, leaveOpen, type, version)
        {
            if (_compress)
            {
                _encoder = new LzmaEncoder((int)_type, (uint)dictionarySize, 0);
                Properties = _encoder.Properties;
                _buffer = new byte[_encoder.BlockSize << 1];
            }
            else
            {
                if (decompressProperties == null)
                    decompressProperties = new byte[] { 0x5D, 0, 0, 4, 0 }; //default

                _decoder = new LzmaDecoder(decompressProperties);
                _buffer = new byte[2 * 0x400 * 0x400];
                _bufferStream = new MemoryStream(_buffer);
                _bufferStream.SetLength(0);
            }
        }

        /// <summary>
        /// Initializes a new instance of LzmaStream for compression.
        /// </summary>
        /// <param name="stream">The underlying stream to read/write data from.</param>
        /// <param name="compressionType">The compression type for LZMA.</param>
        /// <param name="leaveOpen">Indicates whether to leave the underlying stream open when the stream is disposed.</param>
        /// <param name="version">Optional compression version.</param>
        /// <param name="dictionarySize">Optional dictionary size for compression.</param>
        public LzmaStream(Stream stream, CompressionType compressionType, bool leaveOpen, CompressionVersion? version = null) : this(stream, compressionType, leaveOpen, version, 0, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of LzmaStream for decompression.
        /// </summary>
        /// <param name="stream">The underlying stream to read/write data from.</param>
        /// <param name="leaveOpen">Indicates whether to leave the underlying stream open when the stream is disposed.</param>
        /// <param name="decompressProperties">The properties required for LZMA decompression.</param>
        /// <param name="version">Optional compression version.</param>
        public LzmaStream(Stream stream, bool leaveOpen, byte[] decompressProperties, CompressionVersion? version = null) : this(stream, CompressionType.Decompress, leaveOpen, version, 0, decompressProperties)
        {
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using LZMA. Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override int OnRead(DataBlock dataBlock, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            bytesReadFromStream = 0;

            int total = 0;
            while (dataBlock.Length > total)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                int remainingBytes = (int)(_bufferStream.Length - _bufferStream.Position);
                if (remainingBytes < 0x400)
                {
                    if (remainingBytes > 0)
                        _bufferStream.Read(_buffer, 0, (int)remainingBytes);
                    _bufferStream.SetLength(_buffer.Length); //required
                    remainingBytes += _baseStream.Read(_buffer, (int)remainingBytes, _buffer.Length - (int)remainingBytes);
                    _bufferStream.SetLength(remainingBytes);
                    _bufferStream.Position = 0;
                }

                if (remainingBytes == 0)
                    return total;

                int decoded = _decoder.DecodeData(_buffer, (int)_bufferStream.Position, ref remainingBytes, dataBlock, out _);
                bytesReadFromStream += remainingBytes; // returned as amount read
                total += decoded;
                _bufferStream.Position += remainingBytes;
            }
            return total;
        }


        /// <summary>
        /// Compresses data using LZMA and writes it to the stream. Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override void OnWrite(DataBlock dataBlock, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = 0;
            cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

            // Use the DataBlock's properties for encoding
            long size = _encoder.EncodeData(dataBlock, _buffer, 0, _buffer.Length, false, cancel);

            if (size > 0)
            {
                // Write the encoded data to the base stream
                _baseStream.Write(_buffer, 0, (int)size);

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
            if (_compress)
            {
                long size = _encoder.EncodeData(new DataBlock(), _buffer, 0, _buffer.Length, true, cancel);
                if (size > 0)
                {
                    _baseStream.Write(_buffer, 0, (int)size);
                    bytesWrittenToStream = (int)size;
                }
            }
        }

        /// <summary>
        /// Disposes the LzmaStream and its resources.
        /// </summary>
        protected override void OnDispose(out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            try { this.OnFlush(new CancellableTask(), out bytesWrittenToStream); } catch { }

            if (_compress)
                try { _encoder.Dispose(); } catch { }
            else
                try { _decoder.Dispose(); } catch { }
        }

    }
}
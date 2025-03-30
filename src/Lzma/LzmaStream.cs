using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// A stream implementation for LZMA compression and decompression.
    /// Inherits common Stream functionality from the StreamBase class.
    /// Uses a customised LZMA to allow Stream.Write() pattern for compression.
    /// </summary>
    public class LzmaStream : StreamBase
    {
        private readonly LzmaDecoder _decoder;
        private readonly LzmaEncoder _encoder;
        private readonly bool _leaveStreamOpen;
        private readonly Stream _baseStream;
        private readonly byte[] _buffer;
        private MemoryStream _bufferStream;

        private bool _compress;

        public override bool CanRead => _baseStream != null && !_compress && _baseStream.CanRead;
        public override bool CanWrite => _baseStream != null && _compress && _baseStream.CanWrite;

        /// <summary>
        /// Compression properties used for LZMA compression.
        /// </summary>
        public byte[] Properties { get; }

        /// <summary>
        /// Initializes a new instance of LzmaStream for compression.
        /// </summary>
        /// <param name="stream">The underlying stream to read/write data from.</param>
        /// <param name="compressionType">The compression type for LZMA.</param>
        /// <param name="leaveOpen">Indicates whether to leave the underlying stream open when the stream is disposed.</param>
        /// <param name="version">Optional compression version.</param>
        /// <param name="dictionarySize">Optional dictionary size for compression.</param>
        public LzmaStream(Stream stream, CompressionType compressionType, bool leaveOpen, CompressionVersion? version = null, int dictionarySize = 0) : base(true)
        {
            _compress = true;
            _leaveStreamOpen = leaveOpen;

            if (compressionType == CompressionType.Optimal)
                compressionType = CompressionType.Level5;
            else if (compressionType == CompressionType.SmallestSize)
                compressionType = CompressionType.MaxLzma2;
            else if (compressionType == CompressionType.Fastest)
                compressionType = CompressionType.Level1;

            _encoder = new LzmaEncoder((int)compressionType, (uint)dictionarySize, 0);
            Properties = _encoder.Properties;
            _buffer = new byte[_encoder.BlockSize << 1];
            _baseStream = stream;
        }

        /// <summary>
        /// Initializes a new instance of LzmaStream for decompression.
        /// </summary>
        /// <param name="stream">The underlying stream to read/write data from.</param>
        /// <param name="leaveOpen">Indicates whether to leave the underlying stream open when the stream is disposed.</param>
        /// <param name="decompressProperties">The properties required for LZMA decompression.</param>
        /// <param name="version">Optional compression version.</param>
        public LzmaStream(Stream stream, bool leaveOpen, byte[] decompressProperties, CompressionVersion? version = null) : base(true)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (decompressProperties == null)
                throw new ArgumentNullException(nameof(decompressProperties));

            _compress = false;
            _leaveStreamOpen = leaveOpen;

            _decoder = new LzmaDecoder(decompressProperties);
            _buffer = new byte[2 * 0x400 * 0x400];
            _bufferStream = new MemoryStream(_buffer);
            _bufferStream.SetLength(0);
            _baseStream = stream;
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using LZMA. Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override int OnRead(DataBlock dataBlock)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            int total = 0;
            while (dataBlock.Length > total)
            {
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
                base.AddPosition(remainingBytes); // returned as amount read
                total += decoded;
                _bufferStream.Position += remainingBytes;
            }
            return total;
        }


        /// <summary>
        /// Compresses data using LZMA and writes it to the stream. Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override void OnWrite(DataBlock dataBlock)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            // Use the DataBlock's properties for encoding
            long size = _encoder.EncodeData(dataBlock, _buffer, 0, _buffer.Length, false);

            if (size > 0)
            {
                // Write the encoded data to the base stream
                _baseStream.Write(_buffer, 0, (int)size);

                // Update the position
                base.AddPosition(size);
            }
        }


        /// <summary>
        /// Flushes any remaining compressed data to the stream.
        /// </summary>
        internal override void OnFlush()
        {
            if (_compress)
            {
                long size = _encoder.EncodeData(new DataBlock(), _buffer, 0, _buffer.Length, true);
                if (size > 0)
                {
                    _baseStream.Write(_buffer, 0, (int)size);
                    base.AddPosition(size);
                }
            }
        }

        /// <summary>
        /// Disposes the LzmaStream and its resources.
        /// </summary>
        protected override void OnDispose()
        {
            base.Flush();

            if (!_leaveStreamOpen)
                _baseStream.Dispose();

            if (_compress)
                _encoder.Dispose();
            else
                _decoder.Dispose();
        }

    }
}
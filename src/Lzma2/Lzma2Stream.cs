using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Streaming Fast LZMA2 compress
    /// </summary>
    public class Lzma2Stream : CompressionStream, ICompressionDefaults
    {
        private Lzma2Decoder _decoder;
        private Lzma2Encoder _encoder;
        private CompressionBuffer _buffer;

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.Lzma2;
        internal override int DefaultBufferOverflowSize => 3 * 0x400 * 0x400;
        internal override int DefaultBufferSize => 2 * 0x400 * 0x400;
        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level5;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxLzma2;

        public Lzma2Stream(Stream stream, CompressionOptions options, int dictSize = 0) : base(true, stream, options)
        {
            long bufferSize = options.BlockSize ?? -1;

            if (IsCompress)
            {
                if (bufferSize <= 0 || bufferSize > int.MaxValue)
                    bufferSize = options.BufferSize ?? this.DefaultBufferSize;
                else
                    options.BufferOverflowSize = options.BufferSize = (int)bufferSize;

                _encoder = new Lzma2Encoder((int)CompressionType, options.ThreadCount ?? -1, options.BlockSize ?? -1, dictSize, 0, options.BufferSize ?? 0);
                this.Properties = new byte[] { _encoder.Properties };
                //_buffComp = BufferPool.Rent(_bufferSize);
                _buffer = new CompressionBuffer(bufferSize << 2);
            }
            else
            {
                bufferSize = (int)(options.BufferSize ?? this.DefaultBufferSize);

                if (options.InitProperties == null)
                    throw new Exception("LZMA requires CompressionOptions.InitProperties to be set to an array when decompressing");

                this.Properties = options.InitProperties;
                _decoder = new Lzma2Decoder(options.InitProperties[0]);

                // Compressed stream input _outBuffer
                _buffer = new CompressionBuffer(bufferSize);
            }
        }

        /// <summary>
        /// Initialize streaming compress context
        /// </summary>
        /// <param name="stream">output data stream storee</param>
        /// <param name="type">compressed level / mode</param>
        /// <param name="nbThreads">thread use, auto = 0</param>
        /// <param name="bufferSize">Native interop _outBuffer size, default = 64M</param>
        /// <exception cref="FL2Exception"></exception>
        public Lzma2Stream(Stream stream, CompressionOptions options) : this(stream, options, 0)
        {
        }

        /// <summary>
        /// Read decompressed data
        /// </summary>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            //This line can be used in a loop to skip through blocks/chunks - leaving it here as a reference
            //Lzma2BlockInfo info = _decoder.ReadSubBlockInfo(_buffComp, 0);

            bytesReadFromStream = 0;

            int total = 0;
            //while (data.AvailableWrite > total)
            while (data.AvailableWrite > 0)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                if (_buffer.Pos == 0) //will auto tidy and reset Pos to 0
                    _buffer.Write(BaseStream.Read(_buffer.Data, _buffer.Size, _buffer.AvailableWrite));

                if (_buffer.AvailableRead == 0)
                    return total;

                int inSz = _buffer.AvailableRead;
                int decoded = _decoder.DecodeData(_buffer, ref inSz, data, data.AvailableWrite, out _);
                bytesReadFromStream += inSz; // returned as amount read
                total += decoded;
            }

            //EXPERIMENTAL: lzma doesn't read the null terminator. Accomodate it to keep read positions correct
            if (_buffer.AvailableRead > 0 && _buffer.Data[_buffer.Pos] == 0)
            {
                _buffer.Read(1);
                bytesReadFromStream++;
            }
            return total;
        }

        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = 0;
            cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

            // Use the DataBlock's properties for encoding
            int avRead = data.AvailableRead;
            long size = _encoder.EncodeData(data, _buffer, false, cancel);

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
        /// Write Checksum in the end. finish compress progress.
        /// </summary>
        /// <exception cref="FL2Exception"></exception>

        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;
            if (IsCompress)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken
                long size = _encoder.EncodeData(data, _buffer, true, cancel);
                if (size > 0)
                {
                    BaseStream.Write(_buffer.Data, _buffer.Pos, _buffer.AvailableRead);
                    _buffer.Read(_buffer.AvailableRead);
                    bytesWrittenToStream = (int)size;
                }
                if (complete)
                {
                    BaseStream.Write(new byte[1], 0, 1);
                    bytesWrittenToStream += 1;
                }
            }
        }

        protected override void OnDispose()
        {
            if (IsCompress)
                try { _encoder.Dispose(); } catch { }
            else
                try { _decoder.Dispose(); } catch { }
            try { _buffer.Dispose(); } catch { }
            //try { BufferPool.Return(_buffComp); } catch { }
        }

    }
}
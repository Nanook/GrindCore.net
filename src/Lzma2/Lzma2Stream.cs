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
        private Lzma2Decoder _dec;
        private Lzma2Encoder _enc;

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.Lzma2;
        internal override int DefaultBufferOverflowSize => 3 * 0x400 * 0x400;
        internal override int DefaultBufferSize => 2 * 0x400 * 0x400;
        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level5;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxLzma2;

        private byte[] _buffComp;
        private CompressionBuffer _cbuff;
        private CompressionBuffer _buff;

        public Lzma2Stream(Stream stream, CompressionOptions options, int dictSize = 0) : base(true, stream, options)
        {
            long bufferSize = options.BlockSize ?? -1;

            if (IsCompress)
            {
                if (bufferSize <= 0 || bufferSize > int.MaxValue)
                    bufferSize = (long)(options.BufferSize ?? this.DefaultBufferSize);
                else
                    options.BufferOverflowSize = options.BufferSize = (int)bufferSize;

                _enc = new Lzma2Encoder((int)CompressionType, options.ThreadCount ?? -1, options.BlockSize ?? -1, dictSize, 0, options.BufferSize ?? 0);
                this.Properties = new byte[] { _enc.Properties };
                _buffComp = BufferPool.Rent(bufferSize);
                _cbuff = new CompressionBuffer(bufferSize);
                _buff = new CompressionBuffer(bufferSize);
            }
            else
            {
                bufferSize = (long)(options.BufferSize ?? this.DefaultBufferSize);

                if (options.InitProperties == null)
                    throw new Exception("LZMA requires CompressionOptions.InitProperties to be set to an array when decompressing");

                this.Properties = options.InitProperties;
                _dec = new Lzma2Decoder(options.InitProperties[0]);

                // Compressed stream input _outBuffer
                _buffComp = BufferPool.Rent(bufferSize);
                _cbuff = new CompressionBuffer(bufferSize);
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
            int pos = data.Pos;
            bytesReadFromStream = 0;
            int read = 0;
            if (_cbuff.AvailableRead != 0)
            {
                read = Math.Min(data.AvailableWrite, _cbuff.AvailableRead);
                data.Write(_cbuff.Data, 0, read);
            }

            while (data.AvailableWrite != 0)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                BaseStream.Read(_buffComp, 0, 6);
                Lzma2BlockInfo info = _dec.ReadSubBlockInfo(_buffComp, 0);
                if (info.IsTerminator)
                    return read;

                if (info.InitProp)
                    _dec.SetProps(_dec.Properties); // feels like info.Prop should be passed, but it crashes it
                if (info.InitState)
                    _dec.SetState();
                BaseStream.Read(_buffComp, 6, info.BlockSize - 6);

                if (info.UncompressedSize <= data.AvailableWrite)
                {
                    _dec.DecodeData(_buffComp, 0, info.BlockSize, data, info.UncompressedSize, out int status);
                    bytesReadFromStream += info.BlockSize;
                    read += info.UncompressedSize;
                }
                else
                {
                    _cbuff.Pos = 0;
                    _cbuff.Size = info.UncompressedSize;
                    _dec.DecodeData(_buffComp, 0, info.BlockSize, _cbuff, info.UncompressedSize, out int status);
                    data.Write(_cbuff.Data, read, data.AvailableWrite);
                    //_buffMs.Read(dataBlock, c, dataBlock.Length - c);
                    read = data.Pos - pos;
                }
            }

            return read;
        }

        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = 0;
            cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

            // Use the DataBlock's properties for encoding
            int avRead = data.AvailableRead;
            long size = _enc.EncodeData(data, _buff, false, cancel);

            if (size > 0)
            {
                // Write the encoded data to the base stream
                BaseStream.Write(_buff.Data, _buff.Pos, _buff.AvailableRead);
                _buff.Read(_buff.AvailableRead);

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
                long size = _enc.EncodeData(data, _buff, true, cancel);
                if (size > 0)
                {
                    BaseStream.Write(_buff.Data, _buff.Pos, _buff.AvailableRead);
                    _buff.Read(_buff.AvailableRead);
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
                try { _enc.Dispose(); } catch { }
            else
                try { _dec.Dispose(); } catch { }
            try { _cbuff.Dispose(); } catch { }
            try { BufferPool.Return(_buffComp); } catch { }
        }

    }
}
using System;
using System.Collections.Generic;
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

        public byte Properties { get; }

        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level5;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxLzma2;

        private byte[] _buffComp;
        private byte[] _buff;
        private Stream _buffMs;


        private Lzma2Stream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null, long blockSize = 64 * 1024, int dictSize = 0, int threads = 1, byte? decompressProperties = null) : base(true, stream, leaveOpen, type, version)
        {
            if (_compress)
            {
                _enc = new Lzma2Encoder((int)_type, threads, (ulong)blockSize, (uint)dictSize, 0);
                this.Properties = _enc.Properties;
                _buffComp = new byte[blockSize];
                _buff = new byte[blockSize];
                _buffMs = new MemoryStream(_buff);
                _buffMs.SetLength(blockSize);
            }
            else
            {
                if (decompressProperties == null)
                    decompressProperties = 8;

                _dec = new Lzma2Decoder(decompressProperties.Value);

                // Compressed stream input buffer
                _buffComp = new byte[0x10000];
                _buff = new byte[0x200000];
                _buffMs = new MemoryStream(_buff);
                _buffMs.SetLength(0);
            }
        }

        /// <summary>
        /// Initialize streaming compress context
        /// </summary>
        /// <param name="stream">output data stream storee</param>
        /// <param name="type">compressed level / mode</param>
        /// <param name="nbThreads">thread use, auto = 0</param>
        /// <param name="bufferSize">Native interop buffer size, default = 64M</param>
        /// <exception cref="FL2Exception"></exception>
        public Lzma2Stream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null, long blockSize = 64 * 1024, int dictSize = 0, int threads = 1) : this(stream, type, leaveOpen, version, blockSize, dictSize, threads, null)
        {
        }

        /// <summary>
        /// Initialize streaming decompression context
        /// </summary>
        /// <param name="stream">output data stream storee</param>
        /// <param name="type">compressed level / mode</param>
        /// <param name="leaveOpen">leave dst </param>
        /// <param name="decompressProperties">Created by the compressor, normally stored somewhere with the stream/compressed data</param>
        /// <param name="version">Version of the algorithm to use</param>
        public Lzma2Stream(Stream stream, bool leaveOpen, byte decompressProperties, CompressionVersion? version = null) : this(stream, CompressionType.Decompress, leaveOpen, version, 0, 1, decompressProperties)
        {
        }


        /// <summary>
        /// Read decompressed data
        /// </summary>
        internal override int OnRead(DataBlock dataBlock, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {

            bytesReadFromStream = 0;
            int c = 0;
            int r = (int)(_buffMs.Length - _buffMs.Position);
            if (r != 0)
            {
                c = Math.Min(dataBlock.Length, r);
                dataBlock.Write(0, _buffMs, c);
            }

            while (c < dataBlock.Length)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                _baseStream.Read(_buffComp, 0, 6);
                Lzma2BlockInfo info = _dec.ReadSubBlockInfo(_buffComp, 0);
                if (info.IsTerminator)
                    return c;

                if (info.InitProp)
                    _dec.SetProps(_dec.Properties); // feels like info.Prop should be passed, but it crashes it
                if (info.InitState)
                    _dec.SetState();
                _baseStream.Read(_buffComp, 6, info.BlockSize - 6);

                if (c + info.UncompressedSize <= dataBlock.Length)
                {
                    _dec.DecodeData(_buffComp, 0, info.BlockSize, dataBlock, c, info.UncompressedSize, out int status);
                    bytesReadFromStream += info.BlockSize;
                    c += info.UncompressedSize;
                }
                else
                {
                    _buffMs.Position = 0;
                    _buffMs.SetLength(info.UncompressedSize);
                    _dec.DecodeData(_buffComp, 0, info.BlockSize, new DataBlock(_buff), 0, info.UncompressedSize, out int status);
                    dataBlock.Write(c, _buffMs, dataBlock.Length - c);
                    //_buffMs.Read(dataBlock, c, dataBlock.Length - c);
                    c = dataBlock.Length;
                }
            }

            return c;
        }


        internal override void OnWrite(DataBlock dataBlock, CancellableTask cancel, out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            int pos = 0;
            while (pos != dataBlock.Length)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                if (_buffMs.Position == 0 && dataBlock.Length - pos >= _buff.Length) // avoid copying data about and use the passed buffer
                {
                    int c = _enc.EncodeData(dataBlock, pos, _buff.Length, _buffComp, 0, _buffComp.Length);
                    if (c != 0 && (c > 1 || _buffComp[0] != 0))
                    {
                        _baseStream.Write(_buffComp, 0, c - 1); //don't write terminator
                        bytesWrittenToStream += c - 1;
                    }
                    pos += _buff.Length;
                }
                else
                {
                    int cp = (int)Math.Min(dataBlock.Length - pos, _buffMs.Length - _buffMs.Position);
                    dataBlock.Read(pos, _buffMs, cp);
                    pos += cp;
                    if (_buffMs.Length - _buffMs.Position == 0)
                    {
                        int c = _enc.EncodeData(new DataBlock(_buff), 0, _buff.Length, _buffComp, 0, _buffComp.Length);
                        if (c != 0 && (c > 1 || _buffComp[0] != 0))
                        {
                            _baseStream.Write(_buffComp, 0, c - 1); //don't write terminator
                            bytesWrittenToStream += c - 1;
                        }
                        _buffMs.Position = 0;
                    }
                }
            }

            //Write(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Write Checksum in the end. finish compress progress.
        /// </summary>
        /// <exception cref="FL2Exception"></exception>
        internal override void OnFlush(CancellableTask cancel, out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            if (_compress)
            {
                cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                if (_buffMs.Position != 0)
                {
                    int c = _enc.EncodeData(new DataBlock(_buff), 0, (int)_buffMs.Position, _buffComp, 0, _buffComp.Length);
                    if (c != 0 && (c > 1 || _buffComp[0] != 0))
                    {
                        _baseStream.Write(_buffComp, 0, c - 1); //don't write terminator
                        bytesWrittenToStream += c - 1;
                    }
                }
            }

        }

        protected override void OnDispose(out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            try { base.Flush(); } catch { }
            if (_compress)
                try
                {
                    _baseStream.Write(new byte[1], 0, 1);
                    bytesWrittenToStream += 1;
                } catch { } //write terminator

            if (_compress)
                try { _enc.Dispose(); } catch { }
            else
                try { _dec.Dispose(); } catch { }
            try { _buffMs.Dispose(); } catch { }
        }

    }
}
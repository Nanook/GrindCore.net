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
    public class Lzma2Stream : StreamBase
    {
        private Lzma2Decoder _dec;
        private Lzma2Encoder _enc;

        public byte Properties { get; }

        private readonly bool _leaveStreamOpen;
        private byte[] _buffComp;
        private byte[] _buff;
        private Stream _buffMs;

        private readonly Stream _baseStream;
        private bool _compress;
        public override bool CanRead => _baseStream != null && !_compress && _baseStream.CanRead;
        public override bool CanWrite => _baseStream != null && _compress && _baseStream.CanWrite;
        public override bool CanSeek => false;

        /// <summary>
        /// Can't determine decompressed data size
        /// </summary>
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <summary>
        /// Initialize streaming compress context
        /// </summary>
        /// <param name="stream">output data stream storee</param>
        /// <param name="type">compressed level / mode</param>
        /// <param name="nbThreads">thread use, auto = 0</param>
        /// <param name="bufferSize">Native interop buffer size, default = 64M</param>
        /// <exception cref="FL2Exception"></exception>
        public Lzma2Stream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null, long blockSize = 64 * 1024, int dictSize = 0, int threads = 1) : base(true)
        {
            _compress = true;
            _leaveStreamOpen = leaveOpen;

            if (type == CompressionType.Optimal)
                type = CompressionType.Level5;
            else if (type == CompressionType.SmallestSize)
                type = CompressionType.MaxLzma2;
            else if (type == CompressionType.Fastest)
                type = CompressionType.Level1;


            _enc = new Lzma2Encoder((int)type, threads, (ulong)blockSize, (uint)dictSize, 0);
            this.Properties = _enc.Properties;
            _buffComp = new byte[blockSize];
            _buff = new byte[blockSize];
            _buffMs = new MemoryStream(_buff);
            _buffMs.SetLength(blockSize);
            _baseStream = stream;
        }

        /// <summary>
        /// Initialize streaming decompression context
        /// </summary>
        /// <param name="stream">output data stream storee</param>
        /// <param name="type">compressed level / mode</param>
        /// <param name="leaveOpen">leave dst </param>
        /// <param name="decompressProperties">Created by the compressor, normally stored somewhere with the stream/compressed data</param>
        /// <param name="version">Version of the algorithm to use</param>
        public Lzma2Stream(Stream stream, bool leaveOpen, byte decompressProperties, CompressionVersion? version = null) : base(true)
        {
            _compress = false;
            _leaveStreamOpen = leaveOpen;

            _dec = new Lzma2Decoder(decompressProperties);

            // Compressed stream input buffer
            _buffComp = new byte[0x10000];
            _buff = new byte[0x200000];
            _buffMs = new MemoryStream(_buff);
            _buffMs.SetLength(0);
            _baseStream = stream;
        }


        /// <summary>
        /// Read decompressed data
        /// </summary>
        internal override int OnRead(DataBlock dataBlock)
        {
            int c = 0;
            int r = (int)(_buffMs.Length - _buffMs.Position);
            if (r != 0)
            {
                c = Math.Min(dataBlock.Length, r);
                dataBlock.Write(0, _buffMs, c);
            }

            while (c < dataBlock.Length)
            {
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
                    AddPosition(info.BlockSize);
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


        internal override void OnWrite(DataBlock dataBlock)
        {
            int pos = 0;
            while (pos != dataBlock.Length)
            {
                if (_buffMs.Position == 0 && dataBlock.Length - pos >= _buff.Length) // avoid copying data about and use the passed buffer
                {
                    int c = _enc.EncodeData(dataBlock, pos, _buff.Length, _buffComp, 0, _buffComp.Length);
                    if (c != 0 && (c > 1 || _buffComp[0] != 0))
                        _baseStream.Write(_buffComp, 0, c - 1); //don't write terminator
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
                            AddPosition(c - 1);
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
        internal override void OnFlush()
        {
            if (_compress)
            {
                if (_buffMs.Position != 0)
                {
                    int c = _enc.EncodeData(new DataBlock(_buff), 0, (int)_buffMs.Position, _buffComp, 0, _buffComp.Length);
                    if (c != 0 && (c > 1 || _buffComp[0] != 0))
                    {
                        _baseStream.Write(_buffComp, 0, c - 1); //don't write terminator
                        AddPosition(c - 1);
                    }
                }
            }

        }

        protected override void OnDispose()
        {
            base.Flush();
            if (_compress)
                _baseStream.Write(new byte[1], 0, 1); //write terminator

            if (!_leaveStreamOpen)
                _baseStream.Dispose();

            if (_compress)
                _enc.Dispose();
            else
                _dec.Dispose();
            _buffMs.Dispose();
        }

    }
}
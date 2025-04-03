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
    public class FastLzma2Stream : StreamBase
    {
        private FastLzma2Encoder _enc;
        private FastLzma2Decoder _dec;

        private readonly bool _leaveStreamOpen;
        private readonly Stream _stream;
        private bool _isCompressing;

        public override bool CanRead => !_isCompressing;
        public override bool CanWrite => _isCompressing;

        public FastLzma2Stream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, false, null, version)
        {
        }

        public FastLzma2Stream(Stream stream, CompressionType type, bool leaveOpen, CompressionParameters? compressParams = null, CompressionVersion? version = null) : base (true)
        {
            if (compressParams == null)
                compressParams = new CompressionParameters(0);

            _isCompressing = type != CompressionType.Decompress;
            _leaveStreamOpen = leaveOpen;
            _stream = stream;

            if (_isCompressing)
            {
                if (type == CompressionType.Optimal)
                    type = CompressionType.Level6;
                else if (type == CompressionType.SmallestSize)
                    type = CompressionType.MaxFastLzma2;
                else if (type == CompressionType.Fastest)
                    type = CompressionType.Level1;

                _enc = new FastLzma2Encoder((int)type, compressParams);
            }
            else
            {
                _dec = new FastLzma2Decoder(_stream, _stream.Length, (int)type, compressParams);
            }
        }

        internal override void OnWrite(DataBlock dataBlock, CancellableTask cancel)
        {
            _enc.EncodeData(dataBlock, true, _stream, cancel); 
        }

        internal override int OnRead(DataBlock dataBlock, CancellableTask cancel)
        {
            return _dec.DecodeData(dataBlock, _stream, cancel);
        }

        /// <summary>
        /// Write Checksum in the end. finish compress progress.
        /// </summary>
        /// <exception cref="FL2Exception"></exception>
        internal override void OnFlush(CancellableTask cancel)
        {
            if (_isCompressing)
                _enc.Flush(_stream, cancel);
            else
                _stream.Flush();
        }

        protected override void OnDispose()
        {
            base.Flush();
            if (!_leaveStreamOpen)
                _stream.Dispose();
            if (_isCompressing)
                _enc.Dispose();
            else
                _dec.Dispose();
        }
    }
}
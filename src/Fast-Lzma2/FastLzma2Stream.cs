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
    public class FastLzma2Stream : CompressionStream, ICompressionDefaults
    {
        private FastLzma2Encoder _enc;
        private FastLzma2Decoder _dec;
        private bool _flushed;

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.FastLzma2;
        internal override int DefaultProcessSizeMin => 0x400 * 0x400;
        internal override int DefaultProcessSizeMax => 0x400 * 0x400;
        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level6;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxFastLzma2;

        public FastLzma2Stream(Stream stream, CompressionOptions options) : this(stream, options, null)
        {
        }

        public FastLzma2Stream(Stream stream, CompressionOptions options, CompressionParameters? compressParams = null) : base (true, stream, options)
        {
            _flushed = false;

            if (compressParams == null)
                compressParams = new CompressionParameters(options.ThreadCount ?? 0);

            if (IsCompress)
                _enc = new FastLzma2Encoder((int)CompressionType, compressParams);
            else
                _dec = new FastLzma2Decoder(BaseStream, BaseStream.Length, (int)CompressionType, compressParams);
        }

        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            int avRead = data.AvailableRead;
            _enc.EncodeData(data, true, BaseStream, cancel, out bytesWrittenToStream);
            if (avRead != data.AvailableRead)
                _flushed = false;
        }

        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            return _dec.DecodeData(data, BaseStream, cancel, out bytesReadFromStream);
        }

        /// <summary>
        /// Write Checksum in the end. finish compress progress.
        /// </summary>
        /// <exception cref="FL2Exception"></exception>
        internal override void OnFlush(CancellableTask cancel, out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            if (IsCompress && !_flushed)
                _enc.Flush(BaseStream, cancel, out bytesWrittenToStream);
        }

        protected override void OnDispose(out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            OnFlush(new CancellableTask(), out bytesWrittenToStream);
            if (IsCompress)
                try { _enc.Dispose(); } catch { }
            else
                try { _dec.Dispose(); } catch { }
        }
    }
}
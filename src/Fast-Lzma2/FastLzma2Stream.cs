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

        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level6;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxFastLzma2;

        public FastLzma2Stream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, false, null, version)
        {
        }

        public FastLzma2Stream(Stream stream, CompressionType type, bool leaveOpen, CompressionParameters? compressParams = null, CompressionVersion? version = null) : base (true, stream, leaveOpen, type, version)
        {
            if (compressParams == null)
                compressParams = new CompressionParameters(0);

            if (_compress)
                _enc = new FastLzma2Encoder((int)_type, compressParams);
            else
                _dec = new FastLzma2Decoder(_baseStream, _baseStream.Length, (int)_type, compressParams);
        }

        internal override void OnWrite(DataBlock dataBlock, CancellableTask cancel, out int bytesWrittenToStream)
        {
            _enc.EncodeData(dataBlock, true, _baseStream, cancel, out bytesWrittenToStream); 
        }

        internal override int OnRead(DataBlock dataBlock, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            return _dec.DecodeData(dataBlock, _baseStream, cancel, out bytesReadFromStream);
        }

        /// <summary>
        /// Write Checksum in the end. finish compress progress.
        /// </summary>
        /// <exception cref="FL2Exception"></exception>
        internal override void OnFlush(CancellableTask cancel, out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            if (_compress)
                _enc.Flush(_baseStream, cancel, out bytesWrittenToStream);
            else
                _baseStream.Flush();
        }

        protected override void OnDispose(out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            try { OnFlush(new CancellableTask(), out bytesWrittenToStream); } catch { }
            if (_compress)
                try { _enc.Dispose(); } catch { }
            else
                try { _dec.Dispose(); } catch { }
        }
    }
}
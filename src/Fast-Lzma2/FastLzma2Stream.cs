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

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.FastLzma2;
        internal override int DefaultBufferOverflowSize => 3 * 0x400 * 0x400;
        internal override int DefaultBufferSize => 2 * 0x400 * 0x400;
        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level1;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level6;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.MaxFastLzma2;

        public FastLzma2Stream(Stream stream, CompressionOptions options) : this(stream, options, null)
        {
        }

        public FastLzma2Stream(Stream stream, CompressionOptions options, CompressionParameters? compressParams = null) : base (true, stream, options)
        {
            if (compressParams == null)
                compressParams = new CompressionParameters(options.ThreadCount ?? 0, 2 * 1024 * 1024);

            if (IsCompress)
                _enc = new FastLzma2Encoder((int)CompressionType, compressParams);
            else
                _dec = new FastLzma2Decoder(BaseStream, BaseStream.Length, (int)CompressionType, compressParams);
        }

        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;
            int avRead = data.AvailableRead;
            if (avRead == 0)
                return;
            _enc.EncodeData(data, true, BaseStream, cancel, out bytesWrittenToStream);
        }

        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            return _dec.DecodeData(data, BaseStream, cancel, out bytesReadFromStream);
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
                OnWrite(data, cancel, out bytesWrittenToStream); //data may have 0 bytes
                //if (flush)
                    _enc.Flush(BaseStream, cancel, out int bytesWrittenToStream2);
                bytesWrittenToStream += bytesWrittenToStream2;
            }
        }

        protected override void OnDispose()
        {
            if (IsCompress)
                try { _enc.Dispose(); } catch { }
            else
                try { _dec.Dispose(); } catch { }
        }
    }
}
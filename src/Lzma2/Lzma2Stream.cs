using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Streaming Fast LZMA2 compress
    /// </summary>
    public class Lzma2Stream : Stream
    {
        private Lzma2Decoder _dec;
        private Lzma2Encoder _enc;

        public byte Properties { get; }

        private readonly bool _leaveStreamOpen;
        private byte[] _buffComp;
        private byte[] _buff;
        private Stream _buffMs;

        private bool _disposed;
        private readonly Stream _stream;
        private bool _isComp;
        public override bool CanRead => _stream != null && _stream.CanRead;
        public override bool CanWrite => _stream != null && _stream.CanWrite;
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
        public Lzma2Stream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null, long blockSize = 64 * 1024, int dictSize = 0, int threads = 1)
        {
            _isComp = true;
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
            _stream = stream;
        }

        /// <summary>
        /// Initialize streaming decompression context
        /// </summary>
        /// <param name="stream">output data stream storee</param>
        /// <param name="type">compressed level / mode</param>
        /// <param name="leaveOpen">leave dst </param>
        /// <param name="decompressProperties">Created by the compressor, normally stored somewhere with the stream/compressed data</param>
        /// <param name="version">Version of the algorithm to use</param>
        public Lzma2Stream(Stream stream, bool leaveOpen, byte decompressProperties, CompressionVersion? version = null)
        {
            _isComp = false;
            _leaveStreamOpen = leaveOpen;

            _dec = new Lzma2Decoder(decompressProperties);

            // Compressed stream input buffer
            _buffComp = new byte[0x10000];
            _buff = new byte[0x200000];
            _buffMs = new MemoryStream(_buff);
            _buffMs.SetLength(0);
            _stream = stream;
        }

        /// <summary>
        /// Read decompressed data
        /// </summary>
        /// <param name="buffer">buffer array to receive data</param>
        /// <param name="offset">Start index in buffer</param>
        /// <param name="count">How many bytes to append</param>
        /// <returns>How many bytes read.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            int c = 0;
            int r = (int)(_buffMs.Length - _buffMs.Position);
            if (r != 0)
            {
                c = Math.Min(count, r);
                _buffMs.Read(buffer, offset, c);
            }

            while (c < count)
            {
                _stream.Read(_buffComp, 0, 6);
                Lzma2BlockInfo info = _dec.ReadSubBlockInfo(_buffComp, 0);
                if (info.IsTerminator)
                    return c;

                if (info.InitProp)
                    _dec.SetProps(_dec.Properties); // feels like info.Prop should be passed, but it crashes it
                if (info.InitState)
                    _dec.SetState();
                _stream.Read(_buffComp, 6, info.BlockSize - 6);

                if (c + info.UncompressedSize <= count)
                {
                    _dec.DecodeData(_buffComp, 0, info.BlockSize, buffer, offset + c, info.UncompressedSize, out int status);
                    c += info.UncompressedSize;
                }
                else
                {
                    _buffMs.Position = 0;
                    _buffMs.SetLength(info.UncompressedSize);
                    _dec.DecodeData(_buffComp, 0, info.BlockSize, _buff, 0, info.UncompressedSize, out int status);
                    _buffMs.Read(buffer, offset + c, count - c);
                    c = count;
                }
            }

            return c;
        }

        /// <summary>
        /// Read decompressed data
        /// </summary>
        /// <param name="buffer">buffer array to receive data</param>
        /// <returns>How many bytes read.</returns>
        public override int Read(Span<byte> buffer)
        {
            //return decompressCore(buffer);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Read decompressed data asynchronized
        /// </summary>
        /// <param name="buffer">buffer array to receive data</param>
        /// <param name="offset">Start index in buffer</param>
        /// <param name="count">How many bytes to append</param>
        /// <param name="cancellationToken"></param>
        /// <returns>How many bytes read.</returns>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            //Memory<byte> bufferMemory = buffer.AsMemory(offset, count);
            //return ReadAsync(buffer, cancellationToken).AsTask();
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Read decompressed data asynchronized
        /// </summary>
        /// <param name="buffer">buffer array to receive data</param>
        /// <param name="cancellationToken"></param>
        /// <returns>How many bytes read.</returns>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            //return new ValueTask<int>(decompressCore(buffer.Span, cancellationToken));
            throw new NotImplementedException();
        }

        /// <summary>
        /// Start compression and finish stream.
        /// </summary>
        /// <param name="buffer">Raw data</param>
        /// <param name="offset">Start index in buffer</param>
        /// <param name="count">How many bytes to append</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            int pos = 0;
            while (pos != count)
            {
                if (_buffMs.Position == 0 && count - pos >= _buff.Length) // avoid copying data about and use the passed buffer
                {
                    int c = _enc.EncodeData(buffer, offset + pos, _buff.Length, _buffComp, 0, _buffComp.Length);
                    if (c != 0 && (c > 1 || _buffComp[0] != 0))
                        _stream.Write(_buffComp, 0, c - 1); //don't write terminator
                    pos += _buff.Length;
                }
                else
                {
                    int cp = (int)Math.Min(count - pos, _buffMs.Length - _buffMs.Position);
                    _buffMs.Write(buffer, pos, cp);
                    pos += cp;
                    if (_buffMs.Length - _buffMs.Position == 0)
                    {
                        int c = _enc.EncodeData(_buff, 0, _buff.Length, _buffComp, 0, _buffComp.Length);
                        if (c != 0 && (c > 1 || _buffComp[0] != 0))
                            _stream.Write(_buffComp, 0, c - 1); //don't write terminator
                        _buffMs.Position = 0;
                    }
                }
            }

            //Write(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Start compression and finish stream.
        /// </summary>
        /// <param name="buffer">Raw data</param>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            //compressCore(buffer, true);
            throw new NotImplementedException();
        }

        /// <summary>
        /// Start compression and finish stream asynchronized.
        /// </summary>
        /// <param name="buffer">Raw data</param>
        /// <param name="offset">Start index in buffer</param>
        /// <param name="count">How many bytes to append</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
            //Memory<byte> bufferMemory = buffer.AsMemory(offset, count);
            //await new ValueTask<int>(compressCore(bufferMemory.Span, true, cancellationToken)).ConfigureAwait(false);
            //return;
        }

        /// <summary>
        /// Start compression and finish stream asynchronized.
        /// </summary>
        /// <param name="buffer">Raw data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
            //await new ValueTask<int>(compressCore(buffer.Span, true, cancellationToken)).ConfigureAwait(false);
            //return;
        }

        /// <summary>
        /// Not support
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        /// <summary>
        /// Not support
        /// </summary>
        /// <param name="value"></param>
        /// <exception cref="NotSupportedException"></exception>
        public override void SetLength(long value) => throw new NotSupportedException();


        /// <summary>
        /// Close streaming progress
        /// </summary>
        public override void Close() => Dispose(true);

        /// <summary>
        /// Write Checksum in the end. finish compress progress.
        /// </summary>
        /// <exception cref="FL2Exception"></exception>
        public override void Flush()
        {
            if (_isComp)
            {
                if (_buffMs.Position != 0)
                {
                    int c = _enc.EncodeData(_buff, 0, (int)_buffMs.Position, _buffComp, 0, _buffComp.Length);
                    if (c != 0 && (c > 1 || _buffComp[0] != 0))
                        _stream.Write(_buffComp, 0, c - 1); //don't write terminator
                }
            }
            //if (_isComp)
            //{
            //    nuint code = Interop.FastLzma2.FL2_endStream(_context, ref _compOutBuffer);
            //    if (FL2Exception.IsError(code))
            //    {
            //        if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
            //            throw new FL2Exception(code);
            //    }
            //    _stream.Write(_buffComp, 0, (int)_compOutBuffer.pos);
            //    //prepare for next mission
            //    code = Interop.FastLzma2.FL2_initCStream(_context, 0);
            //    if (FL2Exception.IsError(code))
            //        throw new FL2Exception(code);
            //}
            //else
            //    _stream.Flush();
        }


        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Flush();
                if (_isComp)
                    _stream.Write(new byte[1], 0, 1); //write terminator

                if (!_leaveStreamOpen)
                    _stream.Dispose();
                if (disposing)
                {
                }
                if (_isComp)
                    _enc.Dispose();
                else
                    _dec.Dispose();
                _buffMs.Dispose();
                _disposed = true;
            }
        }

        ~Lzma2Stream()
        {
            Dispose(disposing: false);
        }

        public new void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
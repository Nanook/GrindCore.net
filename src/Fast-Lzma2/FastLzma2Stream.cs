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
    public class FastLzma2Stream : Stream
    {
        private readonly int _bufferSize;
        private readonly bool _leaveStreamOpen;
        private byte[] _bufferArray;
        private GCHandle _bufferHandle;
        private FL2InBuffer _decompInBuffer;
        private FL2OutBuffer _compOutBuffer;
        private bool _disposed;
        private readonly Stream _stream;
        private readonly nint _context;
        private bool _isComp;
        public nint ContextPtr => _context;
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
        public FastLzma2Stream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, false, null, version)
        {
        }

        /// <summary>
        /// Initialize streaming compress context
        /// </summary>
        /// <param name="stream">output data stream storee</param>
        /// <param name="type">compressed level / mode</param>
        /// <param name="leaveOpen">leave dst </param>
        /// <param name="nbThreads">thread use, auto = 0</param>
        /// <param name="bufferSize">Native interop buffer size, default = 64M</param>
        /// <exception cref="FL2Exception"></exception>
        public FastLzma2Stream(Stream stream, CompressionType type, bool leaveOpen, CompressionParameters? compressParams = null, CompressionVersion? version = null)
        {
            if (compressParams == null)
                compressParams = new CompressionParameters(0);

            _isComp = type != CompressionType.Decompress;
            _leaveStreamOpen = leaveOpen;
            _disposed = false;
            _bufferSize = compressParams.DictionarySize;
            _stream = stream;

            if (_isComp)
            {
                if (type == CompressionType.Optimal)
                    type = CompressionType.Level6;
                else if (type == CompressionType.SmallestSize)
                    type = CompressionType.MaxFastLzma2;
                else if (type == CompressionType.Fastest)
                    type = CompressionType.Level1;

                if (compressParams.Threads == 1)
                    _context = Interop.FastLzma2.FL2_createCStream();
                else
                    _context = Interop.FastLzma2.FL2_createCStreamMt((uint)compressParams.Threads, 1);

                foreach (var kv in compressParams.Values)
                {
                    if (kv.Value != null)
                        this.setParameter(kv.Key, (nuint)kv.Value);
                }

                nuint code = Interop.FastLzma2.FL2_initCStream(_context, (int)type);
                if (FL2Exception.IsError(code))
                    throw new FL2Exception(code);
                // Compressed stream output buffer
                _bufferArray = new byte[_bufferSize];
                _bufferHandle = GCHandle.Alloc(_bufferArray, GCHandleType.Pinned);
                _compOutBuffer = new FL2OutBuffer()
                {
                    dst = _bufferHandle.AddrOfPinnedObject(),
                    size = (nuint)_bufferArray.Length,
                    pos = 0
                };
            }
            else
            {
                if (compressParams.Threads == 1)
                    _context = Interop.FastLzma2.FL2_createDStream();
                else
                    _context = Interop.FastLzma2.FL2_createDStreamMt((uint)compressParams.Threads);
                nuint code = Interop.FastLzma2.FL2_initDStream(_context);
                if (FL2Exception.IsError(code))
                    throw new FL2Exception(code);
                // Compressed stream input buffer
                _bufferArray = new byte[_stream.Length < _bufferSize ? _stream.Length : _bufferSize];
                int bytesRead = _stream.Read(_bufferArray, 0, _bufferArray.Length);
                _bufferHandle = GCHandle.Alloc(_bufferArray, GCHandleType.Pinned);
                _decompInBuffer = new FL2InBuffer()
                {
                    src = _bufferHandle.AddrOfPinnedObject(),
                    size = (nuint)bytesRead,
                    pos = 0
                };
            }
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
            return Read(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Read decompressed data
        /// </summary>
        /// <param name="buffer">buffer array to receive data</param>
        /// <returns>How many bytes read.</returns>
        public override int Read(Span<byte> buffer)
        {
            return decompressCore(buffer);
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
            Memory<byte> bufferMemory = buffer.AsMemory(offset, count);
            return ReadAsync(buffer, cancellationToken).AsTask();
        }

        /// <summary>
        ///  Read decompressed data asynchronized
        /// </summary>
        /// <param name="buffer">buffer array to receive data</param>
        /// <param name="cancellationToken"></param>
        /// <returns>How many bytes read.</returns>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return new ValueTask<int>(decompressCore(buffer.Span, cancellationToken));
        }

        /// <summary>
        /// Start compression and finish stream.
        /// </summary>
        /// <param name="buffer">Raw data</param>
        /// <param name="offset">Start index in buffer</param>
        /// <param name="count">How many bytes to append</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        /// <summary>
        /// Start compression and finish stream.
        /// </summary>
        /// <param name="buffer">Raw data</param>
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            compressCore(buffer, true);
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
            Memory<byte> bufferMemory = buffer.AsMemory(offset, count);
            await new ValueTask<int>(compressCore(bufferMemory.Span, true, cancellationToken)).ConfigureAwait(false);
            return;
        }

        /// <summary>
        /// Start compression and finish stream asynchronized.
        /// </summary>
        /// <param name="buffer">Raw data</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await new ValueTask<int>(compressCore(buffer.Span, true, cancellationToken)).ConfigureAwait(false);
            return;
        }

        private unsafe int compressCore(ReadOnlySpan<byte> buffer, bool appending, CancellationToken cancellationToken = default)
        {
            ref byte ref_buffer = ref MemoryMarshal.GetReference(buffer);
            fixed (byte* pBuffer = &ref_buffer)
            {
                FL2InBuffer inBuffer = new FL2InBuffer()
                {
                    src = (nint)pBuffer,
                    size = (nuint)buffer.Length,
                    pos = 0
                };
                nuint code;

                //push source data & receive part of compressed data
                do
                {
                    _compOutBuffer.pos = 0;
                    //code 1 output is full, 0 working
                    code = Interop.FastLzma2.FL2_compressStream(_context, ref _compOutBuffer, ref inBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                            throw new FL2Exception(code);
                    }
                    _stream.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                } while (!cancellationToken.IsCancellationRequested && (_compOutBuffer.pos != 0));
                if (cancellationToken.IsCancellationRequested)
                {
                    Interop.FastLzma2.FL2_cancelCStream(_context);
                    return 0;
                }

                // continue receive compressed data
                do
                {
                    _compOutBuffer.pos = 0;
                    //Returns 1 if input or output still exists in the CStream object, 0 if complete,
                    code = Interop.FastLzma2.FL2_copyCStreamOutput(_context, ref _compOutBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                            throw new FL2Exception(code);
                    }
                    _stream.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                } while (!cancellationToken.IsCancellationRequested && _compOutBuffer.pos != 0);
                if (cancellationToken.IsCancellationRequested)
                {
                    Interop.FastLzma2.FL2_cancelCStream(_context);
                    return 0;
                }

                // receive all remaining compressed data for safety
                do
                {
                    _compOutBuffer.pos = 0;
                    //Returns 1 if input or output still exists in the CStream object, 0 if complete,
                    code = Interop.FastLzma2.FL2_flushStream(_context, ref _compOutBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                            throw new FL2Exception(code);
                    }
                    _stream.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                } while (!cancellationToken.IsCancellationRequested && _compOutBuffer.pos != 0);
                if (cancellationToken.IsCancellationRequested)
                {
                    Interop.FastLzma2.FL2_cancelCStream(_context);
                    return 0;
                }

                //Write compress checksum if not appending mode
                if (!appending)
                {
                    code = Interop.FastLzma2.FL2_endStream(_context, ref _compOutBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.IsError(code))
                        {
                            if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                                throw new FL2Exception(code);
                        }
                    }
                    _stream.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                    //reset for next mission
                    code = Interop.FastLzma2.FL2_initCStream(_context, 0);
                    if (FL2Exception.IsError(code))
                        throw new FL2Exception(code);
                }
            }
            return 0;
        }

        /// <summary>
        /// How many data has been decompressed
        /// </summary>
        public long DecompressProgress => (long)Interop.FastLzma2.FL2_getDStreamProgress(_context);

        private unsafe int decompressCore(Span<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Set the memory limit for the decompression stream under MT. Otherwise decode will failed if buffer is too small.
            // Guess 64mb buffer is enough for most case.
            //Interop.FastLzma2.FL2_setDStreamMemoryLimitMt(_context, (nuint)64 * 1024 * 1024);
            ref byte ref_buffer = ref MemoryMarshal.GetReference(buffer);
            fixed (byte* pBuffer = &ref_buffer)
            {
                FL2OutBuffer outBuffer = new FL2OutBuffer()
                {
                    dst = (nint)pBuffer,
                    size = (nuint)buffer.Length,
                    pos = 0
                };

                nuint code;
                do
                {
                    // 0 finish,1 decoding
                    code = Interop.FastLzma2.FL2_decompressStream(_context, ref outBuffer, ref _decompInBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                        {
                            throw new FL2Exception(code);
                        }
                    }
                    //output is full
                    if (outBuffer.pos == outBuffer.size)
                    {
                        break;
                    }
                    //decode complete and no more input
                    if (code == 0 && _decompInBuffer.size == 0)
                    {
                        break;
                    }
                    if (code == 0 || _decompInBuffer.size == _decompInBuffer.pos)
                    {
                        int bytesRead = _stream.Read(_bufferArray, 0, _bufferArray.Length);
                        _decompInBuffer.size = (nuint)bytesRead;
                        _decompInBuffer.pos = 0;
                    }
                } while (!cancellationToken.IsCancellationRequested);
                return (int)outBuffer.pos;
            }
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
                nuint code = Interop.FastLzma2.FL2_endStream(_context, ref _compOutBuffer);
                if (FL2Exception.IsError(code))
                {
                    if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                        throw new FL2Exception(code);
                }
                _stream.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                //prepare for next mission
                code = Interop.FastLzma2.FL2_initCStream(_context, 0);
                if (FL2Exception.IsError(code))
                    throw new FL2Exception(code);
            }
            else
                _stream.Flush();
        }

        /// <summary>
        /// Set detail compress parameter
        /// </summary>
        /// <param name="param"> Parameter Enum</param>
        /// <param name="value"></param>
        /// <returns>Error Code</returns>
        /// <exception cref="FL2Exception"></exception>
        private nuint setParameter(FL2Parameter param, nuint value)
        {
            nuint code = Interop.FastLzma2.FL2_CStream_setParameter(_context, param, value);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return code;
        }

        /// <summary>
        /// Get detail compress parameter
        /// </summary>
        /// <param name="param"> Parameter Enum</param>
        /// <returns>Parameter Value</returns>
        /// <exception cref="FL2Exception"></exception>
        private nuint getParameter(FL2Parameter param)
        {
            var code = Interop.FastLzma2.FL2_CStream_getParameter(_context, param);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return code;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Flush();
                if (!_leaveStreamOpen)
                    _stream.Dispose();
                if (disposing)
                {
                    _bufferHandle.Free();
                }
                if (_isComp)
                    Interop.FastLzma2.FL2_freeCStream(_context);
                else
                    Interop.FastLzma2.FL2_freeDStream(_context);
                _disposed = true;
            }
        }

        ~FastLzma2Stream()
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
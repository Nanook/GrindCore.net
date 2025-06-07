using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.Threading;
using System.IO;

namespace Nanook.GrindCore.FastLzma2
{
    /// <summary>
    /// Provides a Fast-LZMA2 encoder supporting multi-threaded compression and streaming output.
    /// </summary>
    internal unsafe class FastLzma2Encoder : IDisposable
    {
        private FL2OutBuffer _compOutBuffer;
        private byte[] _bufferArray;
        private readonly IntPtr _context;
        private GCHandle _bufferHandle;
        private readonly int _bufferSize;
        private bool _flushed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FastLzma2Encoder"/> class with the specified buffer size, compression level, and parameters.
        /// </summary>
        /// <param name="bufferSize">The buffer size to use for output.</param>
        /// <param name="level">The compression level (default is 6).</param>
        /// <param name="compressParams">Optional compression parameters for multi-threaded compression.</param>
        /// <exception cref="FL2Exception">Thrown if the encoder context cannot be initialized or a parameter cannot be set.</exception>
        public FastLzma2Encoder(int bufferSize, int level = 6, CompressionParameters? compressParams = null)
        {
            if (compressParams == null)
                compressParams = new CompressionParameters(0);

            if (compressParams.Threads <= 1)
                _context = Interop.FastLzma2.FL2_createCStream();
            else
                _context = Interop.FastLzma2.FL2_createCStreamMt((uint)compressParams.Threads, 1);

            _bufferSize = bufferSize;
            _flushed = false;

            foreach (var kv in compressParams.Values)
            {
                if (kv.Value != null)
                    this.setParameter(kv.Key, (UIntPtr)kv.Value);
            }

            UIntPtr code = Interop.FastLzma2.FL2_initCStream(_context, level);
            if (FL2Exception.IsError(code))
                throw new FL2Exception(code);

            // Compressed stream output buffer
            _bufferArray = BufferPool.Rent(_bufferSize + (_bufferSize >> 1) + 0x20);
            _bufferHandle = GCHandle.Alloc(_bufferArray, GCHandleType.Pinned);
            _compOutBuffer = new FL2OutBuffer()
            {
                dst = _bufferHandle.AddrOfPinnedObject(),
                size = (UIntPtr)_bufferArray.Length,
                pos = 0
            };
        }

        /// <summary>
        /// Gets the value of a compression parameter from the encoder.
        /// </summary>
        /// <param name="param">The parameter to retrieve.</param>
        /// <returns>The value of the parameter.</returns>
        /// <exception cref="FL2Exception">Thrown if the parameter cannot be retrieved.</exception>
        private nuint getParameter(FL2Parameter param)
        {
            var code = Interop.FastLzma2.FL2_CStream_getParameter(_context, param);
            if (FL2Exception.IsError(code))
                throw new FL2Exception(code);
            return code;
        }

        /// <summary>
        /// Sets a compression parameter for the encoder.
        /// </summary>
        /// <param name="param">The parameter to set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>The result code from the native call.</returns>
        /// <exception cref="FL2Exception">Thrown if the parameter cannot be set.</exception>
        private UIntPtr setParameter(FL2Parameter param, UIntPtr value)
        {
            UIntPtr code = Interop.FastLzma2.FL2_CStream_setParameter(_context, param, value);
            if (FL2Exception.IsError(code))
                throw new FL2Exception(code);

            return code;
        }

        /// <summary>
        /// Encodes data from the provided buffer and writes compressed output to the specified stream.
        /// </summary>
        /// <param name="buffer">The buffer containing data to compress.</param>
        /// <param name="appending">Indicates if this is an appending operation (no end-of-stream marker).</param>
        /// <param name="output">The stream to write compressed data to.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the output stream.</param>
        /// <returns>Always returns 0.</returns>
        /// <exception cref="FL2Exception">Thrown if a fatal compression error occurs.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        public unsafe int EncodeData(CompressionBuffer buffer, bool appending, Stream output, CancellableTask cancel, out int bytesWrittenToStream)
        {
            bytesWrittenToStream = 0;

            fixed (byte* pBuffer = buffer.Data)
            {
                *&pBuffer += buffer.Pos;

                FL2InBuffer inBuffer = new FL2InBuffer()
                {
                    src = (IntPtr)pBuffer,
                    size = (UIntPtr)buffer.AvailableRead,
                    pos = 0
                };
                UIntPtr code;

                // Push source data & receive part of compressed data
                do
                {
                    cancel.ThrowIfCancellationRequested();

                    _compOutBuffer.pos = 0;
                    code = Interop.FastLzma2.FL2_compressStream(_context, ref _compOutBuffer, ref inBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                            throw new FL2Exception(code);
                    }
                    bytesWrittenToStream += (int)_compOutBuffer.pos;
                    output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                } while (_compOutBuffer.pos != 0);

                // Continue to receive compressed data
                do
                {
                    cancel.ThrowIfCancellationRequested();

                    _compOutBuffer.pos = 0;
                    code = Interop.FastLzma2.FL2_copyCStreamOutput(_context, ref _compOutBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                            throw new FL2Exception(code);
                    }
                    bytesWrittenToStream += (int)_compOutBuffer.pos;
                    output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                } while (_compOutBuffer.pos != 0);

                // Receive all remaining compressed data for safety
                do
                {
                    cancel.ThrowIfCancellationRequested();

                    _compOutBuffer.pos = 0;
                    code = Interop.FastLzma2.FL2_flushStream(_context, ref _compOutBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                            throw new FL2Exception(code);
                    }
                    bytesWrittenToStream += (int)_compOutBuffer.pos;
                    output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                } while (_compOutBuffer.pos != 0);

                // Write compress checksum if not appending mode
                if (!appending)
                {
                    cancel.ThrowIfCancellationRequested();

                    code = Interop.FastLzma2.FL2_endStream(_context, ref _compOutBuffer);
                    if (FL2Exception.IsError(code))
                    {
                        if (FL2Exception.IsError(code))
                        {
                            if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                                throw new FL2Exception(code);
                        }
                    }
                    bytesWrittenToStream += (int)_compOutBuffer.pos;
                    output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                    // Reset for next mission
                    code = Interop.FastLzma2.FL2_initCStream(_context, 0);
                    if (FL2Exception.IsError(code))
                        throw new FL2Exception(code);
                }
                buffer.Read((int)inBuffer.pos);
            }
            return 0;
        }

        /// <summary>
        /// Flushes any remaining compressed data to the output stream and finalizes the stream.
        /// </summary>
        /// <param name="output">The stream to write compressed data to.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesWrittenToStream">The number of bytes written to the output stream.</param>
        /// <exception cref="FL2Exception">Thrown if a fatal compression error occurs.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        public void Flush(Stream output, CancellableTask cancel, out int bytesWrittenToStream)
        {
            cancel.ThrowIfCancellationRequested();
            bytesWrittenToStream = 0;

            if (_flushed)
                return;

            _flushed = true;

            UIntPtr code = Interop.FastLzma2.FL2_endStream(_context, ref _compOutBuffer);
            if (FL2Exception.IsError(code))
            {
                if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                    throw new FL2Exception(code);
            }
            bytesWrittenToStream = (int)_compOutBuffer.pos;
            output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
            // Prepare for next mission
            code = Interop.FastLzma2.FL2_initCStream(_context, 0);
            if (FL2Exception.IsError(code))
                throw new FL2Exception(code);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="FastLzma2Encoder"/>.
        /// </summary>
        public void Dispose()
        {
            _bufferHandle.Free();
            Interop.FastLzma2.FL2_freeCStream(_context);
            GC.SuppressFinalize(this);
            BufferPool.Return(_bufferArray);
        }
    }
}

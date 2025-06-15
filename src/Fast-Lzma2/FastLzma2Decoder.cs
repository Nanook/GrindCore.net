using System;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore.FastLzma2
{
    /// <summary>
    /// Provides a decoder for Fast-LZMA2 compressed streams, supporting multi-threaded decompression.
    /// </summary>
    internal unsafe class FastLzma2Decoder : IDisposable
    {
        //private FL2OutBuffer _compOutBuffer;
        private CompressionBuffer _buffer;
        private readonly IntPtr _context;
        private GCHandle _bufferHandle;
        private FL2InBuffer _decompInBuffer;

        /// <summary>
        /// Gets the properties used for decoding.
        /// </summary>
        public byte[] Properties { get; }

        /// <summary>
        /// Gets the block size used for decompression.
        /// </summary>
        public int BlockSize { get; }

        /// <summary>
        /// Gets the block size to keep in memory.
        /// </summary>
        public uint KeepBlockSize { get; }

        /// <summary>
        /// Gets the total number of decompressed bytes.
        /// </summary>
        public long BytesFullSize { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastLzma2Decoder"/> class for the specified input stream and parameters.
        /// </summary>
        /// <param name="read">A delegate to provide input data.</param>
        /// <param name="bufferSize">The buffer size to use for decompression.</param>
        /// <param name="size">The expected size of the compressed data.</param>
        /// <param name="level">The compression level (default is 6).</param>
        /// <param name="compressParams">Optional compression parameters for multi-threaded decompression.</param>
        /// <exception cref="FL2Exception">Thrown if the decoder context cannot be initialized.</exception>
        public FastLzma2Decoder(Func<CompressionBuffer, int, int> read, int bufferSize, long size, int level = 6, CompressionParameters? compressParams = null)
        {
            if (compressParams == null)
                compressParams = new CompressionParameters(0);

            if (compressParams.Threads == 1)
                _context = Interop.FastLzma2.FL2_createDStream();
            else
                _context = Interop.FastLzma2.FL2_createDStreamMt((uint)compressParams.Threads);

            nuint code = Interop.FastLzma2.FL2_initDStream(_context);
            if (FL2Exception.IsError(code))
                throw new FL2Exception(code);

            // Compressed stream input buffer
            _buffer = new CompressionBuffer((int)(size < bufferSize ? size : bufferSize));
            int bytesRead = read(_buffer, _buffer.AvailableWrite);
            _bufferHandle = GCHandle.Alloc(_buffer.Data, GCHandleType.Pinned);
            _decompInBuffer = new FL2InBuffer()
            {
                src = _bufferHandle.AddrOfPinnedObject(),
                size = (nuint)bytesRead,
                pos = 0
            };
        }

        /// <summary>
        /// Sets a parameter for the decompression stream.
        /// </summary>
        /// <param name="param">The parameter to set.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>The result code from the native call.</returns>
        /// <exception cref="FL2Exception">Thrown if the parameter cannot be set.</exception>
        private UIntPtr setParameter(FL2Parameter param, UIntPtr value)
        {
            UIntPtr code = Interop.FastLzma2.FL2_CStream_setParameter(_context, param, value);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return code;
        }

        /// <summary>
        /// Gets the number of bytes decompressed so far.
        /// </summary>
        public long DecompressProgress => (long)Interop.FastLzma2.FL2_getDStreamProgress(_context);

        /// <summary>
        /// Decodes data from the input stream into the provided buffer.
        /// </summary>
        /// <param name="outData">The buffer to write decompressed data to.</param>
        /// <param name="read">A delegate to provide input data.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <param name="bytesReadFromStream">The number of bytes read from the input stream.</param>
        /// <returns>The number of bytes written to the buffer.</returns>
        /// <exception cref="FL2Exception">Thrown if a fatal decompression error occurs.</exception>
        /// <exception cref="OperationCanceledException">Thrown if cancellation is requested.</exception>
        public unsafe int DecodeData(CompressionBuffer outData, Func<CompressionBuffer, int, int> read, CancellableTask cancel, out int bytesReadFromStream)
        {
            outData.Tidy(); //ensure all the space is at the end making _buffer.AvailableWrite safe for interop

            bytesReadFromStream = 0;

            // Set the memory limit for the decompression stream under MT. Otherwise decode will fail if buffer is too small.
            // Guess 64mb buffer is enough for most cases.

            fixed (byte* pBuffer = outData.Data)
            {
                *&pBuffer += outData.Size; // writePos is Size
                FL2OutBuffer outBuffer = new FL2OutBuffer()
                {
                    dst = (nint)pBuffer,
                    size = (nuint)outData.AvailableWrite,
                    pos = 0
                };

                do
                {
                    cancel.ThrowIfCancellationRequested();

                    FL2ErrorCode code = FL2ErrorCode.NoError;
                    int pos = (int)_decompInBuffer.pos;
                    // 0 finish, 1 decoding
                    UIntPtr cd = Interop.FastLzma2.FL2_decompressStream(_context, ref outBuffer, ref _decompInBuffer);
                    if (FL2Exception.IsError(cd))
                    {
                        code = FL2Exception.GetErrorCode(cd);
                        if (code != FL2ErrorCode.Buffer)
                            throw new FL2Exception(code);
                    }

                    bytesReadFromStream += (int)_decompInBuffer.pos - pos;
                    _buffer.Read(bytesReadFromStream);

                    // output is full
                    if (outBuffer.pos == outBuffer.size)
                        break;
                    // decode complete and no more input
                    if (code == 0 && _decompInBuffer.size == 0)
                        break;
                    if (code == 0 && _decompInBuffer.size == _decompInBuffer.pos)
                    {
                        int bytesRead = read(_buffer, _buffer.AvailableWrite);
                        _decompInBuffer.size = (nuint)bytesRead;
                        _decompInBuffer.pos = 0;
                    }
                } while (true);

                outData.Write((int)outBuffer.pos);
                return (int)outBuffer.pos;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="FastLzma2Decoder"/>.
        /// </summary>
        public void Dispose()
        {
            _bufferHandle.Free();
            Interop.FastLzma2.FL2_freeDStream(_context);
            GC.SuppressFinalize(this);
            _buffer.Dispose();
        }
    }
}

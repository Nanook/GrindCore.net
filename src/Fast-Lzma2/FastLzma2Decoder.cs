using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Lzma.Interop.Lzma;
using static Nanook.GrindCore.Lzma.Interop;
using System.Linq;
using System.Threading;
using System.IO;

namespace Nanook.GrindCore.Lzma
{
    internal unsafe class FastLzma2Decoder : IDisposable
    {

        private FL2OutBuffer _compOutBuffer;
        private byte[] _bufferArray;
        private readonly IntPtr _context;
        private GCHandle _bufferHandle;
        private readonly int _bufferSize;
        private FL2InBuffer _decompInBuffer;


        public byte[] Properties { get; }
        public int BlockSize { get; }
        public uint KeepBlockSize { get; }
        public long BytesIn { get; private set; }
        public long BytesOut { get; private set; }
        public long BytesFullSize { get; private set; }

        public FastLzma2Decoder(Stream input, long size, int level = 6, CompressionParameters? compressParams = null)
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
            _bufferSize = 64 * 0x400 * 0x400; // compressParams.DictionarySize;
            _bufferArray = new byte[size < _bufferSize ? size : _bufferSize];
            int bytesRead = input.Read(_bufferArray, 0, _bufferArray.Length);
            _bufferHandle = GCHandle.Alloc(_bufferArray, GCHandleType.Pinned);
            _decompInBuffer = new FL2InBuffer()
            {
                src = _bufferHandle.AddrOfPinnedObject(),
                size = (nuint)bytesRead,
                pos = 0
            };
        }

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
        /// How many data has been decompressed
        /// </summary>
        public long DecompressProgress => (long)Interop.FastLzma2.FL2_getDStreamProgress(_context);

        public unsafe int DecodeData(DataBlock buffer, Stream input, CancellableTask cancel)
        {
            // Set the memory limit for the decompression stream under MT. Otherwise decode will failed if buffer is too small.
            // Guess 64mb buffer is enough for most case.
            //Interop.FastLzma2.FL2_setDStreamMemoryLimitMt(_context, (nuint)64 * 1024 * 1024);
            //ref byte ref_buffer = ref MemoryMarshal.GetReference(buffer.Data);
            //fixed (byte* pBuffer = &ref_buffer)
            fixed (byte* pBuffer = buffer.Data)
            {
                FL2OutBuffer outBuffer = new FL2OutBuffer()
                {
                    dst = (nint)pBuffer,
                    size = (nuint)buffer.Length,
                    pos = 0
                };

                do
                {
                    cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

                    FL2ErrorCode code = FL2ErrorCode.NoError;
                    // 0 finish,1 decoding
                    UIntPtr cd = Interop.FastLzma2.FL2_decompressStream(_context, ref outBuffer, ref _decompInBuffer);
                    if (FL2Exception.IsError(cd))
                    {
                        code = FL2Exception.GetErrorCode(cd);
                        if (code != FL2ErrorCode.Buffer)
                            throw new FL2Exception(code);
                    }
                    //output is full
                    if (outBuffer.pos == outBuffer.size)
                        break;
                    //decode complete and no more input
                    if (code == 0 && _decompInBuffer.size == 0)
                        break;
                    if (code == 0 && _decompInBuffer.size == _decompInBuffer.pos)
                    {
                        int bytesRead = input.Read(_bufferArray, 0, _bufferArray.Length);
                        _decompInBuffer.size = (nuint)bytesRead;
                        _decompInBuffer.pos = 0;
                    }
                } while (true);
                return (int)outBuffer.pos;
            }
        }

        public void Flush(Stream output)
        {
            UIntPtr code = Interop.FastLzma2.FL2_endStream(_context, ref _compOutBuffer);
            if (FL2Exception.IsError(code))
            {
                if (FL2Exception.GetErrorCode(code) != FL2ErrorCode.Buffer)
                    throw new FL2Exception(code);
            }
            output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
            //prepare for next mission
            code = Interop.FastLzma2.FL2_initCStream(_context, 0);
            if (FL2Exception.IsError(code))
                throw new FL2Exception(code);
        }

        public void Dispose()
        {
            _bufferHandle.Free();
            Interop.FastLzma2.FL2_freeDStream(_context);
            GC.SuppressFinalize(this);
        }

    }
}
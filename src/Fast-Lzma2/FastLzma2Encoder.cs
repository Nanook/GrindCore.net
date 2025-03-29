using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Lzma.Interop.Lzma;
using static Nanook.GrindCore.Lzma.Interop;
using System.Linq;
using System.Threading;
using System.IO;

namespace Nanook.GrindCore.Lzma
{
    internal unsafe class FastLzma2Encoder : IDisposable
    {

        private FL2OutBuffer _compOutBuffer;
        private byte[] _bufferArray;
        private readonly IntPtr _context;
        private GCHandle _bufferHandle;
        private readonly int _bufferSize;


        public byte[] Properties { get; }
        public int BlockSize { get; }
        public uint KeepBlockSize { get; }
        public long BytesIn { get; private set; }
        public long BytesOut { get; private set; }
        public long BytesFullSize { get; private set; }

        public FastLzma2Encoder(int level = 6, CompressionParameters? compressParams = null)
        {
            if (compressParams == null)
                compressParams = new CompressionParameters(0);

            if (compressParams.Threads == 1)
                _context = Interop.FastLzma2.FL2_createCStream();
            else
                _context = Interop.FastLzma2.FL2_createCStreamMt((uint)compressParams.Threads, 1);

            _bufferSize = compressParams.DictionarySize;

            foreach (var kv in compressParams.Values)
            {
                if (kv.Value != null)
                    this.setParameter(kv.Key, (UIntPtr)kv.Value);
            }

            UIntPtr code = Interop.FastLzma2.FL2_initCStream(_context, level);
            if (FL2Exception.IsError(code))
                throw new FL2Exception(code);
            // Compressed stream output buffer
            _bufferArray = new byte[_bufferSize];
            _bufferHandle = GCHandle.Alloc(_bufferArray, GCHandleType.Pinned);
            _compOutBuffer = new FL2OutBuffer()
            {
                dst = _bufferHandle.AddrOfPinnedObject(),
                size = (UIntPtr)_bufferArray.Length,
                pos = 0
            };
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

        private UIntPtr setParameter(FL2Parameter param, UIntPtr value)
        {
            UIntPtr code = Interop.FastLzma2.FL2_CStream_setParameter(_context, param, value);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return code;
        }

        public unsafe int EncodeData(DataBlock buffer, bool appending, Stream output, CancellationToken cancellationToken = default)
        {
            //ref byte ref_buffer = ref MemoryMarshal.GetReference(buffer.Data);
            //fixed (byte* pBuffer = &ref_buffer)
            fixed (byte* pBuffer = buffer.Data)
            {
                FL2InBuffer inBuffer = new FL2InBuffer()
                {
                    src = (IntPtr)pBuffer,
                    size = (UIntPtr)buffer.Length,
                    pos = 0
                };
                UIntPtr code;

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
                    output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
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
                    output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
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
                    output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
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
                    output.Write(_bufferArray, 0, (int)_compOutBuffer.pos);
                    //reset for next mission
                    code = Interop.FastLzma2.FL2_initCStream(_context, 0);
                    if (FL2Exception.IsError(code))
                        throw new FL2Exception(code);
                }
            }
            return 0;
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
            Interop.FastLzma2.FL2_freeCStream(_context);
            GC.SuppressFinalize(this);
        }

    }
}
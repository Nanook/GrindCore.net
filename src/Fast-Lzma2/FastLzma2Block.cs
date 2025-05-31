using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.FastLzma2;

namespace Nanook.GrindCore.FastLzma2
{
    public class FastLzma2Block : CompressionBlock
    {
        private nint _compressCtx;
        private nint _decompressCtx;
        private byte _dictProp;

        public override int RequiredCompressOutputSize { get; }

        public FastLzma2Block(CompressionAlgorithm algorithm, CompressionOptions options) : base(algorithm, options)
        {
            int blockSize = (int)options.BlockSize!;
            int level = (int)options.Type;
            int threads = options.ThreadCount ?? 1;

            _compressCtx = FL2_createCCtxMt((uint)threads);
            _decompressCtx = FL2_createDCtxMt((uint)threads);

            // Retrieve dictionary property for decoding
            _dictProp = FL2_getCCtxDictProp(_compressCtx);

            // Configure compression properties
            FL2_CCtx_setParameter(_compressCtx, FL2Parameter.DictionarySize, (nuint)options.BlockSize!);
            FL2_CCtx_setParameter(_compressCtx, FL2Parameter.CompressionLevel, (nuint)level);

            RequiredCompressOutputSize = (int)FL2_compressBound((nuint)blockSize);
        }

        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                nuint compressedSize = (nuint)dstData.Length;
                nuint result = FL2_compressCCtx(
                    _compressCtx, dstPtr, compressedSize, srcPtr, (nuint)srcData.Length, (int)this.Options.Type);

                if (result == 0)
                    throw new InvalidOperationException("Fast-LZMA2 Block Compression failed.");

                return (int)result;
            }
        }

        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                // Ensure correct decompression size
                nuint decompressedSize = FL2_findDecompressedSize(srcPtr, (nuint)srcData.Length);
                if (decompressedSize == (nuint)uint.MaxValue)
                    throw new InvalidOperationException("Invalid compressed data detected.");

                FL2_initDCtx(_decompressCtx, srcData.Data[0]); // Use property byte from compressed data

                nuint result = FL2_decompressMt(dstPtr, decompressedSize, srcPtr, (nuint)srcData.Length, (uint)(this.Options.ThreadCount ?? 1));

                if (result < 0 || result == (nuint)uint.MaxValue)
                    throw new InvalidOperationException($"Fast-LZMA2 Multi-Threaded Block Decompression failed. Error code: {result}");

                return (int)result;
            }
        }

        ~FastLzma2Block()
        {
            FL2_freeCCtx(_compressCtx);
            FL2_freeDCtx(_decompressCtx);
        }
    }
}
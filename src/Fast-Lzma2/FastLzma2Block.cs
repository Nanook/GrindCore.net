using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.FastLzma2;

namespace Nanook.GrindCore.FastLzma2
{
    /// <summary>
    /// Provides a block-based implementation of the Fast-LZMA2 compression algorithm, supporting multi-threaded compression and decompression.
    /// </summary>
    public class FastLzma2Block : CompressionBlock
    {
        private nint _compressCtx;
        private nint _decompressCtx;
        private byte _dictProp;

        /// <summary>
        /// Gets the required output buffer size for compression, as determined by the Fast-LZMA2 library.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FastLzma2Block"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        public FastLzma2Block(CompressionOptions options) : base(CompressionAlgorithm.FastLzma2, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.BlockSize == null)
                throw new ArgumentNullException(nameof(options.BlockSize));

            int blockSize = (int)options.BlockSize!;
            int level = (int)this.CompressionType;
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

        /// <summary>
        /// Compresses the source data block into the destination data block using Fast-LZMA2.
        /// </summary>
        /// <param name="srcData">The source data block to compress.</param>
        /// <param name="dstData">The destination data block to write compressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if compression fails.</exception>
        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                nuint compressedSize = (nuint)dstData.Length;
                nuint result = FL2_compressCCtx(
                    _compressCtx, dstPtr, compressedSize, srcPtr, (nuint)srcData.Length, (int)this.CompressionType);

                if (result == 0)
                    throw new InvalidOperationException("Fast-LZMA2 Block Compression failed.");

                return (int)result;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using Fast-LZMA2.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if decompression fails or the compressed data is invalid.</exception>
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

        /// <summary>
        /// Releases any resources used by the <see cref="FastLzma2Block"/>.
        /// </summary>
        internal override void OnDispose()
        {
            FL2_freeCCtx(_compressCtx);
            FL2_freeDCtx(_decompressCtx);
        }
    }
}

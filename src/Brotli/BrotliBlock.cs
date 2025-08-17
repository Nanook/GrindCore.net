using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Brotli;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>
    /// Provides a Brotli compression block for compressing and decompressing data using the Brotli algorithm.
    /// </summary>
    public class BrotliBlock : CompressionBlock
    {
        private const int WindowBits_Min = 10;
        private const int WindowBits_Default = 22;
        private const int WindowBits_Max = 24;

        private SafeBrotliEncoderHandle _encoderState;

        /// <summary>
        /// Gets the required output buffer size for compression, including Brotli overhead.
        /// </summary>
        public override int RequiredCompressOutputSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrotliBlock"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> or <c>options.BlockSize</c> is null.</exception>
        public BrotliBlock(CompressionOptions options) : base(CompressionAlgorithm.Brotli, options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (options.BlockSize == null)
                throw new ArgumentNullException(nameof(options.BlockSize));

            int blockSize = (int)options.BlockSize!;

            _encoderState = DN9_BRT_v1_1_0_BrotliEncoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            _encoderState.Version = this.Options.Version ?? this.Defaults.Version;

            // Set compression parameters
            DN9_BRT_v1_1_0_BrotliEncoderSetParameter(_encoderState, BrotliEncoderParameter.Quality, (uint)this.CompressionType);
            DN9_BRT_v1_1_0_BrotliEncoderSetParameter(_encoderState, BrotliEncoderParameter.LGWin, (uint)this.Options.BlockSize!);

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x10; // Adjust for overhead
        }

        /// <summary>
        /// Compresses the source data block into the destination data block using Brotli.
        /// </summary>
        /// <param name="srcData">The source data block to compress.</param>
        /// <param name="dstData">The destination data block to write compressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if Brotli block compression fails.</exception>
        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                UIntPtr compressedSize = (UIntPtr)dstData.Length;
                BOOL success = DN9_BRT_v1_1_0_BrotliEncoderCompress(
                    (int)this.CompressionType, //level
                    WindowBits_Default,
                    0,
                    (UIntPtr)srcData.Length,
                    srcPtr,
                    &compressedSize,
                    dstPtr
                );

                if (success == BOOL.FALSE)
                    throw new InvalidOperationException("Brotli Block Compression failed.");

                return (int)compressedSize;
            }
        }

        /// <summary>
        /// Decompresses the source data block into the destination data block using Brotli.
        /// </summary>
        /// <param name="srcData">The source data block to decompress.</param>
        /// <param name="dstData">The destination data block to write decompressed data to.</param>
        /// <returns>The number of bytes written to the destination block.</returns>
        /// <exception cref="InvalidOperationException">Thrown if Brotli block decompression fails.</exception>
        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                UIntPtr srcSize = (UIntPtr)srcData.Length;
                UIntPtr decompressedSize = (UIntPtr)dstData.Length;

                BOOL success = DN9_BRT_v1_1_0_BrotliDecoderDecompress(srcSize, srcPtr, &decompressedSize, dstPtr);

                if (success == BOOL.FALSE)
                    throw new InvalidOperationException("Brotli Block Decompression failed.");

                return (int)decompressedSize;
            }
        }

        /// <summary>
        /// Releases resources used by the Brotli encoder.
        /// </summary>
        internal override void OnDispose()
        {
            _encoderState.Dispose();
        }
    }
}

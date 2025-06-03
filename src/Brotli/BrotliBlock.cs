using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Brotli;

namespace Nanook.GrindCore.Brotli
{
    public class BrotliBlock : CompressionBlock
    {
        private const int WindowBits_Min = 10;
        private const int WindowBits_Default = 22;
        private const int WindowBits_Max = 24;

        private SafeBrotliEncoderHandle _encoderState;

        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.Brotli;

        public override int RequiredCompressOutputSize { get; }

        public BrotliBlock(CompressionOptions options) : base(options)
        {
            int blockSize = (int)options.BlockSize!;

            _encoderState = DN9_BRT_v1_1_0_EncoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            _encoderState.Version = this.Options.Version ?? this.Defaults.Version;

            // Set compression parameters
            DN9_BRT_v1_1_0_EncoderSetParameter(_encoderState, BrotliEncoderParameter.Quality, (uint)this.CompressionType);
            DN9_BRT_v1_1_0_EncoderSetParameter(_encoderState, BrotliEncoderParameter.LGWin, (uint)this.Options.BlockSize!);

            RequiredCompressOutputSize = blockSize + (blockSize >> 1) + 0x10; // Adjust for overhead
        }

        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                UIntPtr compressedSize = (UIntPtr)dstData.Length;
                BOOL success = DN9_BRT_v1_1_0_EncoderCompress(
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

        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                UIntPtr srcSize = (UIntPtr)srcData.Length;
                UIntPtr decompressedSize = (UIntPtr)dstData.Length;

                BOOL success = DN9_BRT_v1_1_0_DecoderDecompress(srcSize, srcPtr, &decompressedSize, dstPtr);

                if (success == BOOL.FALSE)
                    throw new InvalidOperationException("Brotli Block Decompression failed.");

                return (int)decompressedSize;
            }
        }

        ~BrotliBlock()
        {
            _encoderState.Dispose();
        }
    }
}
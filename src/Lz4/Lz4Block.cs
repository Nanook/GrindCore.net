

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;
using System.Reflection.Emit;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lz4;

namespace Nanook.GrindCore.Lz4
{
    public class Lz4Block : CompressionBlock
    {
        private int _acceleration;

        public override int RequiredCompressOutputSize { get; }

        public Lz4Block(CompressionAlgorithm algorithm, CompressionOptions options) : base(algorithm, options)
        {
            _acceleration = 1;
            int isize = (int)options.BlockSize!;
            RequiredCompressOutputSize = isize + (isize / 255) + 16;
        }

        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                SZ_Lz4_v1_9_4_Stream stream = new SZ_Lz4_v1_9_4_Stream();
                SZ_Lz4_v1_9_4_Init(ref stream);

                int compressedSize = SZ_Lz4_v1_9_4_CompressFastContinue(
                    ref stream, srcPtr, (IntPtr)dstPtr, srcData.Length, dstData.Length, _acceleration);

                SZ_Lz4_v1_9_4_End(ref stream);

                if (compressedSize <= 0)
                    throw new InvalidOperationException("LZ4 Block Compression failed.");

                return compressedSize;
            }
        }

        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* srcPtr = srcData.Data)
            fixed (byte* dstPtr = dstData.Data)
            {
                *&srcPtr += srcData.Offset;
                *&dstPtr += dstData.Offset;

                SZ_Lz4_v1_9_4_Stream stream = new SZ_Lz4_v1_9_4_Stream();
                SZ_Lz4_v1_9_4_Init(ref stream);

                int decompressedSize = SZ_Lz4_v1_9_4_DecompressSafeContinue(
                    ref stream, srcPtr, dstPtr, srcData.Length, dstData.Length);

                SZ_Lz4_v1_9_4_End(ref stream);

                if (decompressedSize < 0)
                    throw new InvalidOperationException("LZ4 Block Decompression failed.");

                return decompressedSize;
            }
        }
    }
}

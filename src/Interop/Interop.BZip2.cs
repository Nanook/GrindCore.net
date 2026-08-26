using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        /// <summary>
        /// Mirrors the native bz_stream layout embedded (by value) inside
        /// SZ_BZip2_v1_0_8_CompressionContext. Used ONLY via <c>Marshal.SizeOf</c> to size the fixed
        /// unmanaged allocation BZip2Encoder makes for the real context - never instantiated or
        /// passed by value/ref itself. libbzip2 stores the exact bz_stream* address passed to
        /// BZ2_bzCompressInit and rejects any later call whose pointer doesn't match byte-for-byte
        /// (bzlib.c: `if (s->strm != strm) return BZ_PARAM_ERROR`), so the context must live at a
        /// single fixed address for its whole lifetime - a managed struct field can't guarantee
        /// that across separate P/Invoke calls under a compacting GC, hence unmanaged allocation
        /// (see BZip2Encoder/BZip2Decoder) rather than the "PAL owns a managed struct by value"
        /// style used elsewhere (e.g. Interop.ZStream for pal_zlib_v1_3_1's PAL_ZStream, which zlib
        /// itself never validates the address of).
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct SZ_BZip2_v1_0_8_CompressionContext
        {
            internal IntPtr nextIn;
            internal uint availIn;
            internal uint totalInLo32;
            internal uint totalInHi32;
            internal IntPtr nextOut;
            internal uint availOut;
            internal uint totalOutLo32;
            internal uint totalOutHi32;
            internal IntPtr state;
            internal IntPtr bzalloc;
            internal IntPtr bzfree;
            internal IntPtr opaque;
        }

        /// <summary>
        /// Mirrors the native bz_stream layout embedded (by value) inside
        /// SZ_BZip2_v1_0_8_DecompressionContext, used only for sizing. See
        /// <see cref="SZ_BZip2_v1_0_8_CompressionContext"/>.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct SZ_BZip2_v1_0_8_DecompressionContext
        {
            internal IntPtr nextIn;
            internal uint availIn;
            internal uint totalInLo32;
            internal uint totalInHi32;
            internal IntPtr nextOut;
            internal uint availOut;
            internal uint totalOutLo32;
            internal uint totalOutHi32;
            internal IntPtr state;
            internal IntPtr bzalloc;
            internal IntPtr bzfree;
            internal IntPtr opaque;
        }

        internal static unsafe partial class BZip2
        {
            // BZip2 (libbzip2 1.0.8) result codes - see external/bzip2/bzip2/bzlib.h
            public const int BZ_OK = 0;
            public const int BZ_RUN_OK = 1;
            public const int BZ_FLUSH_OK = 2;
            public const int BZ_FINISH_OK = 3;
            public const int BZ_STREAM_END = 4;
            public const int BZ_SEQUENCE_ERROR = -1;
            public const int BZ_PARAM_ERROR = -2;
            public const int BZ_MEM_ERROR = -3;
            public const int BZ_DATA_ERROR = -4;
            public const int BZ_DATA_ERROR_MAGIC = -5;
            public const int BZ_IO_ERROR = -6;
            public const int BZ_UNEXPECTED_EOF = -7;
            public const int BZ_OUTBUFF_FULL = -8;
            public const int BZ_CONFIG_ERROR = -9;

            // BZ2_bzCompress action codes
            public const int BZ_RUN = 0;
            public const int BZ_FLUSH = 1;
            public const int BZ_FINISH = 2;

            /* ===== Streaming Compression =====
             * ctx is a pointer to a fixed (never-moving) block of unmanaged memory sized to hold
             * SZ_BZip2_v1_0_8_CompressionContext, allocated and freed by the caller (BZip2Encoder) -
             * NOT a `ref` onto a managed struct field. libbzip2's BZ2_bzCompress/BZ2_bzCompressEnd
             * store the bz_stream* pointer passed to BZ2_bzCompressInit and reject every later call
             * whose strm pointer doesn't match it byte-for-byte (bzlib.c: `if (s->strm != strm)
             * return BZ_PARAM_ERROR`) - so the context's address must never change across the
             * Create/Compress/Free call sequence, which a compacting-GC-managed struct field cannot
             * guarantee between separate P/Invoke calls. */

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_BZip2_v1_0_8_CreateCompressionContext(
                IntPtr ctx, int blockSize100k, int workFactor);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_BZip2_v1_0_8_CompressStream(
                IntPtr ctx,
                byte* dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcSize,
                int action,
                out long inSize, out long outSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_BZip2_v1_0_8_FreeCompressionContext(IntPtr ctx);

            /* ===== Streaming Decompression ===== (same fixed-address requirement as above) */

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_BZip2_v1_0_8_CreateDecompressionContext(
                IntPtr ctx, int small);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_BZip2_v1_0_8_DecompressStream(
                IntPtr ctx,
                byte* dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcSize,
                out long inSize, out long outSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_BZip2_v1_0_8_FreeDecompressionContext(IntPtr ctx);

            /* ===== Block Compression & Decompression (one-shot, no persistent context) ===== */

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_BZip2_v1_0_8_CompressBlock(
                byte* dst, ref UIntPtr dstCapacity,
                byte* src, UIntPtr srcSize,
                int blockSize100k, int workFactor);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_BZip2_v1_0_8_DecompressBlock(
                byte* dst, ref UIntPtr dstCapacity,
                byte* src, UIntPtr srcSize,
                int small);
        }
    }
}

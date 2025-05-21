using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        /* ===== Compression Strategies & Directives ===== */
        public enum SZ_ZStd_v1_5_6_Strategy
        {
            Fast = 1,
            DFast = 2,
            Greedy = 3,
            Lazy = 4,
            Lazy2 = 5,
            BTLazy2 = 6,
            BTOpt = 7,
            BTUltra = 8,
            BTUltra2 = 9
        }

        public enum SZ_ZStd_v1_5_6_EndDirective
        {
            Continue = 0,
            Flush = 1,
            End = 2
        }

        public enum SZ_ZStd_v1_5_6_ResetDirective
        {
            ResetSessionOnly = 1,
            ResetParameters = 2,
            ResetSessionAndParameters = 3
        }

        public enum SZ_ZStd_v1_5_6_CParameter
        {
            CLevel = 100,
            WindowLog = 101,
            HashLog = 102,
            ChainLog = 103,
            SearchLog = 104,
            MinMatch = 105,
            TargetLength = 106,
            Strategy = 107,
            TargetCBlockSize = 130
        }

        public enum SZ_ZStd_v1_5_6_DParameter
        {
            DWindowLogMax = 100
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_6_Bounds
        {
            public UIntPtr error;
            public int lowerBound;
            public int upperBound;
        }

        /* ===== Context & Dictionary Structures ===== */
        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_6_CompressionContext
        {
            public IntPtr cctx; // Native compression context pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_6_DecompressionContext
        {
            public IntPtr dctx; // Native decompression context pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_6_CompressionDict
        {
            public IntPtr cdict; // Compression dictionary pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_6_DecompressionDict
        {
            public IntPtr ddict; // Decompression dictionary pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_6_InBuffer
        {
            public IntPtr src;
            public UIntPtr size;
            public UIntPtr pos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_6_OutBuffer
        {
            public IntPtr dst;
            public UIntPtr size;
            public UIntPtr pos;
        }

        /* ===== Native Interop Binding ===== */
        internal static unsafe partial class ZStd
        {
            /* ===== Compression Context Management ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern int SZ_ZStd_v1_5_6_CreateCompressionContext(SZ_ZStd_v1_5_6_CompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern int SZ_ZStd_v1_5_6_CreateDecompressionContext(SZ_ZStd_v1_5_6_DecompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern void SZ_ZStd_v1_5_6_FreeCompressionContext(SZ_ZStd_v1_5_6_CompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern void SZ_ZStd_v1_5_6_FreeDecompressionContext(SZ_ZStd_v1_5_6_DecompressionContext* ctx);

            /* ===== Dictionary Handling ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern int SZ_ZStd_v1_5_6_CreateCompressionDict(out IntPtr dict, IntPtr dictBuffer, UIntPtr dictSize, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern int SZ_ZStd_v1_5_6_CreateDecompressionDict(out IntPtr dict, IntPtr dictBuffer, UIntPtr dictSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern void SZ_ZStd_v1_5_6_FreeCompressionDict(IntPtr dict);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern void SZ_ZStd_v1_5_6_FreeDecompressionDict(IntPtr dict);

            /* ===== Block Compression & Decompression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern UIntPtr SZ_ZStd_v1_5_6_CompressBlock(SZ_ZStd_v1_5_6_CompressionContext* ctx, IntPtr dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSize, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern UIntPtr SZ_ZStd_v1_5_6_DecompressBlock(SZ_ZStd_v1_5_6_DecompressionContext* ctx, IntPtr dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSize);

            /* ===== Streaming Compression (Updated) ===== */
            /* ===== Streaming Compression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern UIntPtr SZ_ZStd_v1_5_6_CompressStream(
                SZ_ZStd_v1_5_6_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcCapacity,
                out long inSize, out long outSize);

            /* ===== Streaming Compression Helpers ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern UIntPtr SZ_ZStd_v1_5_6_FlushStream(
                SZ_ZStd_v1_5_6_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity, out long outSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern UIntPtr SZ_ZStd_v1_5_6_EndStream(
                SZ_ZStd_v1_5_6_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity, out long outSize);


            /* ===== Streaming Decompression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern UIntPtr SZ_ZStd_v1_5_6_DecompressStream(SZ_ZStd_v1_5_6_DecompressionContext* ctx, byte* dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSizeout, out long inSize, out long outSize);

            /* ===== Configuration ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern int SZ_ZStd_v1_5_6_SetCompressionLevel(SZ_ZStd_v1_5_6_CompressionContext* ctx, int level);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern int SZ_ZStd_v1_5_6_SetBlockSize(SZ_ZStd_v1_5_6_CompressionContext* ctx, UIntPtr blockSize);

            /* ===== Recommended Buffer Sizes (New) ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern UIntPtr SZ_ZStd_v1_5_6_CStreamInSize();

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.StdCall)]
            public static extern UIntPtr SZ_ZStd_v1_5_6_CStreamOutSize();
        }
    }
}
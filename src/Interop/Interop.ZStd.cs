using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        /* ===== Compression Strategies & Directives ===== */
        public enum SZ_ZStd_v1_5_7_Strategy
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

        public enum SZ_ZStd_v1_5_7_EndDirective
        {
            Continue = 0,
            Flush = 1,
            End = 2
        }

        public enum SZ_ZStd_v1_5_7_ResetDirective
        {
            ResetSessionOnly = 1,
            ResetParameters = 2,
            ResetSessionAndParameters = 3
        }

        public enum SZ_ZStd_v1_5_7_CParameter
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

        public enum SZ_ZStd_v1_5_7_DParameter
        {
            DWindowLogMax = 100
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_Bounds
        {
            public UIntPtr error;
            public int lowerBound;
            public int upperBound;
        }

        /* ===== Context & Dictionary Structures ===== */
        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_CompressionContext
        {
            public IntPtr cctx; // Native compression context pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_DecompressionContext
        {
            public IntPtr dctx; // Native decompression context pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_CompressionDict
        {
            public IntPtr cdict; // Compression dictionary pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_DecompressionDict
        {
            public IntPtr ddict; // Decompression dictionary pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_InBuffer
        {
            public IntPtr src;
            public UIntPtr size;
            public UIntPtr pos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_OutBuffer
        {
            public IntPtr dst;
            public UIntPtr size;
            public UIntPtr pos;
        }

        /* ===== Native Interop Binding ===== */
        internal static unsafe partial class ZStd
        {
            /* ===== Compression Context Management ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_CreateCompressionContext(SZ_ZStd_v1_5_7_CompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_CreateDecompressionContext(SZ_ZStd_v1_5_7_DecompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_ZStd_v1_5_7_FreeCompressionContext(SZ_ZStd_v1_5_7_CompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_ZStd_v1_5_7_FreeDecompressionContext(SZ_ZStd_v1_5_7_DecompressionContext* ctx);

            /* ===== Dictionary Handling ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_CreateCompressionDict(out IntPtr dict, IntPtr dictBuffer, UIntPtr dictSize, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_CreateDecompressionDict(out IntPtr dict, IntPtr dictBuffer, UIntPtr dictSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_ZStd_v1_5_7_FreeCompressionDict(IntPtr dict);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_ZStd_v1_5_7_FreeDecompressionDict(IntPtr dict);

            /* ===== Block Compression & Decompression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_CompressBlock(SZ_ZStd_v1_5_7_CompressionContext* ctx, IntPtr dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSize, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_DecompressBlock(SZ_ZStd_v1_5_7_DecompressionContext* ctx, IntPtr dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSize);

            /* ===== Streaming Compression (Updated) ===== */
            /* ===== Streaming Compression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_CompressStream(
                SZ_ZStd_v1_5_7_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcCapacity,
                out long inSize, out long outSize);

            /* ===== Streaming Compression Helpers ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_FlushStream(
                SZ_ZStd_v1_5_7_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcCapacity,
                out long inSize, out long outSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_EndStream(
                SZ_ZStd_v1_5_7_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcCapacity,
                out long inSize, out long outSize);


            /* ===== Streaming Decompression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_DecompressStream(SZ_ZStd_v1_5_7_DecompressionContext* ctx, byte* dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSizeout, out long inSize, out long outSize);

            /* ===== Configuration ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_SetCompressionLevel(SZ_ZStd_v1_5_7_CompressionContext* ctx, int level);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_SetBlockSize(SZ_ZStd_v1_5_7_CompressionContext* ctx, UIntPtr blockSize);

            /* ===== Recommended Buffer Sizes (New) ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_CStreamInSize();

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_CStreamOutSize();
        }

        /* ===== Compression Strategies & Directives (v1_5_2) ===== */
        public enum SZ_ZStd_v1_5_2_Strategy
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

        public enum SZ_ZStd_v1_5_2_EndDirective
        {
            Continue = 0,
            Flush = 1,
            End = 2
        }

        public enum SZ_ZStd_v1_5_2_ResetDirective
        {
            ResetSessionOnly = 1,
            ResetParameters = 2,
            ResetSessionAndParameters = 3
        }

        public enum SZ_ZStd_v1_5_2_CParameter
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

        public enum SZ_ZStd_v1_5_2_DParameter
        {
            DWindowLogMax = 100
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_2_Bounds
        {
            public UIntPtr error;
            public int lowerBound;
            public int upperBound;
        }

        /* ===== Context & Dictionary Structures (v1_5_2) ===== */
        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_2_CompressionContext
        {
            public IntPtr cctx;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_2_DecompressionContext
        {
            public IntPtr dctx;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_2_CompressionDict
        {
            public IntPtr cdict;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_2_DecompressionDict
        {
            public IntPtr ddict;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_2_InBuffer
        {
            public IntPtr src;
            public UIntPtr size;
            public UIntPtr pos;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_2_OutBuffer
        {
            public IntPtr dst;
            public UIntPtr size;
            public UIntPtr pos;
        }

        /* ===== Native Interop Binding (v1_5_2) ===== */
        internal static unsafe partial class ZStd_v1_5_2
        {
            /* ===== Compression Context Management ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_2_CreateCompressionContext(SZ_ZStd_v1_5_2_CompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_2_CreateDecompressionContext(SZ_ZStd_v1_5_2_DecompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_ZStd_v1_5_2_FreeCompressionContext(SZ_ZStd_v1_5_2_CompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_ZStd_v1_5_2_FreeDecompressionContext(SZ_ZStd_v1_5_2_DecompressionContext* ctx);

            /* ===== Dictionary Handling ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_2_CreateCompressionDict(out IntPtr dict, IntPtr dictBuffer, UIntPtr dictSize, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_2_CreateDecompressionDict(out IntPtr dict, IntPtr dictBuffer, UIntPtr dictSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_ZStd_v1_5_2_FreeCompressionDict(IntPtr dict);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_ZStd_v1_5_2_FreeDecompressionDict(IntPtr dict);

            /* ===== Block Compression & Decompression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_2_CompressBlock(SZ_ZStd_v1_5_2_CompressionContext* ctx, IntPtr dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSize, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_2_DecompressBlock(SZ_ZStd_v1_5_2_DecompressionContext* ctx, IntPtr dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSize);

            /* ===== Streaming Compression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_2_CompressStream(
                SZ_ZStd_v1_5_2_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcCapacity,
                out long inSize, out long outSize);

            /* ===== Streaming Compression Helpers ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_2_FlushStream(
                SZ_ZStd_v1_5_2_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcCapacity,
                out long inSize, out long outSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_2_EndStream(
                SZ_ZStd_v1_5_2_CompressionContext* ctx,
                IntPtr dst, UIntPtr dstCapacity,
                byte* src, UIntPtr srcCapacity,
                out long inSize, out long outSize);

            /* ===== Streaming Decompression ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_2_DecompressStream(SZ_ZStd_v1_5_2_DecompressionContext* ctx, byte* dst, UIntPtr dstCapacity, byte* src, UIntPtr srcSizeout, out long inSize, out long outSize);

            /* ===== Configuration ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_2_SetCompressionLevel(SZ_ZStd_v1_5_2_CompressionContext* ctx, int level);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_2_SetBlockSize(SZ_ZStd_v1_5_2_CompressionContext* ctx, UIntPtr blockSize);

            /* ===== Recommended Buffer Sizes ===== */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_2_CStreamInSize();

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_2_CStreamOutSize();
        }
    }
}
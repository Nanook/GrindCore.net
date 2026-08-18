using System;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        /* ===== Seekable Compression Context Structures ===== */

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_SeekableCStream
        {
            public IntPtr zcs; // Native seekable compression stream pointer
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_ZStd_v1_5_7_Seekable
        {
            public IntPtr zs; // Native seekable decompression context pointer
        }

        /* ===== Native Interop Binding for Seekable ===== */
        internal static unsafe partial class ZStd
        {
            /* ===== Seekable Compression Context Management ===== */

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_Seekable_CreateCStream(SZ_ZStd_v1_5_7_SeekableCStream* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_Seekable_FreeCStream(SZ_ZStd_v1_5_7_SeekableCStream* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_InitCStream(
                SZ_ZStd_v1_5_7_SeekableCStream* ctx,
                int compressionLevel,
                int checksumFlag,
                uint maxFrameSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_CompressStream(
                SZ_ZStd_v1_5_7_SeekableCStream* ctx,
                void* dst,
                UIntPtr dstCapacity,
                void* src,
                UIntPtr srcCapacity,
                long* inSize,
                long* outSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_EndFrame(
                SZ_ZStd_v1_5_7_SeekableCStream* ctx,
                void* dst,
                UIntPtr dstCapacity,
                long* outSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_EndStream(
                SZ_ZStd_v1_5_7_SeekableCStream* ctx,
                void* dst,
                UIntPtr dstCapacity,
                long* outSize);

            /* ===== Seekable Decompression Context Management ===== */

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_Seekable_Create(SZ_ZStd_v1_5_7_Seekable* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_ZStd_v1_5_7_Seekable_Free(SZ_ZStd_v1_5_7_Seekable* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_InitBuff(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                void* src,
                UIntPtr srcSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_InitAdvanced(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                void* opaque,
                IntPtr readFunc,
                IntPtr seekFunc);

            /* ===== Seekable Decompression Operations ===== */

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_Decompress(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                void* dst,
                UIntPtr dstSize,
                ulong offset);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_DecompressFrame(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                void* dst,
                UIntPtr dstSize,
                uint frameIndex);

            /* ===== Seekable Query Functions ===== */

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern uint SZ_ZStd_v1_5_7_Seekable_GetNumFrames(SZ_ZStd_v1_5_7_Seekable* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_ZStd_v1_5_7_Seekable_GetFrameCompressedOffset(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                uint frameIndex);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_ZStd_v1_5_7_Seekable_GetFrameDecompressedOffset(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                uint frameIndex);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_GetFrameCompressedSize(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                uint frameIndex);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern UIntPtr SZ_ZStd_v1_5_7_Seekable_GetFrameDecompressedSize(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                uint frameIndex);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_ZStd_v1_5_7_Seekable_GetDecompressedSize(SZ_ZStd_v1_5_7_Seekable* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern uint SZ_ZStd_v1_5_7_Seekable_OffsetToFrameIndex(
                SZ_ZStd_v1_5_7_Seekable* ctx,
                ulong offset);
        }
    }
}

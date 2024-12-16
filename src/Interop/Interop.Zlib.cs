using Nanook.GrindCore;
using System.Runtime.InteropServices;
using System;
using Nanook.GrindCore.DeflateZLib;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {

#if NET7_0_OR_GREATER
        internal static partial class ZLib
        {
            static ZLib()
            {
                MultiplatformLoader.LoadLibrary(Libraries.GrindCoreLib);
            }


            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_DeflateInit2_")]
            internal static unsafe partial ZLibNative.ErrorCode DeflateInit2_(
                ZLibNative.ZStream* stream,
                ZLibNative.CompressionLevel level,
                ZLibNative.CompressionMethod method,
                int windowBits,
                int memLevel,
                ZLibNative.CompressionStrategy strategy);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Deflate")]
            internal static unsafe partial ZLibNative.ErrorCode Deflate(ZLibNative.ZStream* stream, ZLibNative.FlushCode flush);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_DeflateEnd")]
            internal static unsafe partial ZLibNative.ErrorCode DeflateEnd(ZLibNative.ZStream* stream);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_InflateInit2_")]
            internal static unsafe partial ZLibNative.ErrorCode InflateInit2_(ZLibNative.ZStream* stream, int windowBits);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Inflate")]
            internal static unsafe partial ZLibNative.ErrorCode Inflate(ZLibNative.ZStream* stream, ZLibNative.FlushCode flush);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_InflateEnd")]
            internal static unsafe partial ZLibNative.ErrorCode InflateEnd(ZLibNative.ZStream* stream);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Crc32")]
            internal static unsafe partial uint crc32(uint crc, byte* buffer, int len);


            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Compress")]
            internal static unsafe partial int Compress(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Compress2")]
            internal static unsafe partial int Compress2(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen,
                int level);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Compress3")]
            internal static unsafe partial int Compress3(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen,
                int level,
                int windowBits,
                int memLevel,
                int strategy);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Uncompress")]
            internal static unsafe partial int Uncompress(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Uncompress2")]
            internal static unsafe partial int Uncompress2(
                byte* dest,
                ref uint destLen,
                byte* source,
                ref uint sourceLen);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Uncompress3")]
            internal static unsafe partial int Uncompress3(
                byte* dest,
                ref uint destLen,
                byte* source,
                ref uint sourceLen);
        }
#else

        internal static unsafe partial class ZLib
        {
            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_DeflateInit2_")]
            internal static extern ZLibNative.ErrorCode DeflateInit2_(
                ZLibNative.ZStream* stream,
                ZLibNative.CompressionLevel level,
                ZLibNative.CompressionMethod method,
                int windowBits,
                int memLevel,
                ZLibNative.CompressionStrategy strategy);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Deflate")]
            internal static extern ZLibNative.ErrorCode Deflate(ZLibNative.ZStream* stream, ZLibNative.FlushCode flush);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_DeflateEnd")]
            internal static extern ZLibNative.ErrorCode DeflateEnd(ZLibNative.ZStream* stream);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_InflateInit2_")]
            internal static extern ZLibNative.ErrorCode InflateInit2_(ZLibNative.ZStream* stream, int windowBits);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Inflate")]
            internal static extern ZLibNative.ErrorCode Inflate(ZLibNative.ZStream* stream, ZLibNative.FlushCode flush);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_InflateEnd")]
            internal static extern ZLibNative.ErrorCode InflateEnd(ZLibNative.ZStream* stream);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Crc32")]
            internal static extern uint crc32(uint crc, byte* buffer, int len);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Compress")]
            internal static extern int Compress(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Compress2")]
            internal static extern int Compress2(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen,
                int level);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Compress3")]
            internal static extern int Compress3(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen,
                int level,
                int windowBits,
                int memLevel,
                int strategy);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Uncompress")]
            internal static extern int Uncompress(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Uncompress2")]
            internal static extern int Uncompress2(
                byte* dest,
                ref uint destLen,
                byte* source,
                ref uint sourceLen);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "GrindCore_Uncompress3")]
            internal static extern int Uncompress3(
                byte* dest,
                ref uint destLen,
                byte* source,
                ref uint sourceLen);
        }

#endif
    }
}
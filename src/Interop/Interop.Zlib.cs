using Nanook.GrindCore;
using System.Runtime.InteropServices;
using System;
using Nanook.GrindCore.DeflateZLib;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        internal static unsafe partial class ZLib
        {
            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_DeflateInit2_")]
            internal static extern ZLibNative.ErrorCode DeflateInit2_(
                ZLibNative.ZStream* stream,
                ZLibNative.CompressionLevel level,
                ZLibNative.CompressionMethod method,
                int windowBits,
                int memLevel,
                ZLibNative.CompressionStrategy strategy);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Deflate")]
            internal static extern ZLibNative.ErrorCode Deflate(ZLibNative.ZStream* stream, ZLibNative.FlushCode flush);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_DeflateEnd")]
            internal static extern ZLibNative.ErrorCode DeflateEnd(ZLibNative.ZStream* stream);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_InflateInit2_")]
            internal static extern ZLibNative.ErrorCode InflateInit2_(ZLibNative.ZStream* stream, int windowBits);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Inflate")]
            internal static extern ZLibNative.ErrorCode Inflate(ZLibNative.ZStream* stream, ZLibNative.FlushCode flush);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_InflateEnd")]
            internal static extern ZLibNative.ErrorCode InflateEnd(ZLibNative.ZStream* stream);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Crc32")]
            internal static extern uint crc32(uint crc, byte* buffer, int len);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Compress")]
            internal static extern int Compress(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Compress2")]
            internal static extern int Compress2(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen,
                int level);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Compress3")]
            internal static extern int Compress3(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen,
                int level,
                int windowBits,
                int memLevel,
                int strategy);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Uncompress")]
            internal static extern int Uncompress(
                byte* dest,
                ref uint destLen,
                byte* source,
                uint sourceLen);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Uncompress2")]
            internal static extern int Uncompress2(
                byte* dest,
                ref uint destLen,
                byte* source,
                ref uint sourceLen);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN8_ZLib_Uncompress3")]
            internal static extern int Uncompress3(
                byte* dest,
                ref uint destLen,
                byte* source,
                ref uint sourceLen);
        }
    }
}
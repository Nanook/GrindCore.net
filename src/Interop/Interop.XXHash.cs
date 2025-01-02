using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.XXHash
{
    [StructLayout(LayoutKind.Sequential)]
    public struct XXH32_CTX
    {
        public uint total_len_32; // XXH32_hash_t is assumed to be uint
        public uint large_len;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] v; // Accumulator lanes

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public uint[] mem32; // Internal buffer for partial reads

        public uint memsize;
        public uint reserved; // Reserved field
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XXH64_CTX
    {
        public ulong total_len; // Assuming similar structure for XXH64 with ulong for total length
        public ulong large_len;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public ulong[] v; // Accumulator lanes

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public ulong[] mem64; // Internal buffer for partial reads

        public ulong memsize;
        public ulong reserved; // Reserved field
    }

    internal static unsafe partial class XXHash
    {
        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SZ_XXH32_Reset(ref XXH32_CTX ctx);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SZ_XXH32_Update(ref XXH32_CTX ctx, byte* data, nuint len);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UInt32 SZ_XXH32_Digest(ref XXH32_CTX ctx);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SZ_XXH64_Reset(ref XXH64_CTX ctx);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SZ_XXH64_Update(ref XXH64_CTX ctx, byte* data, nuint len);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern UInt64 SZ_XXH64_Digest(ref XXH64_CTX ctx);
    }
}
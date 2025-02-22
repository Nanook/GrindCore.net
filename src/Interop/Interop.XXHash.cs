using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.XXHash
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct XXH32_CTX
    {
        public uint total_len_32; // XXH32_hash_t is assumed to be uint
        public uint large_len;
        public fixed uint v[4]; // Accumulator lanes
        public fixed uint mem32[4]; // Internal buffer for partial reads
        public uint memsize;
        public uint reserved; // Reserved field
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct XXH64_CTX
    {
        public ulong total_len; // Assuming similar structure for XXH64 with ulong for total length
        public ulong large_len;
        public fixed ulong v[4]; // Accumulator lanes
        public fixed ulong mem64[4]; // Internal buffer for partial reads
        public ulong memsize;
        public ulong reserved; // Reserved field
    }

    internal static unsafe partial class XXHash
    {
        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SZ_XXH32_Reset(XXH32_CTX* ctx);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SZ_XXH32_Update(XXH32_CTX* ctx, byte* data, nuint len);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern uint SZ_XXH32_Digest(XXH32_CTX* ctx);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SZ_XXH64_Reset(XXH64_CTX* ctx);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern void SZ_XXH64_Update(XXH64_CTX* ctx, byte* data, nuint len);

        [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
        public static extern ulong SZ_XXH64_Digest(XXH64_CTX* ctx);
    }
}

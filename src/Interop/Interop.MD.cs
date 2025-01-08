using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct MD2_CTX
        {
            public ulong len;
            public fixed byte data[16];
            public fixed byte checksum[16];
            public fixed byte state[16];
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct MD4_CTX
        {
            public fixed uint sz[2];
            public fixed uint counter[4];
            public fixed byte save[64];
        }

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct MD5_CTX
        {
            public fixed uint sz[2];
            public fixed uint counter[4];
            public fixed byte save[64];
        }

        internal static unsafe partial class MD
        {
            // Libraries.GrindCoreLib should be defined as a constant string pointing to the name of your native library
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD2_Init(MD2_CTX* m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD2_Update(MD2_CTX* m, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD2_Final(byte* res, MD2_CTX* m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD4_Init(MD4_CTX* m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD4_Update(MD4_CTX* m, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD4_Final(byte* res, MD4_CTX* m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD5_Init(MD5_CTX* m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD5_Update(MD5_CTX* m, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD5_Final(byte* res, MD5_CTX* m);
        }
    }
}

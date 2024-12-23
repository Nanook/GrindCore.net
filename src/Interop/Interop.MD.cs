using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MD2_CTX
        {
            public ulong len;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] data;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] checksum;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] state;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MD4_CTX
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] sz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public uint[] counter;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] save;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MD5_CTX
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public uint[] sz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public uint[] counter;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] save;
        }

        internal static unsafe partial class MD
        {
            // Libraries.GrindCoreLib should be defined as a constant string pointing to the name of your native library
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD2_Init(ref MD2_CTX m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD2_Update(ref MD2_CTX m, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD2_Final(byte* res, ref MD2_CTX m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD4_Init(ref MD4_CTX m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD4_Update(ref MD4_CTX m, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD4_Final(byte* res, ref MD4_CTX m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD5_Init(ref MD5_CTX m);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD5_Update(ref MD5_CTX m, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_MD5_Final(byte* res, ref MD5_CTX m);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        public const int SHA3_256_DIGEST_LENGTH = 32;
        public const int SHA3_384_DIGEST_LENGTH = 48;
        public const int SHA3_512_DIGEST_LENGTH = 64;

        // SHA3_KECCAK_SPONGE_WORDS is based on uint64_t (ulong in C#)
        public const int SHA3_KECCAK_SPONGE_WORDS = 200 / sizeof(ulong);

        [StructLayout(LayoutKind.Sequential)]
        public struct SHA3_CTX
        {
            public ulong saved;
            public ulong byteIndex;
            public ulong wordIndex;
            public ulong capacityWords;

            [StructLayout(LayoutKind.Explicit)]
            public struct KeccakState
            {
                [FieldOffset(0)]
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = SHA3_KECCAK_SPONGE_WORDS)]
                public ulong[] s;

                [FieldOffset(0)]
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = SHA3_KECCAK_SPONGE_WORDS * 8)]
                public byte[] sb;
            }

            public KeccakState u;
            public uint digest_length;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHA384_CTX
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public ulong[] sz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public ulong[] counter;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] save;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHA512_CTX
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public ulong[] sz;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public ulong[] counter;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] save;
        }


        internal static unsafe partial class SHA
        {
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Init(ref SHA3_CTX ctx, uint bitSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Update(ref SHA3_CTX ctx, byte* bufIn, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Final(byte* res, ref SHA3_CTX ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Init(ref SHA384_CTX ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Update(ref SHA384_CTX ctx, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Final(byte* res, ref SHA384_CTX ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Init(ref SHA512_CTX ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Update(ref SHA512_CTX ctx, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Final(byte* res, ref SHA512_CTX ctx);
        }
    }
}

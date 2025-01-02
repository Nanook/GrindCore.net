using System;
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

        public const int SHA1_NUM_BLOCK_WORDS = 16;
        public const int SHA1_NUM_DIGEST_WORDS = 5;
        public const int SHA1_BLOCK_SIZE = SHA1_NUM_BLOCK_WORDS * 4;
        public const int SHA1_DIGEST_SIZE = SHA1_NUM_DIGEST_WORDS * 4;

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct CSha1
        {
            public IntPtr func_UpdateBlocks;
            public ulong count;
            public fixed ulong _pad_2[2];
            public fixed uint state[SHA1_NUM_DIGEST_WORDS];
            public fixed uint _pad_3[3];
            public fixed byte buffer[SHA1_BLOCK_SIZE];
        }

        public const int SHA256_NUM_BLOCK_WORDS = 16;
        public const int SHA256_NUM_DIGEST_WORDS = 8;
        public const int SHA256_BLOCK_SIZE = SHA256_NUM_BLOCK_WORDS * 4;
        public const int SHA256_DIGEST_SIZE = SHA256_NUM_DIGEST_WORDS * 4;

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct CSha256
        {
            public IntPtr func_UpdateBlocks;
            public ulong count;
            public fixed ulong pad_2[2];
            public fixed uint state[SHA256_NUM_DIGEST_WORDS];
            public fixed byte buffer[SHA256_BLOCK_SIZE];
        }

        internal static unsafe partial class SHA
        {
            // SHA1
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Sha1_SetFunction(ref CSha1 p, uint algo);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_Init(ref CSha1 p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_InitState(ref CSha1 p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_Update(ref CSha1 p, byte* data, nuint size);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_Final(ref CSha1 p, byte* digest);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_PrepareBlock(ref CSha1 p, byte* block, uint size);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_GetBlockDigest(ref CSha1 p, byte* data, byte* destDigest);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1Prepare();

            // SHA2 256
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Sha256_SetFunction(ref CSha256 p, uint algo);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256_InitState(ref CSha256 p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256_Init(ref CSha256 p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256_Update(ref CSha256 p, byte* data, nuint size);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256_Final(ref CSha256 p, byte* digest);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256Prepare();

            // SHA2 384
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Init(ref SHA384_CTX ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Update(ref SHA384_CTX ctx, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Final(byte* res, ref SHA384_CTX ctx);

            // SHA2 512
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Init(ref SHA512_CTX ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Update(ref SHA512_CTX ctx, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Final(byte* res, ref SHA512_CTX ctx);

            // SHA3
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Init(ref SHA3_CTX ctx, uint bitSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Update(ref SHA3_CTX ctx, byte* bufIn, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Final(byte* res, ref SHA3_CTX ctx);
        }
    }
}

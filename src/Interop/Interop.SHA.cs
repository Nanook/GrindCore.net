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
        public unsafe struct SHA3_CTX
        {
            public ulong saved;
            public ulong byteIndex;
            public ulong wordIndex;
            public ulong capacityWords;

            [StructLayout(LayoutKind.Explicit)]
            public unsafe struct KeccakState
            {
                [FieldOffset(0)]
                public fixed ulong s[SHA3_KECCAK_SPONGE_WORDS];

                [FieldOffset(0)]
                public fixed byte sb[SHA3_KECCAK_SPONGE_WORDS * 8];
            }

            public KeccakState u;
            public uint digest_length;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct SHA384_CTX
        {
            public fixed ulong sz[2];
            public fixed ulong counter[8];
            public fixed byte save[128];
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct SHA512_CTX
        {
            public fixed ulong sz[2];
            public fixed ulong counter[8];
            public fixed byte save[128];
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
            public static extern int SZ_Sha1_SetFunction(CSha1* p, uint algo);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_Init(CSha1* p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_InitState(CSha1* p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_Update(CSha1* p, byte* data, nuint size);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_Final(CSha1* p, byte* digest);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_PrepareBlock(CSha1* p, byte* block, uint size);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1_GetBlockDigest(CSha1* p, byte* data, byte* destDigest);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha1Prepare();

            // SHA2 256
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Sha256_SetFunction(CSha256* p, uint algo);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256_InitState(CSha256* p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256_Init(CSha256* p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256_Update(CSha256* p, byte* data, nuint size);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256_Final(CSha256* p, byte* digest);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Sha256Prepare();

            // SHA2 384
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Init(SHA384_CTX* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Update(SHA384_CTX* ctx, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA384_Final(byte* res, SHA384_CTX* ctx);

            // SHA2 512
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Init(SHA512_CTX* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Update(SHA512_CTX* ctx, byte* p, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA512_Final(byte* res, SHA512_CTX* ctx);

            // SHA3
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Init(SHA3_CTX* ctx, uint bitSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Update(SHA3_CTX* ctx, byte* bufIn, nuint len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_SHA3_Final(byte* res, SHA3_CTX* ctx);
        }
    }
}

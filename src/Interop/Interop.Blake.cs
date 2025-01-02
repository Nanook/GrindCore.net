using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        //#define BLAKE3_VERSION_STRING "0.3.7"
        //#define BLAKE3_KEY_LEN 32
        public const int BLAKE3_OUT_LEN = 32;
        //#define BLAKE3_BLOCK_LEN 64
        //#define BLAKE3_CHUNK_LEN 1024
        public const int BLAKE3_MAX_DEPTH = 54;

        [StructLayout(LayoutKind.Sequential)]
        public struct Blake3Hasher
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public uint[] key;
            public Blake3ChunkState chunk;
            public byte cv_stack_len;
            // The stack size is MAX_DEPTH + 1 because we do lazy merging. For example,
            // with 7 chunks, we have 3 entries in the stack. Adding an 8th chunk
            // requires a 4th entry, rather than merging everything down to 1, because we
            // don't know whether more input is coming. This is different from how the
            // reference implementation does things.
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = (BLAKE3_MAX_DEPTH + 1) * BLAKE3_OUT_LEN)]
            public byte[] cv_stack;
        }

        // Define the Blake3ChunkState struct here
        public struct Blake3ChunkState
        {
            // Define the members of this struct based on your C definition
        }

        public const int BLAKE2S_BLOCK_SIZE = 64;
        public const int BLAKE2S_DIGEST_SIZE = 32;
        public const int BLAKE2SP_PARALLEL_DEGREE = 8;
        public const int BLAKE2SP_NUM_STRUCT_WORDS = 16;

        [StructLayout(LayoutKind.Sequential, Pack = 64)]
        public unsafe struct CBlake2sp
        {
            public Union u;
            public fixed uint states[BLAKE2SP_PARALLEL_DEGREE * BLAKE2SP_NUM_STRUCT_WORDS];
            public fixed uint buf32[BLAKE2SP_PARALLEL_DEGREE * BLAKE2SP_NUM_STRUCT_WORDS * 2];

            [StructLayout(LayoutKind.Explicit)]
            public struct Union
            {
                [FieldOffset(0)]
                public fixed byte _pad_align_ptr[64];

                [FieldOffset(0)]
                public fixed uint _pad_align_32bit[16];

                [FieldOffset(0)]
                public HeaderStruct header;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct HeaderStruct
            {
                public uint cycPos;
                public uint _pad_unused;
                public IntPtr func_Compress_Fast;
                public IntPtr func_Compress_Single;
                public IntPtr func_Init;
                public IntPtr func_Final;
            }
        }


        internal static unsafe partial class Blake
        {
            // Blake2sp
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Blake2sp_SetFunction(ref CBlake2sp p, uint algo);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Blake2sp_Init(ref CBlake2sp p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Blake2sp_InitState(ref CBlake2sp p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Blake2sp_Update(ref CBlake2sp p, byte* data, ulong size);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Blake2sp_Final(ref CBlake2sp p, byte* digest);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void z7_Black2sp_Prepare();

            // Blake3
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern nint SZ_blake3_version();

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_blake3_hasher_init(ref Blake3Hasher self);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_blake3_hasher_init_keyed(ref Blake3Hasher self, byte[] key);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_blake3_hasher_init_derive_key(ref Blake3Hasher self, string context);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_blake3_hasher_init_derive_key_raw(ref Blake3Hasher self, nint context, nuint context_len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_blake3_hasher_update(ref Blake3Hasher self, nint input, nuint input_len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_blake3_hasher_finalize(ref Blake3Hasher self, byte[] output, UIntPtr output_len);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_blake3_hasher_finalize_seek(ref Blake3Hasher self, ulong seek, byte[] output, nuint output_len);

        }
    }
}

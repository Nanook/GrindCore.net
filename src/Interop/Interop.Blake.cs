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

        internal static unsafe partial class Blake
        {
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

using System;
using System.Runtime.InteropServices;
using Nanook.GrindCore.Brotli;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        internal enum BOOL : int
        {
            FALSE = 0,
            TRUE = 1,
        }

        internal static unsafe partial class Brotli
        {
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern SafeBrotliDecoderHandle DN9_BRT_v1_1_0_BrotliDecoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int DN9_BRT_v1_1_0_BrotliDecoderDecompressStream(
                SafeBrotliDecoderHandle state, ref UIntPtr availableIn, byte** nextIn,
                ref UIntPtr availableOut, byte** nextOut, out UIntPtr totalOut);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_BrotliDecoderDecompress(UIntPtr availableInput, byte* inBytes, UIntPtr* availableOutput, byte* outBytes);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void DN9_BRT_v1_1_0_BrotliDecoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_BrotliDecoderIsFinished(SafeBrotliDecoderHandle state);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern SafeBrotliEncoderHandle DN9_BRT_v1_1_0_BrotliEncoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_BrotliEncoderSetParameter(SafeBrotliEncoderHandle state, BrotliEncoderParameter parameter, uint value);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_BrotliEncoderCompressStream(
                SafeBrotliEncoderHandle state, BrotliEncoderOperation op, ref UIntPtr availableIn,
                byte** nextIn, ref UIntPtr availableOut, byte** nextOut, out UIntPtr totalOut);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_BrotliEncoderHasMoreOutput(SafeBrotliEncoderHandle state);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void DN9_BRT_v1_1_0_BrotliEncoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_BrotliEncoderCompress(int quality, int window, int v, UIntPtr availableInput, byte* inBytes, UIntPtr* availableOutput, byte* outBytes);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UIntPtr DN9_BRT_v1_1_0_BrotliEncoderMaxCompressedSize(UIntPtr inputSize);
        }
    }
}

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
            internal static extern SafeBrotliDecoderHandle DN9_BRT_v1_1_0_DecoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern int DN9_BRT_v1_1_0_DecoderDecompressStream(
                SafeBrotliDecoderHandle state, ref UIntPtr availableIn, byte** nextIn,
                ref UIntPtr availableOut, byte** nextOut, out UIntPtr totalOut);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_DecoderDecompress(UIntPtr availableInput, byte* inBytes, UIntPtr* availableOutput, byte* outBytes);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void DN9_BRT_v1_1_0_DecoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_DecoderIsFinished(SafeBrotliDecoderHandle state);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern SafeBrotliEncoderHandle DN9_BRT_v1_1_0_EncoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_EncoderSetParameter(SafeBrotliEncoderHandle state, BrotliEncoderParameter parameter, uint value);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_EncoderCompressStream(
                SafeBrotliEncoderHandle state, BrotliEncoderOperation op, ref UIntPtr availableIn,
                byte** nextIn, ref UIntPtr availableOut, byte** nextOut, out UIntPtr totalOut);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_EncoderHasMoreOutput(SafeBrotliEncoderHandle state);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern void DN9_BRT_v1_1_0_EncoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern BOOL DN9_BRT_v1_1_0_EncoderCompress(int quality, int window, int v, UIntPtr availableInput, byte* inBytes, UIntPtr* availableOutput, byte* outBytes);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            internal static extern UIntPtr DN9_BRT_v1_1_0_EncoderMaxCompressedSize(UIntPtr inputSize);
        }
    }
}

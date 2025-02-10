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
            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliDecoderCreateInstance")]
            internal static extern SafeBrotliDecoderHandle DN9_BRT_v1_1_0_DecoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliDecoderDecompressStream")]
            internal static extern int DN9_BRT_v1_1_0_DecoderDecompressStream(
                SafeBrotliDecoderHandle state, ref nuint availableIn, byte** nextIn,
                ref nuint availableOut, byte** nextOut, out nuint totalOut);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliDecoderDecompress")]
            internal static extern BOOL DN9_BRT_v1_1_0_DecoderDecompress(nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliDecoderDestroyInstance")]
            internal static extern void DN9_BRT_v1_1_0_DecoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliDecoderIsFinished")]
            internal static extern BOOL DN9_BRT_v1_1_0_DecoderIsFinished(SafeBrotliDecoderHandle state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliEncoderCreateInstance")]
            internal static extern SafeBrotliEncoderHandle DN9_BRT_v1_1_0_EncoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliEncoderSetParameter")]
            internal static extern BOOL DN9_BRT_v1_1_0_EncoderSetParameter(SafeBrotliEncoderHandle state, BrotliEncoderParameter parameter, uint value);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliEncoderCompressStream")]
            internal static extern BOOL DN9_BRT_v1_1_0_EncoderCompressStream(
                SafeBrotliEncoderHandle state, BrotliEncoderOperation op, ref nuint availableIn,
                byte** nextIn, ref nuint availableOut, byte** nextOut, out nuint totalOut);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliEncoderHasMoreOutput")]
            internal static extern BOOL DN9_BRT_v1_1_0_EncoderHasMoreOutput(SafeBrotliEncoderHandle state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliEncoderDestroyInstance")]
            internal static extern void DN9_BRT_v1_1_0_EncoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliEncoderCompress")]
            internal static extern BOOL DN9_BRT_v1_1_0_EncoderCompress(int quality, int window, int v, nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "DN9_BRT_v1_1_0_BrotliEncoderMaxCompressedSize")]
            internal static extern nuint DN9_BRT_v1_1_0_EncoderMaxCompressedSize(nuint inputSize);
        }
    }
}




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
            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderCreateInstance")]
            internal static extern SafeBrotliDecoderHandle DN8_Brotli_v1_0_9_DecoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDecompressStream")]
            internal static extern int DN8_Brotli_v1_0_9_DecoderDecompressStream(
                SafeBrotliDecoderHandle state, ref nuint availableIn, byte** nextIn,
                ref nuint availableOut, byte** nextOut, out nuint totalOut);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDecompress")]
            internal static extern BOOL DN8_Brotli_v1_0_9_DecoderDecompress(nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDestroyInstance")]
            internal static extern void DN8_Brotli_v1_0_9_DecoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderIsFinished")]
            internal static extern BOOL DN8_Brotli_v1_0_9_DecoderIsFinished(SafeBrotliDecoderHandle state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCreateInstance")]
            internal static extern SafeBrotliEncoderHandle DN8_Brotli_v1_0_9_EncoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderSetParameter")]
            internal static extern BOOL DN8_Brotli_v1_0_9_EncoderSetParameter(SafeBrotliEncoderHandle state, BrotliEncoderParameter parameter, uint value);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCompressStream")]
            internal static extern BOOL DN8_Brotli_v1_0_9_EncoderCompressStream(
                SafeBrotliEncoderHandle state, BrotliEncoderOperation op, ref nuint availableIn,
                byte** nextIn, ref nuint availableOut, byte** nextOut, out nuint totalOut);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderHasMoreOutput")]
            internal static extern BOOL DN8_Brotli_v1_0_9_EncoderHasMoreOutput(SafeBrotliEncoderHandle state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderDestroyInstance")]
            internal static extern void DN8_Brotli_v1_0_9_EncoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCompress")]
            internal static extern BOOL DN8_Brotli_v1_0_9_EncoderCompress(int quality, int window, int v, nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);

        }
    }
}

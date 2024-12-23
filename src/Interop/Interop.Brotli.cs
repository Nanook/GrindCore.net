


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
            internal static extern SafeBrotliDecoderHandle BrotliDecoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDecompressStream")]
            internal static extern int BrotliDecoderDecompressStream(
                SafeBrotliDecoderHandle state, ref nuint availableIn, byte** nextIn,
                ref nuint availableOut, byte** nextOut, out nuint totalOut);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDecompress")]
            internal static extern BOOL BrotliDecoderDecompress(nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDestroyInstance")]
            internal static extern void BrotliDecoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderIsFinished")]
            internal static extern BOOL BrotliDecoderIsFinished(SafeBrotliDecoderHandle state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCreateInstance")]
            internal static extern SafeBrotliEncoderHandle BrotliEncoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderSetParameter")]
            internal static extern BOOL BrotliEncoderSetParameter(SafeBrotliEncoderHandle state, BrotliEncoderParameter parameter, uint value);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCompressStream")]
            internal static extern BOOL BrotliEncoderCompressStream(
                SafeBrotliEncoderHandle state, BrotliEncoderOperation op, ref nuint availableIn,
                byte** nextIn, ref nuint availableOut, byte** nextOut, out nuint totalOut);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderHasMoreOutput")]
            internal static extern BOOL BrotliEncoderHasMoreOutput(SafeBrotliEncoderHandle state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderDestroyInstance")]
            internal static extern void BrotliEncoderDestroyInstance(IntPtr state);

            [DllImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCompress")]
            internal static extern BOOL BrotliEncoderCompress(int quality, int window, int v, nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);
        }
    }
}

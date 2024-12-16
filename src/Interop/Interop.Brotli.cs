


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

#if NET7_0_OR_GREATER
        internal static partial class Brotli
        {
            static Brotli()
            {
                MultiplatformLoader.LoadLibrary(Libraries.GrindCoreLib);
            }


            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderCreateInstance")]
            internal static partial SafeBrotliDecoderHandle BrotliDecoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDecompressStream")]
            internal static unsafe partial int BrotliDecoderDecompressStream(
                SafeBrotliDecoderHandle state, ref nuint availableIn, byte** nextIn,
                ref nuint availableOut, byte** nextOut, out nuint totalOut);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDecompress")]
            internal static unsafe partial BOOL BrotliDecoderDecompress(nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderDestroyInstance")]
            internal static partial void BrotliDecoderDestroyInstance(IntPtr state);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliDecoderIsFinished")]
            internal static partial BOOL BrotliDecoderIsFinished(SafeBrotliDecoderHandle state);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCreateInstance")]
            internal static partial SafeBrotliEncoderHandle BrotliEncoderCreateInstance(IntPtr allocFunc, IntPtr freeFunc, IntPtr opaque);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderSetParameter")]
            internal static partial BOOL BrotliEncoderSetParameter(SafeBrotliEncoderHandle state, BrotliEncoderParameter parameter, uint value);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCompressStream")]
            internal static unsafe partial BOOL BrotliEncoderCompressStream(
                SafeBrotliEncoderHandle state, BrotliEncoderOperation op, ref nuint availableIn,
                byte** nextIn, ref nuint availableOut, byte** nextOut, out nuint totalOut);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderHasMoreOutput")]
            internal static partial BOOL BrotliEncoderHasMoreOutput(SafeBrotliEncoderHandle state);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderDestroyInstance")]
            internal static partial void BrotliEncoderDestroyInstance(IntPtr state);

            [LibraryImport(Libraries.GrindCoreLib, EntryPoint = "BrotliEncoderCompress")]
            internal static unsafe partial BOOL BrotliEncoderCompress(int quality, int window, int v, nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);
        }
#else
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
#endif
  }
}
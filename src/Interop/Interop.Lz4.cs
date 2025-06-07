using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        // LZ4 Error Codes
        public const int SZ_Lz4_v1_9_4_OK = 0;
        public const int SZ_Lz4_v1_9_4_ERROR = -1;
        public const int SZ_Lz4_v1_9_4_MEMERROR = -2;
        public const int SZ_Lz4_v1_9_4_COMPRESSFAIL = -3;
        public const int SZ_Lz4_v1_9_4_DECOMPRESSFAIL = -4;

        // LZ4 Stream structure representation
        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_Lz4_v1_9_4_Stream
        {
            public IntPtr internalState;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_Lz4F_v1_9_4_CompressionContext
        {
            public IntPtr internalState;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SZ_Lz4F_v1_9_4_DecompressionContext
        {
            public IntPtr internalState;
        }

        public enum LZ4F_blockSizeID_t : uint
        {
            LZ4F_default = 0,
            LZ4F_max64KB = 4,
            LZ4F_max256KB = 5,
            LZ4F_max1MB = 6,
            LZ4F_max4MB = 7
        }

        public enum LZ4F_contentChecksum_t : uint
        {
            LZ4F_noContentChecksum = 0,
            LZ4F_contentChecksumEnabled = 1
        }

        public enum LZ4F_blockMode_t : uint
        {
            LZ4F_blockLinked = 0,
            LZ4F_blockIndependent = 1
        }

        public enum LZ4F_frameType_t : uint
        {
            LZ4F_frame = 0,
            LZ4F_skippableFrame = 1
        }

        public enum LZ4F_blockChecksum_t : uint
        {
            LZ4F_noBlockChecksum = 0,
            LZ4F_blockChecksumEnabled = 1
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LZ4F_frameInfo_t
        {
            public LZ4F_blockSizeID_t blockSizeID;
            public LZ4F_blockMode_t blockMode;
            public LZ4F_contentChecksum_t contentChecksumFlag;
            public LZ4F_frameType_t frameType;
            public ulong contentSize;
            public uint dictID;
            public LZ4F_blockChecksum_t blockChecksumFlag;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LZ4F_preferences_t
        {
            public LZ4F_frameInfo_t frameInfo;
            public int compressionLevel;
            public uint autoFlush;
            public uint favorDecSpeed;
            public uint reserved1;
            public uint reserved2;
        }

        internal static unsafe partial class Lz4
        {
            public static int SZ_Lz4_v1_9_4_CompressBound(int inputSize)
            {
                return (inputSize > 0x7E000000) ? 0 : inputSize + (inputSize / 255) + 16;
            }

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_Init(ref SZ_Lz4_v1_9_4_Stream stream);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lz4_v1_9_4_End(ref SZ_Lz4_v1_9_4_Stream stream);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lz4_v1_9_4_ResetStream(ref SZ_Lz4_v1_9_4_Stream stream);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lz4_v1_9_4_TransferStateToPalLZ4Stream(
                ref SZ_Lz4_v1_9_4_Stream from, ref SZ_Lz4_v1_9_4_Stream to);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr SZ_Lz4_v1_9_4_GetCurrentLZ4Stream(ref SZ_Lz4_v1_9_4_Stream stream);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_CompressFastContinue(
                ref SZ_Lz4_v1_9_4_Stream stream,
                byte* src,
                IntPtr dst,
                int srcSize,
                int dstCapacity,
                int acceleration);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_CompressPartial(
                ref SZ_Lz4_v1_9_4_Stream stream,
                byte* src,
                IntPtr dst,
                int srcSize,
                int targetSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_DecompressSafeContinue(
                ref SZ_Lz4_v1_9_4_Stream stream,
                byte* src,
                byte* dst,
                int compressedSize,
                int dstCapacity);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_DecompressUsingDict(
                ref SZ_Lz4_v1_9_4_Stream stream,
                byte* src,
                byte* dst,
                int srcSize,
                int dstCapacity,
                IntPtr dictStart,
                int dictSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_DecompressPartialUsingDict(
                ref SZ_Lz4_v1_9_4_Stream stream,
                byte* src,
                byte* dst,
                int compressedSize,
                int targetOutputSize,
                int maxOutputSize,
                IntPtr dictStart,
                int dictSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_LoadDict(
                ref SZ_Lz4_v1_9_4_Stream stream,
                IntPtr dictionary,
                int dictSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_SaveDict(
                ref SZ_Lz4_v1_9_4_Stream stream,
                IntPtr safeBuffer,
                int maxDictSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_AttachDict(
                ref SZ_Lz4_v1_9_4_Stream stream,
                ref SZ_Lz4_v1_9_4_Stream dictStream);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_Flush(ref SZ_Lz4_v1_9_4_Stream stream);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_CompressHC(byte* src, IntPtr dst, int srcSize,  int dstCapacity, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_CompressHC_ExtState(IntPtr stateHC, byte* src, IntPtr dst, int srcSize, int maxDstSize, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4_v1_9_4_CompressHC_DestSize( IntPtr stateHC, byte* src, IntPtr dst, ref int srcSizePtr, int targetDstSize, int compressionLevel);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_Lz4F_v1_9_4_CompressFrameBound(ulong srcSize, IntPtr prefsPtr);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_Lz4F_v1_9_4_CompressBound(ulong srcSize, IntPtr prefsPtr);

            /* Compression Context Management */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4F_v1_9_4_CreateCompressionContext(SZ_Lz4F_v1_9_4_CompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lz4F_v1_9_4_FreeCompressionContext(SZ_Lz4F_v1_9_4_CompressionContext* ctx);

            /* Frame-Based Compression Functions */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_Lz4F_v1_9_4_CompressBegin(SZ_Lz4F_v1_9_4_CompressionContext* ctx, IntPtr dstBuffer, ulong dstCapacity, LZ4F_preferences_t* prefsPtr);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_Lz4F_v1_9_4_CompressUpdate(SZ_Lz4F_v1_9_4_CompressionContext* ctx, IntPtr dstBuffer, ulong dstCapacity, byte* srcBuffer, ulong srcSize, IntPtr cOptPtr);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_Lz4F_v1_9_4_Flush(SZ_Lz4F_v1_9_4_CompressionContext* ctx, IntPtr dstBuffer, ulong dstCapacity, IntPtr cOptPtr);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_Lz4F_v1_9_4_CompressEnd(SZ_Lz4F_v1_9_4_CompressionContext* ctx, IntPtr dstBuffer, ulong dstCapacity, IntPtr cOptPtr);

            /* Decompression Context Management */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4F_v1_9_4_CreateDecompressionContext(SZ_Lz4F_v1_9_4_DecompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lz4F_v1_9_4_FreeDecompressionContext(SZ_Lz4F_v1_9_4_DecompressionContext* ctx);

            /* Frame-Based Decompression Functions */
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_Lz4F_v1_9_4_GetFrameInfo(SZ_Lz4F_v1_9_4_DecompressionContext* ctx, IntPtr frameInfoPtr, byte* srcBuffer, ref ulong srcSizePtr);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern ulong SZ_Lz4F_v1_9_4_Decompress(SZ_Lz4F_v1_9_4_DecompressionContext* ctx, byte* dstBuffer, ref ulong dstSizePtr, byte* srcBuffer, ref ulong srcSizePtr, IntPtr dOptPtr);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lz4F_v1_9_4_ResetDecompressionContext(SZ_Lz4F_v1_9_4_DecompressionContext* ctx);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lz4F_v1_9_4_CompressHC_Stream(SZ_Lz4F_v1_9_4_CompressionContext* ctx, IntPtr dstBuffer, ulong dstCapacity, byte* srcBuffer, int srcSize, int compressionLevel, IntPtr cOptPtr);
        }
    }

}

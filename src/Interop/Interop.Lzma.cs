using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore
{
    internal static partial class Interop
    {
        public const int LZMA_REQUIRED_INPUT_MAX = 20;

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct CLzma2EncProps
        {
            public CLzmaEncProps lzmaProps;
            public ulong blockSize;
            public int numBlockThreads_Reduced;
            public int numBlockThreads_Max;
            public int numTotalThreads;
            public uint numThreadGroups;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct CBufferInStream
        {
            public IntPtr vt;
            public IntPtr buffer;     // Pointer to the byte _outBuffer
            public ulong size;        // Total size of the _outBuffer
            public ulong pos;         // Current position in the _outBuffer
            public ulong remaining;   // Remaining bytes in the _outBuffer
            public ulong processed;   // Total bytes processed (running total)
            public int finished;
            public int count;
            public UIntPtr lastSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct CLzmaEncProps
        {
            public int level;       /* 0 <= level <= 9 */
            public uint dictSize;   /* (1 << 12) <= dictSize <= (1 << 27) for 32-bit version
                                       (1 << 12) <= dictSize <= (3 << 29) for 64-bit version
                                       default = (1 << 24) */
            public int lc;          /* 0 <= lc <= 8, default = 3 */
            public int lp;          /* 0 <= lp <= 4, default = 0 */
            public int pb;          /* 0 <= pb <= 4, default = 2 */
            public int algo;        /* 0 - fast, 1 - normal, default = 1 */
            public int fb;          /* 5 <= fb <= 273, default = 32 */
            public int btMode;      /* 0 - hashChain Mode, 1 - binTree mode - normal, default = 1 */
            public int numHashBytes; /* 2, 3 or 4, default = 4 */
            public uint numHashOutBits; /* default = ? */
            public uint mc;         /* 1 <= mc <= (1 << 30), default = 32 */
            public uint writeEndMark;  /* 0 - do not write EOPM, 1 - write EOPM, default = 0 */
            public int numThreads;  /* 1 or 2, default = 2 */

            public int affinityGroup;

            public ulong reduceSize; /* estimated size of data that will be compressed. default = (UInt64)(Int64)-1.
                                 Encoder uses this value to reduce dictionary size */

            public ulong affinity;

            public ulong affinityInGroup;
        }

        //[StructLayout(LayoutKind.Sequential)]
        //public struct CLzma2Enc
        //{
        //    public byte propEncoded;
        //    public CLzma2EncProps props;
        //    public ulong expectedDataSize;
        //    public IntPtr tempBufLzma; // Byte pointer
        //    public IntPtr alloc; // ISzAllocPtr
        //    public IntPtr allocBig; // ISzAllocPtr
        //                            // Other members omitted for simplicity
        //}

        [StructLayout(LayoutKind.Sequential)]
        public struct CLzma2Dec
        {
            public uint state;
            public byte control;
            public byte needInitLevel;
            public byte isExtraMode;
            public byte _pad_;
            public uint packSize;
            public uint unpackSize;
            public CLzmaDec decoder;
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct CLzmaDec
        {
            public CLzmaProps prop;                  // Nested struct
            public IntPtr probs;                     // CLzmaProb* in C maps to IntPtr in C#
            public IntPtr probs_1664;                // CLzmaProb* in C maps to IntPtr in C#
            public byte* dic;                        // Byte* in C maps to byte* in C#
            public UIntPtr dicBufSize;               // SizeT in C maps to UIntPtr in C#
            public UIntPtr dicPos;                   // SizeT in C maps to UIntPtr in C#
            public IntPtr buf;                       // const Byte* in C maps to IntPtr in C#
            public uint range;                       // UInt32 in C maps to uint in C#
            public uint code;                        // UInt32 in C maps to uint in C#
            public uint processedPos;                // UInt32 in C maps to uint in C#
            public uint checkDicSize;                // UInt32 in C maps to uint in C#
            public fixed uint reps[4];               // UInt32[4] in C maps to uint[] in C#
            public uint state;                       // UInt32 in C maps to uint in C#
            public uint remainLen;                   // UInt32 in C maps to uint in C#
            public uint numProbs;                    // UInt32 in C maps to uint in C#
            public uint tempBufSize;                 // unsigned in C maps to uint in C#
            public fixed byte tempBuf[LZMA_REQUIRED_INPUT_MAX]; // Byte[LZMA_REQUIRED_INPUT_MAX] in C maps to byte[] in C#
        }

        // Define the CLzmaProps structure (as you might already have)
        [StructLayout(LayoutKind.Sequential)]
        public struct CLzmaProps
        {
            public byte lc;
            public byte lp;
            public byte pb;
            public byte _pad_;
            public uint dicSize;
        }


        internal static unsafe partial class Lzma
        {
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma2_v25_01_Dec_Construct(ref CLzma2Dec p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma2_v25_01_Dec_FreeProbs(ref CLzma2Dec p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma2_v25_01_Dec_Free(ref CLzma2Dec p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Dec_AllocateProbs(ref CLzma2Dec p, byte prop);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Dec_Allocate(ref CLzma2Dec p, byte prop);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma2_v25_01_Dec_Init(ref CLzma2Dec p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Dec_DecodeToDic(ref CLzma2Dec p, ulong dicLimit, byte* src, ulong* srcLen, int finishMode, int* status);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Dec_DecodeToBuf(ref CLzma2Dec p, byte* dest, ulong* destLen, byte* src, ulong* srcLen, int finishMode, int* status);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Dec_Parse(ref CLzma2Dec p, ulong outSize, byte* src, ulong* srcLen, int checkFinishBlock);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Decode(byte* dest, ulong* destLen, byte* src, ulong* srcLen, byte prop, int finishMode, int* status);



            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma2_v25_01_Enc_Construct(ref CLzma2EncProps p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma2_v25_01_Enc_Normalize(ref CLzma2EncProps p);
            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr SZ_Lzma2_v25_01_Enc_Create();

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma2_v25_01_Enc_Destroy(IntPtr p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Enc_SetProps(IntPtr p, ref CLzma2EncProps props);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma2_v25_01_Enc_SetDataSize(IntPtr p, ulong expectedDataSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern byte SZ_Lzma2_v25_01_Enc_WriteProperties(IntPtr p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Enc_Encode2(IntPtr p, byte* outBuf, ulong* outBufSize, byte* inData, ulong inDataSize, IntPtr progress);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Enc_EncodeMultiCallPrepare(IntPtr p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma2_v25_01_Enc_EncodeMultiCall(IntPtr p, byte* dest, ulong* destLen, ref CBufferInStream srcStream, uint init, uint final);



            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma_v25_01_Dec_Construct(ref CLzmaDec p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma_v25_01_Dec_Init(ref CLzmaDec p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Dec_AllocateProbs(ref CLzmaDec p, byte* props, uint propsSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma_v25_01_Dec_FreeProbs(ref CLzmaDec p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Dec_Allocate(ref CLzmaDec p, byte* props, uint propsSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma_v25_01_Dec_Free(ref CLzmaDec p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Dec_DecodeToDic(ref CLzmaDec p, ulong dicLimit, byte* src, ulong* srcLen, int finishMode, int* status);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Dec_DecodeToBuf(ref CLzmaDec p, byte* dest, ulong* destLen, byte* src, ulong* srcLen, int finishMode, int* status);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Dec_LzmaDecode(byte* dest, ulong* destLen, byte* src, ulong* srcLen, byte* propData, uint propSize, int finishMode, int* status);




            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma_v25_01_EncProps_Init(ref CLzmaEncProps p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma_v25_01_EncProps_Normalize(ref CLzmaEncProps p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern uint SZ_Lzma_v25_01_EncProps_GetDictSize(ref CLzmaEncProps props);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr SZ_Lzma_v25_01_Enc_Create();

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma_v25_01_Enc_Destroy(IntPtr p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Enc_SetProps(IntPtr p, ref CLzmaEncProps props);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern void SZ_Lzma_v25_01_Enc_SetDataSize(IntPtr p, ulong expectedDataSize);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Enc_WriteProperties(IntPtr p, byte* properties, ulong* size);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern uint SZ_Lzma_v25_01_Enc_IsWriteEndMark(IntPtr p);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Enc_Encode(IntPtr p, IntPtr outStream, IntPtr inStream, IntPtr progress);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Enc_MemEncode(IntPtr p, byte* dest, ulong* destLen, byte* src, ulong srcLen, int writeEndMark, IntPtr progress);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Enc_LzmaEncode(byte* dest, ulong* destLen, byte* src, ulong srcLen, ref CLzmaEncProps props, byte* propsEncoded, ulong* propsSize, int writeEndMark, IntPtr progress);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Enc_LzmaCodeMultiCallPrepare(IntPtr p, uint* blockSize, uint* dictSize, uint final);

            [DllImport(Libraries.GrindCoreLib, CallingConvention = CallingConvention.Cdecl)]
            public static extern int SZ_Lzma_v25_01_Enc_LzmaCodeMultiCall(IntPtr p, byte* dest, ulong* destLen, ref CBufferInStream srcStream, int limit,uint* availableBytes, int final);
        }
    }
}




using System.Diagnostics;
using System.Runtime.InteropServices;
using System;

namespace Nanook.GrindCore.ZLib
{
    internal static class Crc32NgHelper
    {
        // Calculate CRC based on the old CRC and the new bytes
        public static unsafe uint UpdateCrc32(uint crc32, byte[] buffer, int offset, int length)
        {
            Debug.Assert(buffer != null && offset >= 0 && length >= 0 && offset <= buffer.Length - length);
            fixed (byte* bufferPtr = buffer)
            {
                *&bufferPtr += offset;
                return Interop.ZLib.DN9_ZLibNg_v2_2_1_crc32(crc32, bufferPtr, length);
            }
        }

        //public static unsafe uint UpdateCrc32(uint crc32, ReadOnlySpan<byte> _outBuffer)
        //{
        //    fixed (byte* bufferPtr = &MemoryMarshal.GetReference(_outBuffer))
        //    {
        //        return Interop.ZLib.DN9_ZLibNg_v2_2_1_crc32(crc32, bufferPtr, _outBuffer.Length);
        //    }
        //}
    }
}

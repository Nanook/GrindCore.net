using System.Diagnostics;
using System.Runtime.InteropServices;
using System;

namespace Nanook.GrindCore.ZLib
{
    /// <summary>
    /// Provides helper methods for calculating CRC32 checksums using ZLibNg.
    /// </summary>
    internal static class Crc32NgHelper
    {
        /// <summary>
        /// Updates a CRC32 checksum with the specified buffer segment using ZLibNg.
        /// </summary>
        /// <param name="crc32">The initial CRC32 value.</param>
        /// <param name="buffer">The buffer containing the data to update the CRC with.</param>
        /// <param name="offset">The offset in the buffer at which to start.</param>
        /// <param name="length">The number of bytes to use from the buffer.</param>
        /// <returns>The updated CRC32 value.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="buffer"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="length"/> is negative, or if the range is invalid.</exception>
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

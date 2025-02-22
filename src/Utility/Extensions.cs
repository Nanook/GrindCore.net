using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Nanook.GrindCore
{
    internal static class Utilities
    {
        public static byte[] GetBytes(ulong value)
        {
            if (BitConverter.IsLittleEndian)
                value = swapBytes(value);

            return BitConverter.GetBytes(value);
        }

        public static byte[] GetBytes(uint value)
        {
            if (BitConverter.IsLittleEndian)
                value = swapBytes(value);

            return BitConverter.GetBytes(value);
        }

        private static ulong swapBytes(ulong value)
        {
            return ((value & 0x00000000000000FFUL) << 56) |
                   ((value & 0x000000000000FF00UL) << 40) |
                   ((value & 0x0000000000FF0000UL) << 24) |
                   ((value & 0x00000000FF000000UL) << 8) |
                   ((value & 0x000000FF00000000UL) >> 8) |
                   ((value & 0x0000FF0000000000UL) >> 24) |
                   ((value & 0x00FF000000000000UL) >> 40) |
                   ((value & 0xFF00000000000000UL) >> 56);
        }

        private static uint swapBytes(uint value)
        {
            return ((value & 0x000000FFU) << 24) |
                   ((value & 0x0000FF00U) << 8) |
                   ((value & 0x00FF0000U) >> 8) |
                   ((value & 0xFF000000U) >> 24);
        }
    }

    internal static class ByteArrayExtensions
    {
        public static string ToHexString(this byte[] bytes)
        {
            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }

    internal static class UIntExtensions
    {
        public static byte[] ToByteArray(this uint value)
        {
            return Utilities.GetBytes(value);
        }
    }

    internal static class ULongExtensions
    {
        public static byte[] ToByteArray(this ulong value)
        {
            return Utilities.GetBytes(value);
        }
    }
}

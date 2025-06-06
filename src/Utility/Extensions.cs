using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Provides utility methods for byte and endianness operations.
    /// </summary>
    internal static class Utilities
    {
        /// <summary>
        /// Returns the bytes of the specified <see cref="ulong"/> value in big-endian order.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A byte array representing the value in big-endian order.</returns>
        public static byte[] GetBytes(ulong value)
        {
            if (BitConverter.IsLittleEndian)
                value = swapBytes(value);

            return BitConverter.GetBytes(value);
        }

        /// <summary>
        /// Returns the bytes of the specified <see cref="uint"/> value in big-endian order.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A byte array representing the value in big-endian order.</returns>
        public static byte[] GetBytes(uint value)
        {
            if (BitConverter.IsLittleEndian)
                value = swapBytes(value);

            return BitConverter.GetBytes(value);
        }

        /// <summary>
        /// Swaps the byte order of a <see cref="ulong"/> value.
        /// </summary>
        /// <param name="value">The value to swap.</param>
        /// <returns>The value with its bytes reversed.</returns>
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

        /// <summary>
        /// Swaps the byte order of a <see cref="uint"/> value.
        /// </summary>
        /// <param name="value">The value to swap.</param>
        /// <returns>The value with its bytes reversed.</returns>
        private static uint swapBytes(uint value)
        {
            return ((value & 0x000000FFU) << 24) |
                   ((value & 0x0000FF00U) << 8) |
                   ((value & 0x00FF0000U) >> 8) |
                   ((value & 0xFF000000U) >> 24);
        }
    }

    /// <summary>
    /// Provides extension methods for byte arrays.
    /// </summary>
    internal static class ByteArrayExtensions
    {
        /// <summary>
        /// Converts a byte array to its hexadecimal string representation.
        /// </summary>
        /// <param name="bytes">The byte array to convert.</param>
        /// <returns>A string containing the hexadecimal representation of the byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="bytes"/> is null.</exception>
        public static string ToHexString(this byte[] bytes)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            StringBuilder hex = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
                hex.Append(b.ToString("x2"));
            return hex.ToString();
        }
    }

    /// <summary>
    /// Provides extension methods for <see cref="uint"/>.
    /// </summary>
    internal static class UIntExtensions
    {
        /// <summary>
        /// Converts a <see cref="uint"/> value to a byte array in big-endian order.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A byte array representing the value in big-endian order.</returns>
        public static byte[] ToByteArray(this uint value)
        {
            return Utilities.GetBytes(value);
        }
    }

    /// <summary>
    /// Provides extension methods for <see cref="ulong"/>.
    /// </summary>
    internal static class ULongExtensions
    {
        /// <summary>
        /// Converts a <see cref="ulong"/> value to a byte array in big-endian order.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A byte array representing the value in big-endian order.</returns>
        public static byte[] ToByteArray(this ulong value)
        {
            return Utilities.GetBytes(value);
        }
    }
}

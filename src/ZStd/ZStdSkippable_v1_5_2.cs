using System;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides utilities for working with Zstandard v1.5.2 skippable frames.
    /// Skippable frames allow integration of user-defined data into a flow of concatenated ZSTD frames.
    /// They will be ignored (skipped) by standard ZSTD decompressors.
    /// </summary>
    /// <remarks>
    /// This class uses the v1.5.2 implementation of ZSTD. For v1.5.7, see <see cref="ZStdSkippable_v1_5_7"/>.
    /// For additional ZSTD constants and frame type detection, see <see cref="ZStdConstants"/>.
    /// </remarks>
    public static class ZStdSkippable_v1_5_2
    {
        /// <summary>
        /// The base magic number for skippable frames (0x184D2A50).
        /// Valid skippable magic numbers range from 0x184D2A50 to 0x184D2A5F (variants 0-15).
        /// </summary>
        [CLSCompliant(false)]
        public const uint MagicNumberBase = ZStdConstants.SkippableMagicNumberBase;

        /// <summary>
        /// The mask used to identify skippable frames (0xFFFFFFF0).
        /// </summary>
        [CLSCompliant(false)]
        public const uint MagicNumberMask = ZStdConstants.SkippableMagicMask;

        /// <summary>
        /// Maximum variant value (0-15) for skippable frame magic numbers.
        /// </summary>
        [CLSCompliant(false)]
        public const uint MaxVariant = ZStdConstants.SkippableMaxVariant;

        /// <summary>
        /// Writes a skippable frame containing custom user data.
        /// </summary>
        /// <param name="destination">Destination buffer for the skippable frame.</param>
        /// <param name="source">Source data to be embedded in the skippable frame.</param>
        /// <param name="magicVariant">Variant value (0-15) to differentiate types of skippable frames.</param>
        /// <returns>Number of bytes written, or an error code if the operation fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown when destination or source is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when magicVariant is greater than 15.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the write operation fails.</exception>
        [CLSCompliant(false)]
        public static unsafe int WriteSkippableFrame(byte[] destination, byte[] source, uint magicVariant)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (magicVariant > MaxVariant)
                throw new ArgumentOutOfRangeException(nameof(magicVariant), $"Magic variant must be between 0 and {MaxVariant}");

            fixed (byte* dstPtr = destination)
            fixed (byte* srcPtr = source)
            {
                UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_WriteSkippableFrame(
                    dstPtr,
                    (UIntPtr)destination.Length,
                    srcPtr,
                    (UIntPtr)source.Length,
                    magicVariant);

                if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                {
                    IntPtr errNamePtr = Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result);
                    string errorName = Marshal.PtrToStringAnsi(errNamePtr) ?? "Unknown error";
                    throw new InvalidOperationException($"Failed to write skippable frame: {errorName}");
                }

                return (int)result;
            }
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Writes a skippable frame containing custom user data with a destination span.
        /// </summary>
        /// <param name="destination">Destination span for the skippable frame.</param>
        /// <param name="source">Source span containing data to be embedded in the skippable frame.</param>
        /// <param name="magicVariant">Variant value (0-15) to differentiate types of skippable frames.</param>
        /// <returns>Number of bytes written, or an error code if the operation fails.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when magicVariant is greater than 15.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the write operation fails.</exception>
        [CLSCompliant(false)]
        public static unsafe int WriteSkippableFrame(Span<byte> destination, ReadOnlySpan<byte> source, uint magicVariant)
        {
            if (magicVariant > MaxVariant)
                throw new ArgumentOutOfRangeException(nameof(magicVariant), $"Magic variant must be between 0 and {MaxVariant}");

            fixed (byte* dstPtr = destination)
            fixed (byte* srcPtr = source)
            {
                UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_WriteSkippableFrame(
                    dstPtr,
                    (UIntPtr)destination.Length,
                    srcPtr,
                    (UIntPtr)source.Length,
                    magicVariant);

                if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                {
                    IntPtr errNamePtr = Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result);
                    string errorName = Marshal.PtrToStringAnsi(errNamePtr) ?? "Unknown error";
                    throw new InvalidOperationException($"Failed to write skippable frame: {errorName}");
                }

                return (int)result;
            }
        }
#endif

        /// <summary>
        /// Reads the content of a skippable frame.
        /// </summary>
        /// <param name="destination">Buffer to receive the skippable frame content.</param>
        /// <param name="source">Buffer containing the skippable frame.</param>
        /// <param name="magicVariant">Receives the magic variant (0-15) that was used when the frame was written.</param>
        /// <returns>Number of bytes read (the actual content size), or an error code if the operation fails.</returns>
        /// <exception cref="ArgumentNullException">Thrown when destination or source is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the read operation fails.</exception>
        [CLSCompliant(false)]
        public static unsafe int ReadSkippableFrame(byte[] destination, byte[] source, out uint magicVariant)
        {
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            fixed (byte* dstPtr = destination)
            fixed (byte* srcPtr = source)
            {
                uint variant;
                UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_ReadSkippableFrame(
                    dstPtr,
                    (UIntPtr)destination.Length,
                    &variant,
                    srcPtr,
                    (UIntPtr)source.Length);

                magicVariant = variant;

                if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                {
                    IntPtr errNamePtr = Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result);
                    string errorName = Marshal.PtrToStringAnsi(errNamePtr) ?? "Unknown error";
                    throw new InvalidOperationException($"Failed to read skippable frame: {errorName}");
                }

                return (int)result;
            }
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Reads the content of a skippable frame using spans.
        /// </summary>
        /// <param name="destination">Span to receive the skippable frame content.</param>
        /// <param name="source">Read-only span containing the skippable frame.</param>
        /// <param name="magicVariant">Receives the magic variant (0-15) that was used when the frame was written.</param>
        /// <returns>Number of bytes read (the actual content size), or an error code if the operation fails.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the read operation fails.</exception>
        [CLSCompliant(false)]
        public static unsafe int ReadSkippableFrame(Span<byte> destination, ReadOnlySpan<byte> source, out uint magicVariant)
        {
            fixed (byte* dstPtr = destination)
            fixed (byte* srcPtr = source)
            {
                uint variant;
                UIntPtr result = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_ReadSkippableFrame(
                    dstPtr,
                    (UIntPtr)destination.Length,
                    &variant,
                    srcPtr,
                    (UIntPtr)source.Length);

                magicVariant = variant;

                if (Interop.ZStd.SZ_ZStd_v1_5_2_IsError(result) != 0)
                {
                    IntPtr errNamePtr = Interop.ZStd.SZ_ZStd_v1_5_2_GetErrorName(result);
                    string errorName = Marshal.PtrToStringAnsi(errNamePtr) ?? "Unknown error";
                    throw new InvalidOperationException($"Failed to read skippable frame: {errorName}");
                }

                return (int)result;
            }
        }
#endif

        /// <summary>
        /// Checks whether the provided buffer starts with a valid skippable frame magic number.
        /// </summary>
        /// <param name="buffer">Buffer to check.</param>
        /// <returns>True if the buffer starts with a skippable frame magic number; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when buffer is null.</exception>
        public static unsafe bool IsSkippableFrame(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            fixed (byte* bufferPtr = buffer)
            {
                return Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_IsSkippableFrame(bufferPtr, (UIntPtr)buffer.Length) != 0;
            }
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        /// <summary>
        /// Checks whether the provided span starts with a valid skippable frame magic number.
        /// </summary>
        /// <param name="buffer">Read-only span to check.</param>
        /// <returns>True if the span starts with a skippable frame magic number; otherwise false.</returns>
        public static unsafe bool IsSkippableFrame(ReadOnlySpan<byte> buffer)
        {
            fixed (byte* bufferPtr = buffer)
            {
                return Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_IsSkippableFrame(bufferPtr, (UIntPtr)buffer.Length) != 0;
            }
        }
#endif
    }
}

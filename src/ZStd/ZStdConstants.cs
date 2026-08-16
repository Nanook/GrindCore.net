using System;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides constants for Zstandard (ZSTD) compression format.
    /// These constants define magic numbers, size limits, and other ZSTD format specifications.
    /// </summary>
    public static class ZStdConstants
    {
        #region Magic Numbers

        /// <summary>
        /// Magic number for standard ZSTD compressed frames (0xFD2FB528).
        /// Valid since ZSTD v0.8.0. This identifies the start of a standard compressed frame.
        /// </summary>
        [CLSCompliant(false)]
        public const uint MagicNumber = 0xFD2FB528;

        /// <summary>
        /// Magic number for ZSTD dictionary format (0xEC30A437).
        /// Valid since ZSTD v0.7.0. This identifies a ZSTD dictionary file.
        /// </summary>
        [CLSCompliant(false)]
        public const uint MagicDictionary = 0xEC30A437;

        /// <summary>
        /// Base magic number for skippable frames (0x184D2A50).
        /// Skippable frames allow embedding custom user data within a ZSTD stream.
        /// Valid magic numbers range from 0x184D2A50 to 0x184D2A5F (16 variants).
        /// See <see cref="SkippableMagicMask"/> and <see cref="SkippableMaxVariant"/>.
        /// </summary>
        [CLSCompliant(false)]
        public const uint SkippableMagicNumberBase = 0x184D2A50;

        /// <summary>
        /// Mask used to identify skippable frames (0xFFFFFFF0).
        /// Use this to test if a magic number represents a skippable frame:
        /// <code>
        /// bool isSkippable = (magicNumber &amp; SkippableMagicMask) == SkippableMagicNumberBase;
        /// </code>
        /// </summary>
        [CLSCompliant(false)]
        public const uint SkippableMagicMask = 0xFFFFFFF0;

        /// <summary>
        /// Maximum variant value for skippable frame magic numbers (0-15).
        /// The actual magic number is: <c>SkippableMagicNumberBase + variant</c>.
        /// This allows 16 different types of skippable frames (0x184D2A50 through 0x184D2A5F).
        /// </summary>
        [CLSCompliant(false)]
        public const uint SkippableMaxVariant = 15;

        #endregion

        #region Block and Frame Limits

        /// <summary>
        /// Maximum log2 value for ZSTD block size (17).
        /// Block size = 2^17 = 128 KB.
        /// This is an internal ZSTD compression unit size limit enforced by the library.
        /// </summary>
        /// <remarks>
        /// Note: This is the maximum size of a single **block** within a frame, not the frame itself.
        /// For seekable compression, frames can be much larger (typically 1MB+) and contain many blocks.
        /// </remarks>
        public const int BlockSizeLogMax = 17;

        /// <summary>
        /// Maximum size of a single ZSTD block (131072 bytes = 128 KB).
        /// Calculated as 2^<see cref="BlockSizeLogMax"/>.
        /// This limit is enforced by the ZSTD library for internal compression blocks.
        /// </summary>
        /// <remarks>
        /// This is NOT the same as frame size. A frame can contain multiple blocks.
        /// For seekable frames, you control frame size (e.g., 1MB default), not individual block size.
        /// </remarks>
        public const int BlockSizeMax = 1 << BlockSizeLogMax; // 131072 bytes

        #endregion

        #region Content Size Markers

        /// <summary>
        /// Special value indicating that the decompressed content size is unknown.
        /// This value is used in the frame header when the final size cannot be determined upfront.
        /// Value: <c>ulong.MaxValue</c> (0xFFFFFFFFFFFFFFFF)
        /// </summary>
        [CLSCompliant(false)]
        public const ulong ContentSizeUnknown = ulong.MaxValue;

        /// <summary>
        /// Special value indicating an error when reading the content size.
        /// Value: <c>ulong.MaxValue - 1</c> (0xFFFFFFFFFFFFFFFE)
        /// </summary>
        [CLSCompliant(false)]
        public const ulong ContentSizeError = ulong.MaxValue - 1;

        #endregion

        #region Frame Type Detection

        /// <summary>
        /// Determines if a magic number represents a standard ZSTD compressed frame.
        /// </summary>
        /// <param name="magicNumber">The 4-byte magic number to test.</param>
        /// <returns>True if the magic number identifies a standard ZSTD frame.</returns>
        [CLSCompliant(false)]
        public static bool IsStandardFrame(uint magicNumber)
        {
            return magicNumber == MagicNumber;
        }

        /// <summary>
        /// Determines if a magic number represents a ZSTD dictionary.
        /// </summary>
        /// <param name="magicNumber">The 4-byte magic number to test.</param>
        /// <returns>True if the magic number identifies a ZSTD dictionary.</returns>
        [CLSCompliant(false)]
        public static bool IsDictionary(uint magicNumber)
        {
            return magicNumber == MagicDictionary;
        }

        /// <summary>
        /// Determines if a magic number represents a skippable frame.
        /// Skippable frames use magic numbers from 0x184D2A50 to 0x184D2A5F.
        /// </summary>
        /// <param name="magicNumber">The 4-byte magic number to test.</param>
        /// <returns>True if the magic number identifies a skippable frame.</returns>
        [CLSCompliant(false)]
        public static bool IsSkippableFrame(uint magicNumber)
        {
            return (magicNumber & SkippableMagicMask) == SkippableMagicNumberBase;
        }

        /// <summary>
        /// Extracts the variant value (0-15) from a skippable frame magic number.
        /// </summary>
        /// <param name="magicNumber">The skippable frame magic number.</param>
        /// <returns>The variant value (0-15), or null if the magic number is not a skippable frame.</returns>
        [CLSCompliant(false)]
        public static uint? GetSkippableVariant(uint magicNumber)
        {
            if (!IsSkippableFrame(magicNumber))
                return null;

            return magicNumber - SkippableMagicNumberBase;
        }

        /// <summary>
        /// Calculates the complete skippable frame magic number for a given variant.
        /// </summary>
        /// <param name="variant">Variant value (0-15).</param>
        /// <returns>The complete magic number (0x184D2A50 + variant).</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when variant is greater than 15.</exception>
        [CLSCompliant(false)]
        public static uint GetSkippableMagicNumber(uint variant)
        {
            if (variant > SkippableMaxVariant)
                throw new ArgumentOutOfRangeException(nameof(variant), 
                    $"Variant must be between 0 and {SkippableMaxVariant}");

            return SkippableMagicNumberBase + variant;
        }

        /// <summary>
        /// Gets a human-readable description of a ZSTD frame type based on its magic number.
        /// </summary>
        /// <param name="magicNumber">The 4-byte magic number to identify.</param>
        /// <returns>A string describing the frame type.</returns>
        [CLSCompliant(false)]
        public static string GetFrameTypeDescription(uint magicNumber)
        {
            if (IsStandardFrame(magicNumber))
                return "Standard ZSTD compressed frame";

            if (IsDictionary(magicNumber))
                return "ZSTD dictionary";

            if (IsSkippableFrame(magicNumber))
            {
                uint variant = magicNumber - SkippableMagicNumberBase;
                return $"Skippable frame (variant {variant})";
            }

            return $"Unknown frame type (magic: 0x{magicNumber:X8})";
        }

        #endregion
    }
}

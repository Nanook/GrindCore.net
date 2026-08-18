using System;
using Nanook.GrindCore.ZStd;
using Xunit;

namespace GrindCore.Tests
{
    public class ZStdConstantsTests
    {
        #region Magic Number Constants Tests

        [Fact]
        public void MagicNumber_HasCorrectValue()
        {
            Assert.Equal(0xFD2FB528u, ZStdConstants.MagicNumber);
        }

        [Fact]
        public void MagicDictionary_HasCorrectValue()
        {
            Assert.Equal(0xEC30A437u, ZStdConstants.MagicDictionary);
        }

        [Fact]
        public void SkippableMagicNumberBase_HasCorrectValue()
        {
            Assert.Equal(0x184D2A50u, ZStdConstants.SkippableMagicNumberBase);
        }

        [Fact]
        public void SkippableMagicMask_HasCorrectValue()
        {
            Assert.Equal(0xFFFFFFF0u, ZStdConstants.SkippableMagicMask);
        }

        [Fact]
        public void SkippableMaxVariant_HasCorrectValue()
        {
            Assert.Equal(15u, ZStdConstants.SkippableMaxVariant);
        }

        #endregion

        #region Block and Frame Limit Tests

        [Fact]
        public void BlockSizeLogMax_HasCorrectValue()
        {
            Assert.Equal(17, ZStdConstants.BlockSizeLogMax);
        }

        [Fact]
        public void BlockSizeMax_IsCalculatedCorrectly()
        {
            Assert.Equal(131072, ZStdConstants.BlockSizeMax);
            Assert.Equal(1 << 17, ZStdConstants.BlockSizeMax);
            Assert.Equal(128 * 1024, ZStdConstants.BlockSizeMax);
        }

        #endregion

        #region Content Size Tests

        [Fact]
        public void ContentSizeUnknown_HasCorrectValue()
        {
            Assert.Equal(ulong.MaxValue, ZStdConstants.ContentSizeUnknown);
        }

        [Fact]
        public void ContentSizeError_HasCorrectValue()
        {
            Assert.Equal(ulong.MaxValue - 1, ZStdConstants.ContentSizeError);
        }

        #endregion

        #region Frame Type Detection Tests

        [Fact]
        public void IsStandardFrame_WithValidMagicNumber_ReturnsTrue()
        {
            Assert.True(ZStdConstants.IsStandardFrame(0xFD2FB528u));
        }

        [Fact]
        public void IsStandardFrame_WithInvalidMagicNumber_ReturnsFalse()
        {
            Assert.False(ZStdConstants.IsStandardFrame(0x00000000u));
            Assert.False(ZStdConstants.IsStandardFrame(0x184D2A50u)); // Skippable
            Assert.False(ZStdConstants.IsStandardFrame(0xEC30A437u)); // Dictionary
        }

        [Fact]
        public void IsDictionary_WithValidMagicNumber_ReturnsTrue()
        {
            Assert.True(ZStdConstants.IsDictionary(0xEC30A437u));
        }

        [Fact]
        public void IsDictionary_WithInvalidMagicNumber_ReturnsFalse()
        {
            Assert.False(ZStdConstants.IsDictionary(0x00000000u));
            Assert.False(ZStdConstants.IsDictionary(0xFD2FB528u)); // Standard
            Assert.False(ZStdConstants.IsDictionary(0x184D2A50u)); // Skippable
        }

        [Fact]
        public void IsSkippableFrame_WithAllValidVariants_ReturnsTrue()
        {
            // Test all 16 valid skippable magic numbers (0x184D2A50 through 0x184D2A5F)
            for (uint i = 0; i <= 15; i++)
            {
                uint magicNumber = 0x184D2A50u + i;
                Assert.True(ZStdConstants.IsSkippableFrame(magicNumber), 
                    $"Variant {i} (0x{magicNumber:X8}) should be recognized as skippable");
            }
        }

        [Fact]
        public void IsSkippableFrame_WithInvalidMagicNumbers_ReturnsFalse()
        {
            Assert.False(ZStdConstants.IsSkippableFrame(0x00000000u));
            Assert.False(ZStdConstants.IsSkippableFrame(0xFD2FB528u)); // Standard
            Assert.False(ZStdConstants.IsSkippableFrame(0xEC30A437u)); // Dictionary
            Assert.False(ZStdConstants.IsSkippableFrame(0x184D2A60u)); // Just outside range
            Assert.False(ZStdConstants.IsSkippableFrame(0x184D2A4Fu)); // Just before range
        }

        [Fact]
        public void GetSkippableVariant_WithValidSkippableFrames_ReturnsCorrectVariant()
        {
            for (uint expectedVariant = 0; expectedVariant <= 15; expectedVariant++)
            {
                uint magicNumber = 0x184D2A50u + expectedVariant;
                uint? actualVariant = ZStdConstants.GetSkippableVariant(magicNumber);

                Assert.NotNull(actualVariant);
                Assert.Equal(expectedVariant, actualVariant!.Value);
            }
        }

        [Fact]
        public void GetSkippableVariant_WithNonSkippableFrames_ReturnsNull()
        {
            Assert.Null(ZStdConstants.GetSkippableVariant(0xFD2FB528u)); // Standard
            Assert.Null(ZStdConstants.GetSkippableVariant(0xEC30A437u)); // Dictionary
            Assert.Null(ZStdConstants.GetSkippableVariant(0x00000000u)); // Invalid
        }

        [Fact]
        public void GetSkippableMagicNumber_WithValidVariants_ReturnsCorrectMagicNumber()
        {
            for (uint variant = 0; variant <= 15; variant++)
            {
                uint expectedMagicNumber = 0x184D2A50u + variant;
                uint actualMagicNumber = ZStdConstants.GetSkippableMagicNumber(variant);

                Assert.Equal(expectedMagicNumber, actualMagicNumber);
            }
        }

        [Fact]
        public void GetSkippableMagicNumber_WithInvalidVariant_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                ZStdConstants.GetSkippableMagicNumber(16));

            Assert.Throws<ArgumentOutOfRangeException>(() => 
                ZStdConstants.GetSkippableMagicNumber(100));
        }

        [Fact]
        public void GetFrameTypeDescription_WithStandardFrame_ReturnsCorrectDescription()
        {
            string description = ZStdConstants.GetFrameTypeDescription(0xFD2FB528u);
            Assert.Equal("Standard ZSTD compressed frame", description);
        }

        [Fact]
        public void GetFrameTypeDescription_WithDictionary_ReturnsCorrectDescription()
        {
            string description = ZStdConstants.GetFrameTypeDescription(0xEC30A437u);
            Assert.Equal("ZSTD dictionary", description);
        }

        [Fact]
        public void GetFrameTypeDescription_WithSkippableFrames_ReturnsCorrectDescription()
        {
            for (uint variant = 0; variant <= 15; variant++)
            {
                uint magicNumber = 0x184D2A50u + variant;
                string description = ZStdConstants.GetFrameTypeDescription(magicNumber);

                Assert.Equal($"Skippable frame (variant {variant})", description);
            }
        }

        [Fact]
        public void GetFrameTypeDescription_WithUnknownMagicNumber_ReturnsCorrectDescription()
        {
            string description = ZStdConstants.GetFrameTypeDescription(0x12345678u);
            Assert.Equal("Unknown frame type (magic: 0x12345678)", description);
        }

        #endregion

        #region Backward Compatibility Tests

        [Fact]
        public void ZStdSkippable_ConstantsMatchZStdConstants()
        {
            // Verify backward compatibility - ZStdSkippable constants should match ZStdConstants
            Assert.Equal(ZStdConstants.SkippableMagicNumberBase, ZStdSkippable.MagicNumberBase);
            Assert.Equal(ZStdConstants.SkippableMagicMask, ZStdSkippable.MagicNumberMask);
            Assert.Equal(ZStdConstants.SkippableMaxVariant, ZStdSkippable.MaxVariant);
        }

        [Fact]
        public void ZStdSkippable_GetMagicNumber_MatchesZStdConstants()
        {
            // Verify that both methods produce the same results
            for (uint variant = 0; variant <= 15; variant++)
            {
                uint fromSkippable = ZStdSkippable.GetMagicNumber(variant);
                uint fromConstants = ZStdConstants.GetSkippableMagicNumber(variant);

                Assert.Equal(fromConstants, fromSkippable);
            }
        }

        #endregion

        #region Integration Tests

        [Fact]
        public void SkippableMagicMask_WorksCorrectlyForDetection()
        {
            // Verify the mask correctly identifies all skippable variants
            for (uint i = 0; i <= 15; i++)
            {
                uint magicNumber = ZStdConstants.SkippableMagicNumberBase + i;
                uint masked = magicNumber & ZStdConstants.SkippableMagicMask;

                Assert.Equal(ZStdConstants.SkippableMagicNumberBase, masked);
            }

            // Verify the mask rejects non-skippable magic numbers
            Assert.NotEqual(ZStdConstants.SkippableMagicNumberBase, 
                ZStdConstants.MagicNumber & ZStdConstants.SkippableMagicMask);
            Assert.NotEqual(ZStdConstants.SkippableMagicNumberBase, 
                ZStdConstants.MagicDictionary & ZStdConstants.SkippableMagicMask);
        }

        [Fact]
        public void AllKnownMagicNumbers_AreUnique()
        {
            // Ensure no collisions between magic numbers
            Assert.NotEqual(ZStdConstants.MagicNumber, ZStdConstants.MagicDictionary);
            Assert.NotEqual(ZStdConstants.MagicNumber, ZStdConstants.SkippableMagicNumberBase);
            Assert.NotEqual(ZStdConstants.MagicDictionary, ZStdConstants.SkippableMagicNumberBase);
        }

        #endregion
    }
}

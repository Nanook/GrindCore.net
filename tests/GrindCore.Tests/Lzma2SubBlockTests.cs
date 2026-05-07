using System;
using System.IO;
using Nanook.GrindCore;
using Nanook.GrindCore.Lzma;
using Nanook.GrindCore.XXHash;
using Xunit;

namespace GrindCore.Tests
{
    /// <summary>
    /// Tests for the LZMA2 uncompressed sub-block uSize fix (issue #51).
    /// Before the fix, uncompressed blocks (control 0x01/0x02) incorrectly
    /// incorporated the control byte into a 3-byte size field, inflating uSize
    /// by 65,536 and causing premature EOF.
    /// </summary>
    public class Lzma2SubBlockTests
    {
        // ── ReadSubBlockInfo unit tests ──────────────────────────────────────

        [Fact]
        public void ReadSubBlockInfo_UncompressedBlock_SizeIsOnly2Bytes()
        {
            // Arrange: control=0x01 (LZMA2_CONTROL_COPY_RESET_DIC), size bytes 0xBF 0x22
            // Correct uSize = (0xBF << 8 | 0x22) + 1 = 48931
            // Buggy   uSize = ((0x01 & 0x1F) << 16 | 0xBF << 8 | 0x22) + 1 = 114467
            byte[] header = new byte[] { 0x01, 0xBF, 0x22 };
            var decoder = new Lzma2Decoder(0x00); // property 0 = lc=0,lp=0,pb=0 (valid range 0-40)

            Lzma2BlockInfo info = decoder.ReadSubBlockInfo(header, 0, 3);

            Assert.False(info.IsCompressed);
            Assert.Equal(48931, info.UncompressedSize);
            Assert.Equal(48931, info.CompressedSize); // equals uSize for uncompressed blocks
        }

        [Fact]
        public void ReadSubBlockInfo_UncompressedBlock_ControlCopyNoReset_SizeIsOnly2Bytes()
        {
            // control=0x02 (LZMA2_CONTROL_COPY_NO_RESET)
            // uSize = (0x00 << 8 | 0xFF) + 1 = 256
            byte[] header = new byte[] { 0x02, 0x00, 0xFF };
            var decoder = new Lzma2Decoder(0x00);

            Lzma2BlockInfo info = decoder.ReadSubBlockInfo(header, 0, 3);

            Assert.False(info.IsCompressed);
            Assert.Equal(256, info.UncompressedSize);
            Assert.Equal(256, info.CompressedSize);
        }

        [Fact]
        public void ReadSubBlockInfo_CompressedBlock_StillUsesControlHighBits()
        {
            // Compressed block: control=0x80|0x20|0x01 = 0xA1
            // hasComp=true, initState=true, initProp=false
            // uSize = ((0xA1 & 0x1F) << 16 | 0x00 << 8 | 0x0A) + 1 = (0x01 << 16 | 0x0A) + 1 = 65547
            // cSize = (0x00 << 8 | 0x64) + 1 = 101
            byte[] header = new byte[] { 0xA1, 0x00, 0x0A, 0x00, 0x64 };
            var decoder = new Lzma2Decoder(0x00);

            Lzma2BlockInfo info = decoder.ReadSubBlockInfo(header, 0, 5);

            Assert.True(info.IsCompressed);
            Assert.Equal(65547, info.UncompressedSize);
            Assert.Equal(101, info.CompressedSize);
        }

        [Fact]
        public void ReadSubBlockInfo_Terminator_IsTerminator()
        {
            byte[] header = new byte[] { 0x00 };
            var decoder = new Lzma2Decoder(0x00);

            Lzma2BlockInfo info = decoder.ReadSubBlockInfo(header, 0, 1);

            Assert.True(info.IsTerminator);
        }

        // ── Round-trip integration tests ─────────────────────────────────────

        /// <summary>
        /// Verifies LZMA2 stream round-trip for data that is incompressible enough
        /// that the encoder emits uncompressed sub-blocks (copy blocks), exercising
        /// the fixed uSize path in the decoder.
        /// </summary>
        [Fact]
        public void Lzma2Stream_RoundTrip_WithUncompressibleData()
        {
            // Random (incompressible) data forces the encoder to emit copy sub-blocks
            var rng = new Random(42);
            byte[] original = new byte[512 * 1024];
            rng.NextBytes(original);

            var (compressed, props) = compressLzma2(original);
            byte[] decompressed = decompressLzma2(compressed, props);

            Assert.Equal(original.Length, decompressed.Length);
            Assert.Equal(
                XXHash64.Compute(original, 0, original.Length),
                XXHash64.Compute(decompressed, 0, decompressed.Length));
        }

        /// <summary>
        /// Verifies LZMA2 stream round-trip for highly compressible data
        /// exercises the compressed sub-block path and ensures the fix didn't
        /// regress normal operation.
        /// </summary>
        [Fact]
        public void Lzma2Stream_RoundTrip_WithCompressibleData()
        {
            // Repetitive data — highly compressible, produces LZMA sub-blocks
            byte[] original = new byte[512 * 1024];
            for (int i = 0; i < original.Length; i++)
                original[i] = (byte)(i % 32);

            var (compressed, props) = compressLzma2(original);
            byte[] decompressed = decompressLzma2(compressed, props);

            Assert.Equal(original.Length, decompressed.Length);
            Assert.Equal(
                XXHash64.Compute(original, 0, original.Length),
                XXHash64.Compute(decompressed, 0, decompressed.Length));
        }

        /// <summary>
        /// Validates the specific bug scenario from issue #51: uncompressed copy sub-blocks
        /// (e.g. control byte 0x01 followed by 2 size bytes) were previously parsed with
        /// a 3-byte size field, inflating uSize by 65,536 and truncating the output.
        /// This test uses mixed data so the encoder emits both compressed and copy sub-blocks
        /// in the same stream, and verifies the full 2 MB is decompressed correctly.
        /// </summary>
        [Fact]
        public void Lzma2Stream_RoundTrip_MixedSubBlocks_FullOutputProduced()
        {
            // Mix of compressible and incompressible regions forces both
            // compressed and copy sub-blocks into the same LZMA2 stream
            var rng = new Random(999);
            byte[] original = new byte[2 * 1024 * 1024];
            // First half: repetitive (compressible)
            for (int i = 0; i < original.Length / 2; i++)
                original[i] = (byte)(i % 64);
            // Second half: random (incompressible, will produce copy blocks)
            byte[] rand = new byte[original.Length / 2];
            rng.NextBytes(rand);
            Array.Copy(rand, 0, original, original.Length / 2, rand.Length);

            var (compressed, props) = compressLzma2(original);
            byte[] decompressed = decompressLzma2(compressed, props);

            Assert.Equal(original.Length, decompressed.Length);
            Assert.Equal(
                XXHash64.Compute(original, 0, original.Length),
                XXHash64.Compute(decompressed, 0, decompressed.Length));
        }

        // ── Helpers

        private static (byte[] data, byte[] properties) compressLzma2(byte[] data)
        {
            int bufferSize = 256 * 1024;
            var options = new CompressionOptions
            {
                Type = CompressionType.Fastest,
                LeaveOpen = true,
                BufferSize = bufferSize,
                BlockSize = bufferSize
            };

            using (var ms = new MemoryStream())
            {
                byte[] props;
                using (var stream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, ms, options))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Complete();
                    props = stream.Properties;
                }
                return (ms.ToArray(), props);
            }
        }

        private static byte[] decompressLzma2(byte[] compressed, byte[] properties)
        {
            int bufferSize = 256 * 1024;
            var options = new CompressionOptions
            {
                Type = CompressionType.Decompress,
                LeaveOpen = true,
                BlockSize = bufferSize,
                InitProperties = properties
            };

            using (var ms = new MemoryStream(compressed))
            using (var stream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, ms, options))
            using (var output = new MemoryStream())
            {
                stream.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}

using System;
using System.Linq;
using System.Text;
using Nanook.GrindCore;
using Nanook.GrindCore.ZStd;
using Xunit;

namespace GrindCore.Tests
{
    /// <summary>
    /// <see cref="CompressionDictionaryOptions.WriteContentSize"/> on <see cref="ZStdBlock"/> (the GrindCore audit's
    /// zstd.md 2.3). false leaves the content size out of the frame header, which then holds the window size in its
    /// place, as zstd's advanced API and python-zstandard write a frame with the content size off: one byte smaller for
    /// a block of 256 B to 64 KiB, NDZ's case. true gives exactly the frame the option's absence gives.
    /// </summary>
    public sealed class ZStdBlockContentSizeTests
    {
        private static byte[] Text(int n, int seed)
        {
            var r = new Random(seed);
            string[] words = { "grindcore", "zstd", "frame", "header", "window", "block", "content", "size", "\n" };
            var sb = new StringBuilder(n + 16);
            while (sb.Length < n)
                sb.Append(words[r.Next(words.Length)]).Append(' ');
            return Encoding.ASCII.GetBytes(sb.ToString(0, n));
        }

        private static readonly byte[] Dict = Text(8000, 99);

        private static CompressionOptions Options(int version, bool? writeContentSize, bool dictionary) =>
            new CompressionOptions
            {
                Type = CompressionType.Optimal,
                Version = CompressionVersion.ZStd(version == 152 ? ZStdVersion.v1_5_2 : ZStdVersion.v1_5_7),
                InitProperties = dictionary ? Dict : null,
                Dictionary = writeContentSize.HasValue ? new CompressionDictionaryOptions { WriteContentSize = writeContentSize } : null,
            };

        private static byte[] Compress(byte[] data, CompressionOptions o)
        {
            o.BlockSize = Math.Max(data.Length, 1);
            using var block = new ZStdBlock(o);
            byte[] dst = new byte[block.RequiredCompressOutputSize];
            int n = dst.Length;
            Assert.Equal(CompressionResultCode.Success, block.Compress(data, 0, data.Length, dst, 0, ref n));
            return dst.Take(n).ToArray();
        }

        private static byte[] Decompress(byte[] frame, int size, CompressionOptions o)
        {
            o.BlockSize = Math.Max(size, 1);
            using var block = new ZStdBlock(o);
            byte[] dst = new byte[size + 64];
            int n = dst.Length;
            Assert.Equal(CompressionResultCode.Success, block.Decompress(frame, 0, frame.Length, dst, 0, ref n));
            return dst.Take(n).ToArray();
        }

        // The frame header descriptor (byte 4 of a zstd frame): a content size is there when Single_Segment_flag (0x20)
        // or a Frame_Content_Size_flag (the top two bits) is set
        private static bool HasContentSize(byte[] frame) => (frame[4] & 0x20) != 0 || (frame[4] >> 6) != 0;

        [Theory]
        [InlineData(157, 300)]
        [InlineData(157, 4096)]
        [InlineData(157, 65000)]
        [InlineData(152, 300)]
        [InlineData(152, 4096)]
        [InlineData(152, 65000)]
        public void WithoutContentSize_FrameIsOneByteSmallerAndDecodes(int version, int size)
        {
            foreach (bool dictionary in new[] { false, true })
            {
                byte[] data = Text(size, size);
                byte[] plain = Compress(data, Options(version, null, dictionary));
                byte[] trimmed = Compress(data, Options(version, false, dictionary));

                Assert.True(HasContentSize(plain), "zstd's default frame records the content size");
                Assert.False(HasContentSize(trimmed), $"dictionary {dictionary}: WriteContentSize = false left the content size in");
                Assert.Equal(plain.Length - 1, trimmed.Length);
                Assert.Equal(data, Decompress(trimmed, size, Options(version, null, dictionary)));
            }
        }

        [Theory]
        [InlineData(157)]
        [InlineData(152)]
        public void WithContentSize_IsTheDefaultFrame(int version)
        {
            foreach (bool dictionary in new[] { false, true })
            foreach (int size in new[] { 0, 100, 4096, 70000 })
            {
                byte[] data = Text(size, size + 1);
                Assert.Equal(Compress(data, Options(version, null, dictionary)), Compress(data, Options(version, true, dictionary)));
            }
        }

        [Theory]
        [InlineData(157)]
        [InlineData(152)]
        public void WithoutContentSize_SmallAndEmptyBlocksDecode(int version)
        {
            // Under 256 bytes the content size takes one byte, as the window size does, so the frame is the same length
            foreach (int size in new[] { 0, 1, 100, 255 })
            {
                byte[] data = Text(size, size + 2);
                byte[] trimmed = Compress(data, Options(version, false, false));
                Assert.False(HasContentSize(trimmed));
                Assert.Equal(Compress(data, Options(version, null, false)).Length, trimmed.Length);
                Assert.Equal(data, Decompress(trimmed, size, Options(version, null, false)));
            }
        }
    }
}

using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Nanook.GrindCore.BZip2;
using System;
using System.IO;
using Xunit;

namespace GrindCore.Tests
{
    /// <summary>
    /// Exercises BZip2Decoder's transparent multi-stream (concatenated .bz2) continuation -
    /// distinct from an ordinary round-trip test, which never produces more than one logical
    /// bzip2 stream and so never exercises this path. bzip2 has documented concatenation as
    /// valid input since 0.9.0 (the same convention gzip uses for concatenated members).
    /// </summary>
    public sealed class BZip2MultiStreamTests
    {
        /// <summary>
        /// Compresses two independent chunks as two separate, complete bzip2 streams (each with
        /// its own "BZh" header and end-of-stream marker), concatenates the compressed bytes, and
        /// confirms decompression transparently continues past the first stream's end and
        /// produces the concatenation of both original chunks.
        /// </summary>
        [Fact]
        public void BZip2Block_ConcatenatedStreams_DecompressAsOneLogicalStream()
        {
            byte[] chunkA = TestDataStream.Create(50_000);
            byte[] chunkB = TestPseudoTextStream.Create(30_000);

            byte[] compressedA = compressBlock(chunkA);
            byte[] compressedB = compressBlock(chunkB);

            // Simulate an externally concatenated .bz2 file (e.g. `cat a.bz2 b.bz2 > combined.bz2`).
            byte[] combined = new byte[compressedA.Length + compressedB.Length];
            Buffer.BlockCopy(compressedA, 0, combined, 0, compressedA.Length);
            Buffer.BlockCopy(compressedB, 0, combined, compressedA.Length, compressedB.Length);

            byte[] expected = new byte[chunkA.Length + chunkB.Length];
            Buffer.BlockCopy(chunkA, 0, expected, 0, chunkA.Length);
            Buffer.BlockCopy(chunkB, 0, expected, chunkA.Length, chunkB.Length);

            using (var ms = new MemoryStream(combined))
            using (var stream = new BZip2Stream(ms, CompressionOptions.DefaultDecompress()))
            using (var outMs = new MemoryStream())
            {
                stream.CopyTo(outMs);
                byte[] actual = outMs.ToArray();

                Assert.Equal(expected.Length, actual.Length);
                Assert.True(actual.AsSpan().SequenceEqual(expected), "Decompressed concatenated streams did not match the concatenation of the original chunks.");
            }
        }

        /// <summary>
        /// Same as above but with three concatenated streams, confirming the continuation logic
        /// isn't limited to a single reinitialization.
        /// </summary>
        [Fact]
        public void BZip2Block_ThreeConcatenatedStreams_DecompressAsOneLogicalStream()
        {
            byte[][] chunks =
            {
                TestDataStream.Create(20_000),
                TestNonCompressibleDataStream.Create(15_000),
                TestPseudoTextStream.Create(25_000)
            };

            using (var combinedMs = new MemoryStream())
            {
                foreach (byte[] chunk in chunks)
                {
                    byte[] compressed = compressBlock(chunk);
                    combinedMs.Write(compressed, 0, compressed.Length);
                }

                byte[] expected = new byte[chunks[0].Length + chunks[1].Length + chunks[2].Length];
                int pos = 0;
                foreach (byte[] chunk in chunks)
                {
                    Buffer.BlockCopy(chunk, 0, expected, pos, chunk.Length);
                    pos += chunk.Length;
                }

                combinedMs.Position = 0;
                using (var stream = new BZip2Stream(combinedMs, CompressionOptions.DefaultDecompress()))
                using (var outMs = new MemoryStream())
                {
                    stream.CopyTo(outMs);
                    byte[] actual = outMs.ToArray();

                    Assert.Equal(expected.Length, actual.Length);
                    Assert.True(actual.AsSpan().SequenceEqual(expected), "Decompressed 3-way concatenated streams did not match the concatenation of the original chunks.");
                }
            }
        }

        /// <summary>
        /// A single (non-concatenated) stream must still decompress normally - confirms the
        /// magic-byte probe that decides whether to continue after BZ_STREAM_END doesn't
        /// misbehave when there simply is no more data.
        /// </summary>
        [Fact]
        public void BZip2Block_SingleStream_DecompressesNormally()
        {
            byte[] chunk = TestDataStream.Create(10_000);
            byte[] compressed = compressBlock(chunk);

            using (var ms = new MemoryStream(compressed))
            using (var stream = new BZip2Stream(ms, CompressionOptions.DefaultDecompress()))
            using (var outMs = new MemoryStream())
            {
                stream.CopyTo(outMs);
                byte[] actual = outMs.ToArray();

                Assert.Equal(chunk.Length, actual.Length);
                Assert.True(actual.AsSpan().SequenceEqual(chunk));
            }
        }

        private static byte[] compressBlock(byte[] data)
        {
            using (CompressionBlock block = CompressionBlockFactory.Create(CompressionAlgorithm.BZip2, CompressionType.Optimal, data.Length, false, null))
            {
                byte[] compressed = BufferPool.Rent(block.RequiredCompressOutputSize);
                int compressedLength = compressed.Length;
                var result = block.Compress(data, 0, data.Length, compressed, 0, ref compressedLength);
                Assert.Equal(CompressionResultCode.Success, result);

                byte[] trimmed = new byte[compressedLength];
                Buffer.BlockCopy(compressed, 0, trimmed, 0, compressedLength);
                BufferPool.Return(compressed);
                return trimmed;
            }
        }
    }
}

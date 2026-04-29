using System;
using System.IO;
using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;
using Xunit;

namespace GrindCore.Tests
{
    /// <summary>
    /// Tests for the WriteEndMark option wired through LZMA stream and block paths.
    ///
    /// LZMA end-of-payload marker (EOPM) behaviour:
    ///   Stream mode  — default writeEndMark=0 (no EOPM); compatible with decoders that read
    ///                  until InitProperties length is exhausted.
    ///   Block mode   — default writeEndMark=1 (EOPM written); makes each block self-terminating.
    ///
    /// The WriteEndMark int? option (null=codec default, 0=suppress, 1=force) allows callers to
    /// override these defaults without changing any other behaviour.
    /// </summary>
    public class LzmaWriteEndMarkTests
    {
        private static readonly byte[] _testData = buildTestData(128 * 1024);

        private static byte[] buildTestData(int size)
        {
            // Mix of compressible and less-compressible bytes so the encoder exercises more code paths
            byte[] d = new byte[size];
            for (int i = 0; i < size; i++)
                d[i] = (byte)((i * 7 + 13) % 251);
            return d;
        }

        // ── Stream mode — default behaviour ─────────────────────────────────

        [Fact]
        public void LzmaStream_DefaultWriteEndMark_RoundTrip()
        {
            // null WriteEndMark → codec default (0, no EOPM) — should still round-trip correctly
            var (compressed, props) = compressLzmaStream(_testData, writeEndMark: null);
            byte[] decompressed = decompressLzmaStream(compressed, _testData.Length, props);

            Assert.Equal(_testData.Length, decompressed.Length);
            Assert.Equal(
                XXHash64.Compute(_testData, 0, _testData.Length),
                XXHash64.Compute(decompressed, 0, decompressed.Length));
        }

        // ── Stream mode — explicit WriteEndMark=0 ───────────────────────────

        [Fact]
        public void LzmaStream_WriteEndMarkZero_RoundTrip()
        {
            var (compressed, props) = compressLzmaStream(_testData, writeEndMark: 0);
            byte[] decompressed = decompressLzmaStream(compressed, _testData.Length, props);

            Assert.Equal(_testData.Length, decompressed.Length);
            Assert.Equal(
                XXHash64.Compute(_testData, 0, _testData.Length),
                XXHash64.Compute(decompressed, 0, decompressed.Length));
        }

        // ── Stream mode — explicit WriteEndMark=1 ───────────────────────────

        [Fact]
        public void LzmaStream_WriteEndMarkOne_RoundTrip()
        {
            var (compressed, props) = compressLzmaStream(_testData, writeEndMark: 1);
            byte[] decompressed = decompressLzmaStream(compressed, _testData.Length, props);

            Assert.Equal(_testData.Length, decompressed.Length);
            Assert.Equal(
                XXHash64.Compute(_testData, 0, _testData.Length),
                XXHash64.Compute(decompressed, 0, decompressed.Length));
        }

        /// <summary>
        /// Enabling the EOPM adds bytes to the compressed output.
        /// WriteEndMark=1 output must be strictly larger than WriteEndMark=0 output.
        /// </summary>
        [Fact]
        public void LzmaStream_WriteEndMarkOne_IsLargerThanWriteEndMarkZero()
        {
            var (withEopm, _) = compressLzmaStream(_testData, writeEndMark: 1);
            var (withoutEopm, _) = compressLzmaStream(_testData, writeEndMark: 0);

            Assert.True(withEopm.Length > withoutEopm.Length,
                $"Expected EOPM output ({withEopm.Length} bytes) > no-EOPM output ({withoutEopm.Length} bytes)");
        }

        // ── Block mode — default behaviour (EOPM on) ────────────────────────

        [Fact]
        public void LzmaBlock_DefaultWriteEndMark_RoundTrip()
        {
            // null WriteEndMark → codec default (1, EOPM written for self-contained blocks)
            roundTripLzmaBlock(_testData, writeEndMark: null);
        }

        // ── Block mode — explicit WriteEndMark=1 ────────────────────────────

        [Fact]
        public void LzmaBlock_WriteEndMarkOne_RoundTrip()
        {
            roundTripLzmaBlock(_testData, writeEndMark: 1);
        }

        // ── Block mode — explicit WriteEndMark=0 ────────────────────────────

        [Fact]
        public void LzmaBlock_WriteEndMarkZero_RoundTrip()
        {
            // Suppressing EOPM is safe in block mode because the decompress side uses
            // explicit sizes rather than relying on the end marker.
            roundTripLzmaBlock(_testData, writeEndMark: 0);
        }

        /// <summary>
        /// Block mode with WriteEndMark=0 produces a smaller compressed payload than
        /// the default WriteEndMark=1 (EOPM is several bytes appended after the payload).
        /// </summary>
        [Fact]
        public void LzmaBlock_WriteEndMarkZero_IsSmallerThanDefault()
        {
            byte[] withEopm = compressLzmaBlock(_testData, writeEndMark: null);
            byte[] withoutEopm = compressLzmaBlock(_testData, writeEndMark: 0);

            Assert.True(withoutEopm.Length < withEopm.Length,
                $"Expected no-EOPM block ({withoutEopm.Length} bytes) < default EOPM block ({withEopm.Length} bytes)");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static (byte[] compressed, byte[] properties) compressLzmaStream(byte[] data, int? writeEndMark)
        {
            var options = new CompressionOptions
            {
                Type = CompressionType.Fastest,
                LeaveOpen = true,
                BufferSize = 64 * 1024,
                Dictionary = writeEndMark.HasValue
                    ? new CompressionDictionaryOptions { WriteEndMark = writeEndMark }
                    : null
            };

            using (var ms = new MemoryStream())
            {
                byte[] props;
                using (var stream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, ms, options))
                {
                    stream.Write(data, 0, data.Length);
                    stream.Complete();
                    props = stream.Properties;
                }
                return (ms.ToArray(), props);
            }
        }

        private static byte[] decompressLzmaStream(byte[] compressed, int expectedSize, byte[] properties)
        {
            var options = new CompressionOptions
            {
                Type = CompressionType.Decompress,
                LeaveOpen = true,
                InitProperties = properties
            };

            using (var ms = new MemoryStream(compressed))
            using (var stream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma, ms, options))
            {
                byte[] result = new byte[expectedSize];
                int total = 0, read;
                while (total < expectedSize && (read = stream.Read(result, total, expectedSize - total)) > 0)
                    total += read;
                return result;
            }
        }

        private static void roundTripLzmaBlock(byte[] data, int? writeEndMark)
        {
            var compOptions = new CompressionOptions
            {
                Type = CompressionType.Fastest,
                BlockSize = data.Length,
                Dictionary = writeEndMark.HasValue
                    ? new CompressionDictionaryOptions { WriteEndMark = writeEndMark }
                    : null
            };

            // Use the same block instance for both compress and decompress so that
            // Properties (5-byte LZMA header) set during Compress is available for Decompress.
            using (CompressionBlock block = CompressionBlockFactory.Create(CompressionAlgorithm.Lzma, compOptions))
            {
                byte[] compressed = BufferPool.Rent(block.RequiredCompressOutputSize);
                byte[] decompressed = BufferPool.Rent(data.Length + 32);
                try
                {
                    int cLen = compressed.Length;
                    var cr = block.Compress(data, 0, data.Length, compressed, 0, ref cLen);
                    Assert.Equal(CompressionResultCode.Success, cr);

                    int dLen = decompressed.Length;
                    var dr = block.Decompress(compressed, 0, cLen, decompressed, 0, ref dLen);
                    Assert.Equal(CompressionResultCode.Success, dr);
                    Assert.Equal(data.Length, dLen);
                    Assert.Equal(
                        XXHash64.Compute(data, 0, data.Length),
                        XXHash64.Compute(decompressed, 0, dLen));
                }
                finally
                {
                    BufferPool.Return(compressed);
                    BufferPool.Return(decompressed);
                }
            }
        }

        private static byte[] compressLzmaBlock(byte[] data, int? writeEndMark)
        {
            var compOptions = new CompressionOptions
            {
                Type = CompressionType.Fastest,
                BlockSize = data.Length,
                Dictionary = writeEndMark.HasValue
                    ? new CompressionDictionaryOptions { WriteEndMark = writeEndMark }
                    : null
            };

            using (CompressionBlock block = CompressionBlockFactory.Create(CompressionAlgorithm.Lzma, compOptions))
            {
                byte[] compressed = BufferPool.Rent(block.RequiredCompressOutputSize);
                try
                {
                    int cLen = compressed.Length;
                    var cr = block.Compress(data, 0, data.Length, compressed, 0, ref cLen);
                    Assert.Equal(CompressionResultCode.Success, cr);

                    byte[] result = new byte[cLen];
                    Array.Copy(compressed, result, cLen);
                    return result;
                }
                finally
                {
                    BufferPool.Return(compressed);
                }
            }
        }
    }
}

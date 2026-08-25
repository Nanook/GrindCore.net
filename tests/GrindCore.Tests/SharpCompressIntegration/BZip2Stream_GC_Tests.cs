using GrindCore.Tests.Utility;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace GrindCore.Tests.SharpCompressIntegration
{
    /// <summary>
    /// Exercises the SharpCompress-compatible BZip2Stream shim (BZip2Stream_GC.cs) end-to-end,
    /// confirming it's usable as a drop-in for SharpCompress.Compressors.BZip2.BZip2Stream
    /// through its real Create/Finish/IsBZip2 API surface, not just that it compiles.
    /// </summary>
    public sealed class BZip2Stream_GC_Tests
    {
        [Fact]
        public void CompressThenDecompress_RoundTrips()
        {
            byte[] data = TestPseudoTextStream.Create(64 * 1024);

            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var bz = BZip2Stream.Create(ms, CompressionMode.Compress, decompressConcatenated: false, leaveOpen: true))
                {
                    bz.Write(data, 0, data.Length);
                    bz.Finish();
                }
                compressed = ms.ToArray();
            }

            Assert.True(BZip2Stream.IsBZip2(new MemoryStream(compressed)), "Compressed output should start with a BZip2 (\"BZ\") header.");

            using (var ms = new MemoryStream(compressed))
            using (var bz = BZip2Stream.Create(ms, CompressionMode.Decompress, decompressConcatenated: false))
            using (var outMs = new MemoryStream())
            {
                bz.CopyTo(outMs);
                byte[] result = outMs.ToArray();

                Assert.Equal(data.Length, result.Length);
                Assert.True(result.AsSpan().SequenceEqual(data));
            }
        }

        [Fact]
        public void IsBZip2_RejectsNonBZip2Data()
        {
            byte[] notBzip2 = { 0x00, 0x01, 0x02, 0x03 };
            Assert.False(BZip2Stream.IsBZip2(new MemoryStream(notBzip2)));
        }

        [Fact]
        public void Mode_ReflectsConstructionParameter()
        {
            using (var ms = new MemoryStream())
            using (var bz = BZip2Stream.Create(ms, CompressionMode.Compress, decompressConcatenated: false, leaveOpen: true))
                Assert.Equal(CompressionMode.Compress, bz.Mode);
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER
        [Fact]
        public async Task CompressThenDecompress_Async_RoundTrips()
        {
            byte[] data = TestPseudoTextStream.Create(64 * 1024);

            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                await using (var bz = await BZip2Stream.CreateAsync(ms, CompressionMode.Compress, decompressConcatenated: false, leaveOpen: true))
                {
                    await bz.WriteAsync(data, 0, data.Length);
                    await bz.FinishAsync();
                }
                compressed = ms.ToArray();
            }

            Assert.True(await BZip2Stream.IsBZip2Async(new MemoryStream(compressed)));

            using (var ms = new MemoryStream(compressed))
            await using (var bz = await BZip2Stream.CreateAsync(ms, CompressionMode.Decompress, decompressConcatenated: false))
            using (var outMs = new MemoryStream())
            {
                await bz.CopyToAsync(outMs);
                byte[] result = outMs.ToArray();

                Assert.Equal(data.Length, result.Length);
                Assert.True(result.AsSpan().SequenceEqual(data));
            }
        }

        [Fact]
        public async Task IsBZip2Async_RejectsNonBZip2Data()
        {
            byte[] notBzip2 = { 0x00, 0x01, 0x02, 0x03 };
            Assert.False(await BZip2Stream.IsBZip2Async(new MemoryStream(notBzip2)));
        }
#endif
    }
}

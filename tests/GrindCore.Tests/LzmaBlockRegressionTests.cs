using System;
using Xunit;
using Nanook.GrindCore;
using Nanook.GrindCore.XXHash;

namespace GrindCore.Tests
{
    public class LzmaBlockRegressionTests
    {
        [Fact]
        public void LzmaBlock_Roundtrip_Fuzz_NoShortDecode()
        {
            using (CompressionBlock block = CompressionBlockFactory.Create(CompressionAlgorithm.Lzma, CompressionType.Optimal, 64 * 1024))
            {
                var rnd = new Random(123456);

                for (int i = 0; i < 2000; i++)
                {
                    int len = rnd.Next(0, 64 * 1024);
                    byte[] src = new byte[len];
                    rnd.NextBytes(src);

                    byte[] compressed = BufferPool.Rent(block.RequiredCompressOutputSize);
                    byte[] decompressed = BufferPool.Rent(len + 32);

                    try
                    {
                        int cLen = compressed.Length;
                        var cr = block.Compress(src, 0, src.Length, compressed, 0, ref cLen);
                        Assert.Equal(CompressionResultCode.Success, cr);

                        int dLen = decompressed.Length;
                        var dr = block.Decompress(compressed, 0, cLen, decompressed, 0, ref dLen);
                        Assert.True(dr == CompressionResultCode.Success, $"Decompress returned error {dr} on iteration {i} (len={len}) cLen={cLen}");

                        ulong srcHash = XXHash64.Compute(src, 0, src.Length);
                        ulong decHash = XXHash64.Compute(decompressed, 0, dLen);
                        Assert.True(dLen == len, $"Decompressed length mismatch on iteration {i} expected={len} got={dLen} cLen={cLen} srcHash={srcHash:x16} decHash={decHash:x16}");
                    }
                    finally
                    {
                        BufferPool.Return(compressed);
                        BufferPool.Return(decompressed);
                    }
                }
            }
        }
    }
}

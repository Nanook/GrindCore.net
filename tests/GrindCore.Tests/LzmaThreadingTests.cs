using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Xunit;
using Xunit.Abstractions;

namespace GrindCore.Tests
{
    /// <summary>
    /// ThreadCount for LZMA/LZMA2 (the GrindCore audit's lzma.md 3.7). Lzma2Stream with ThreadCount &gt; 1 encodes batches
    /// of whole 7-Zip blocks through Lzma2Enc_Encode2 (7-Zip's own threaded encoder), so its output depends on the
    /// settings and the data only, not on how the caller writes; LzmaBlock and Lzma2Block use 7-Zip's two-thread match
    /// finder, which gives byte-identical output; ThreadCount 1 or unset is unchanged (solid, the multi-call hook).
    /// </summary>
    public sealed class LzmaThreadingTests
    {
        private readonly ITestOutputHelper _out;
        public LzmaThreadingTests(ITestOutputHelper output) { _out = output; }

        /// <summary>Text with stretches of noise: compressible and incompressible blocks.</summary>
        private static byte[] Mixed(int n, int seed)
        {
            var r = new Random(seed);
            string[] words = { "grindcore", "deflate", "inflate", "window", "block", "stream", "huffman", "\n", "zlib" };
            var ms = new MemoryStream(n + 64);
            while (ms.Length < n)
            {
                int k = r.Next(1, 40000);
                if (r.Next(4) == 0)
                {
                    var b = new byte[k];
                    r.NextBytes(b);
                    ms.Write(b, 0, b.Length);
                }
                else
                {
                    var sb = new StringBuilder(k + 16);
                    while (sb.Length < k)
                        sb.Append(words[r.Next(words.Length)]).Append(' ');
                    var b = Encoding.ASCII.GetBytes(sb.ToString());
                    ms.Write(b, 0, b.Length);
                }
            }
            return ms.ToArray().Take(n).ToArray();
        }

        // At most 2 threads in a 32-bit process. Each block thread has its own 7-Zip encoder (about 400 MB at Level5's
        // 32 MiB dictionary), and 8 or 16 threads without a BlockSize make Lzma2Stream rent batch buffers of 256-512 MiB:
        // together more than 2 GB of address space holds (the GrindCore audit's lzma.md 3.7.2).
        private static int? Threads(int? threads) =>
#if IS_32BIT
            threads > 2 ? 2 : threads;
#else
            threads;
#endif

        private static CompressionOptions Options(int? threads, long? blockSize, int? bufferSize = null) =>
            new CompressionOptions { Type = CompressionType.Level5, LeaveOpen = true, ThreadCount = Threads(threads), BlockSize = blockSize, BufferSize = bufferSize };

        private static (byte[] compressed, byte[] props) Encode(byte[] data, CompressionOptions o, int writeSize, int flushAt = -1)
        {
            var ms = new MemoryStream();
            byte[] props;
            using (var s = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, ms, o))
            {
                for (int i = 0; i < data.Length; i += writeSize)
                {
                    int n = Math.Min(writeSize, data.Length - i);
                    if (flushAt > i && flushAt < i + n)
                    {
                        s.Write(data, i, flushAt - i);
                        s.Flush();
                        s.Write(data, flushAt, i + n - flushAt);
                    }
                    else
                        s.Write(data, i, n);
                }
                s.Complete();
                props = s.Properties;
            }
            return (ms.ToArray(), props);
        }

        private static byte[] Decode(byte[] compressed, byte[] props)
        {
            using var d = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, new MemoryStream(compressed),
                new CompressionOptions { Type = CompressionType.Decompress, LeaveOpen = true, InitProperties = props });
            var o = new MemoryStream();
            d.CopyTo(o);
            return o.ToArray();
        }

        [Theory]
        [InlineData(1, (1L << 20) + 77)]   // block mode on one thread, as CompressionStreamBoundaryTests use it
        [InlineData(2, 4L << 20)]
        [InlineData(4, 8L << 20)]
        [InlineData(8, null)]
        public void Lzma2Stream_ThreadCount_OutputDoesNotDependOnWriteSizes(int threads, long? blockSize)
        {
            byte[] data = Mixed(20 << 20, 1);
            var o = Options(threads, blockSize);
            var (a, props) = Encode(data, o, 1 << 20);
            var (b, _) = Encode(data, Options(threads, blockSize), 7777);
            var (c, _) = Encode(data, Options(threads, blockSize), data.Length);
            Assert.True(a.AsSpan().SequenceEqual(b), "7,777-byte writes gave other bytes than 1 MiB writes");
            Assert.True(a.AsSpan().SequenceEqual(c), "one write gave other bytes than 1 MiB writes");
            Assert.True(Decode(a, props).AsSpan().SequenceEqual(data), "didn't decode back");
        }

        [Fact]
        public void Lzma2Stream_ThreadCount_FlushThenWrite_Decodes()
        {
            byte[] data = Mixed(12 << 20, 2);
            int at = 5_000_001;
            var o = Options(4, 8L << 20);
            var ms = new MemoryStream();
            byte[] props;
            byte[] afterFlush;
            using (var s = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, ms, o))
            {
                s.Write(data, 0, at);
                s.Flush();
                afterFlush = ms.ToArray();
                s.Write(data, at, data.Length - at);
                s.Complete();
                props = s.Properties;
            }
            // Everything written before the flush is decodable from what had been written by then, plus an end byte
            Assert.True(Decode(afterFlush.Concat(new byte[] { 0 }).ToArray(), props).AsSpan().SequenceEqual(data.AsSpan(0, at)),
                "the stream up to the flush didn't decode to the data written before it");
            Assert.True(Decode(ms.ToArray(), props).AsSpan().SequenceEqual(data), "the whole stream didn't decode");
        }

        [Fact]
        public void Lzma2Stream_ThreadCountOneOrUnset_StaysSolid()
        {
            byte[] data = Mixed(3 << 20, 3);
            var (unset, p) = Encode(data, Options(null, null), 1 << 20);
            var (one, _) = Encode(data, Options(1, null), 1 << 20);
            var (explicitSolid, _) = Encode(data, Options(4, -1), 1 << 20);
            Assert.True(unset.AsSpan().SequenceEqual(one), "ThreadCount 1 differs from unset");
            Assert.True(unset.AsSpan().SequenceEqual(explicitSolid), "BlockSize -1 with ThreadCount 4 isn't solid");
            Assert.True(Decode(unset, p).AsSpan().SequenceEqual(data));
        }

        [Fact]
        public void Lzma2Stream_ThreadCount_EmptyAndTiny()
        {
            foreach (int n in new[] { 0, 1, 1000 })
            {
                byte[] data = Mixed(n, 4);
                var (c, p) = Encode(data, Options(4, 8L << 20), 4096);
                Assert.True(Decode(c, p).AsSpan().SequenceEqual(data), $"{n} bytes didn't round-trip");
                if (n == 0)
                    Assert.Equal(new byte[] { 0 }, c);
            }
        }

        // A measurement: it asserts only round trips, which the tests above make at 1-8 threads on 3-20 MiB (16 threads is
        // the same path as 8, with a larger batch). Its 16-thread step allocates about 1 GiB of batch buffers (the GrindCore
        // audit's lzma.md 3.7.2), and beside the other classes it ran out of memory in 4 and 6 GiB containers. So it runs
        // only with GRINDCORE_BENCHMARKS=1
        [BenchmarkFact]
        public void Lzma2Stream_ThreadCount_Speed()
        {
#if IS_32BIT
            // 16 MiB in a 32-bit process: on 64 MiB, each step's buffers and encoders together outgrow its 2 GB (lzma.md 3.7.2)
            byte[] data = Mixed(16 << 20, 5);
#else
            byte[] data = Mixed(64 << 20, 5);
#endif
            foreach (var (threads, block) in new (int?, long?)[] { (null, null), (8, null), (16, null), (8, 32L << 20) })
            {
                var o = Options(threads, block);
                var sw = Stopwatch.StartNew();
                var (c, p) = Encode(data, o, 1 << 20);
                double sec = sw.Elapsed.TotalSeconds;
                bool ok = Decode(c, p).AsSpan().SequenceEqual(data);
                _out.WriteLine($"ThreadCount {o.ThreadCount?.ToString() ?? "-"}, BlockSize {block?.ToString() ?? "-"}: {sec:F2} s, " +
                               $"{data.Length / sec / (1 << 20):F1} MiB/s, ratio {(double)c.Length / data.Length:F5}, decodes {ok}");
                Assert.True(ok);
            }
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma)]
        [InlineData(CompressionAlgorithm.Lzma2)]
        public void Block_ThreadCount_GivesIdenticalOutput(CompressionAlgorithm alg)
        {
            byte[] data = Mixed(6 << 20, 6);
            byte[]? first = null;
            foreach (int? threads in new int?[] { null, 1, 2, 8 })
            {
                using var block = CompressionBlockFactory.Create(alg, new CompressionOptions { Type = CompressionType.Level5, BlockSize = data.Length, ThreadCount = threads });
                byte[] dst = new byte[block.RequiredCompressOutputSize];
                int n = dst.Length;
                var sw = Stopwatch.StartNew();
                Assert.Equal(CompressionResultCode.Success, block.Compress(data, 0, data.Length, dst, 0, ref n));
                _out.WriteLine($"{alg} ThreadCount {threads?.ToString() ?? "-"}: {sw.Elapsed.TotalSeconds:F2} s, {n} bytes");
                byte[] output = dst.AsSpan(0, n).ToArray();
                first ??= output;
                Assert.True(output.AsSpan().SequenceEqual(first), $"ThreadCount {threads} changed the output");
                byte[] back = new byte[data.Length + 64];
                int m = back.Length;
                Assert.Equal(CompressionResultCode.Success, block.Decompress(output, 0, output.Length, back, 0, ref m));
                Assert.True(back.AsSpan(0, m).SequenceEqual(data));
            }
        }
    }
}

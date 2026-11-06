using System;
using System.IO;
using System.Linq;
using Nanook.GrindCore;
using Nanook.GrindCore.Lzma;
using Xunit;

namespace GrindCore.Tests
{
#if !IS_32BIT_ARM // all tests in this class cause a GC stack overflow in QEMU arm32 (SharedArrayPool.Trim recursion)
    /// <summary>
    /// Writing after Flush() on Lzma2Stream in solid mode (the GrindCore audit's lzma.md 3.1, NDZ's report). Before the
    /// native fix, everything written after the first flush was lost: the native match finder latched the end of its
    /// input when a flush drained it. Flush() is now a sync flush: the stream so far decodes to everything written, and
    /// the stream goes on with its dictionary.
    /// </summary>
    public sealed class Lzma2FlushTests
    {
        private static byte[] Noise(int n, int seed)
        {
            var b = new byte[n];
            new Random(seed).NextBytes(b);
            return b;
        }

        /// <summary>Records repeated with small changes: what follows a flush matches deep into what came before it.</summary>
        private static byte[] Records(int n, int seed)
        {
            var r = new Random(seed);
            var pool = Enumerable.Range(0, 24).Select(i => Noise(r.Next(40, 600), seed * 100 + i)).ToArray();
            var ms = new MemoryStream(n + 600);
            while (ms.Length < n)
            {
                var rec = (byte[])pool[r.Next(pool.Length)].Clone();
                for (int k = r.Next(4); k > 0; k--)
                    rec[r.Next(rec.Length)] = (byte)r.Next(256);
                ms.Write(rec, 0, rec.Length);
            }
            return ms.ToArray().Take(n).ToArray();
        }

        private static CompressionOptions Options(CompressionType level) =>
            new CompressionOptions { Type = level, LeaveOpen = true };

        /// <summary>Writes each part, flushing after each one, then completes: (stream, properties, length after each flush).</summary>
        private static (byte[] stream, byte[] props, long[] flushed) Encode(CompressionType level, params byte[][] parts)
        {
            var ms = new MemoryStream();
            var flushed = new long[parts.Length];
            byte[] props;
            using (var s = new Lzma2Stream(ms, Options(level)))
            {
                for (int i = 0; i < parts.Length; i++)
                {
                    s.Write(parts[i], 0, parts[i].Length);
                    s.Flush();
                    flushed[i] = ms.Length;
                }
                s.Complete();
                props = s.Properties;
            }
            return (ms.ToArray(), props, flushed);
        }

        private static byte[] Decode(byte[] stream, byte[] props)
        {
            using var d = new Lzma2Stream(new MemoryStream(stream),
                new CompressionOptions { Type = CompressionType.Decompress, LeaveOpen = true, InitProperties = props });
            var o = new MemoryStream();
            d.CopyTo(o);
            return o.ToArray();
        }

        [Theory]
        [InlineData(CompressionType.Level1)]
        [InlineData(CompressionType.Level5)]
        [InlineData(CompressionType.Level9)]
        public void WriteAfterFlushIsKept(CompressionType level)
        {
            // NDZ's sequence: 100,000 random bytes, Flush(), the same bytes again, Flush(), then the end
            var a = Noise(100000, 1);
            var (stream, props, flushed) = Encode(level, a, a);
            Assert.True(flushed[1] > flushed[0], "the second flush wrote nothing");
            Assert.Equal(a.Concat(a).ToArray(), Decode(stream, props));
        }


        [Theory]
        [InlineData(CompressionType.Level1)]
        [InlineData(CompressionType.Level5)]
        [InlineData(CompressionType.Level9)]
        public void FlushIsSyncFlushAndKeepsDictionary(CompressionType level)
        {
            var a = Records(200 << 10, 5);
            var (stream, props, flushed) = Encode(level, a, a);
            // the stream up to the first flush, plus the end byte, decodes to exactly what was written by then
            var prefix = stream.Take((int)flushed[0]).Concat(new byte[] { 0x00 }).ToArray();
            Assert.Equal(a, Decode(prefix, props));
            Assert.Equal(a.Concat(a).ToArray(), Decode(stream, props));
            // the repeat is found in the dictionary across the flush
            Assert.True(flushed[1] - flushed[0] < flushed[0] / 20,
                $"the repeat cost {flushed[1] - flushed[0]} bytes, the original {flushed[0]}");
        }

        [Theory]
        [InlineData(CompressionType.Level1, 7)]
        [InlineData(CompressionType.Level5, 8)]
        [InlineData(CompressionType.Level9, 9)]
        public void ManyFlushesRoundTrip(CompressionType level, int seed)
        {
            var data = Records(768 << 10, seed);
            var r = new Random(seed);
            var parts = new System.Collections.Generic.List<byte[]>();
            for (int i = 0; i < data.Length;)
            {
                int n = Math.Min(data.Length - i, 1 + r.Next(16 << 10));
                var part = new byte[n];
                Array.Copy(data, i, part, 0, n);
                parts.Add(part);
                i += n;
            }
            var (stream, props, _) = Encode(level, parts.ToArray());
            Assert.Equal(data, Decode(stream, props));
        }
    }
#endif
}

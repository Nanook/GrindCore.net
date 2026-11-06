using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Nanook.GrindCore;
using Xunit;

namespace GrindCore.Tests
{
#if !IS_32BIT_ARM // stack overflow in QEMU arm32 (MemoryStream.EnsureCapacity recursion via GC)
    /// <summary>
    /// Reading LZMA and LZMA2 streams from a source that returns less than asked for, as a pipe, a network stream or
    /// SharpCompress's BufferedSubStream (the source of every 7z coder) do (the GrindCore audit's lzma.md 3.6.1 L1-L3,
    /// L6). The streams ended at the first decode call that wrote nothing, and LZMA2 parsed its chunk headers from the
    /// bytes it asked for rather than those it got, so such reads truncated the output with no error.
    /// </summary>
    public sealed class LzmaStreamReadTests
    {
        /// <summary>Text with stretches of noise: compressed and uncompressed chunks.</summary>
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

        /// <summary>4 KiB pages, half of them noise and half text, as a disc image is: large chunks.</summary>
        private static byte[] Pages(int n, int seed)
        {
            var r = new Random(seed);
            string[] words = { "grindcore", "deflate", "inflate", "window", "block", "stream", "huffman", "\n", "zlib" };
            byte[] data = new byte[n];
            for (int p = 0; p < n; p += 4096)
            {
                if (r.Next(2) == 0)
                {
                    var b = new byte[4096];
                    r.NextBytes(b);
                    Array.Copy(b, 0, data, p, 4096);
                }
                else
                    for (int i = 0; i < 4096;)
                        foreach (char ch in words[r.Next(words.Length)] + " ")
                            if (i < 4096)
                                data[p + i++] = (byte)ch;
            }
            return data;
        }

        private static byte[] Noise(int n, int seed)
        {
            var b = new byte[n];
            new Random(seed).NextBytes(b);
            return b;
        }

        /// <summary>A source whose Read returns at most <c>step</c> bytes.</summary>
        private sealed class Trickle : Stream
        {
            private readonly byte[] _data;
            private readonly int _step;
            private long _pos;
            public Trickle(byte[] data, int step) { _data = data; _step = step; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = (int)Math.Min(Math.Min(count, _step), _data.Length - _pos);
                Array.Copy(_data, _pos, buffer, offset, n);
                _pos += n;
                return n;
            }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _data.Length;
            public override long Position { get => _pos; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <summary>
        /// As SharpCompress's BufferedSubStream: a Read returns at most what is left of a 128 KiB cache, so a read that
        /// crosses a refill comes back short.
        /// </summary>
        private sealed class CacheRefill : Stream
        {
            private readonly byte[] _data;
            private long _pos;
            public CacheRefill(byte[] data) { _data = data; }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int n = (int)Math.Min(Math.Min(count, 131072 - _pos % 131072), _data.Length - _pos);
                Array.Copy(_data, _pos, buffer, offset, n);
                _pos += n;
                return n;
            }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _data.Length;
            public override long Position { get => _pos; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        // A 1 MiB dictionary by default, which these sizes need at most. Level 5's own, 32 MiB, isn't reduced for a stream
        // of unknown size, and took the class's peak about 100 MiB higher
        private static (byte[] compressed, byte[] props) Compress(CompressionAlgorithm alg, byte[] data, CompressionType type = CompressionType.Level5,
            long? dictionarySize = 1 << 20)
        {
            var ms = new MemoryStream();
            byte[] props;
            // LZMA with its end mark, so the stream says where it ends
            var o = new CompressionOptions
            {
                Type = type, LeaveOpen = true,
                Dictionary = alg == CompressionAlgorithm.Lzma || dictionarySize != null
                    ? new CompressionDictionaryOptions { DictionarySize = dictionarySize, WriteEndMark = alg == CompressionAlgorithm.Lzma ? 1 : (int?)null }
                    : null
            };
            using (var s = CompressionStreamFactory.Create(alg, ms, o))
            {
                s.Write(data, 0, data.Length);
                s.Complete();
                props = s.Properties;
            }
            return (ms.ToArray(), props);
        }

        private static CompressionStream Decoder(CompressionAlgorithm alg, Stream source, byte[] props, int? bufferSize = null) =>
            CompressionStreamFactory.Create(alg, source, new CompressionOptions
                { Type = CompressionType.Decompress, LeaveOpen = true, InitProperties = props, BufferSize = bufferSize });

        /// <summary>Reads to the end on a worker thread, so a stream that stops making progress fails the test instead of hanging it.</summary>
        private static byte[] ReadAll(Func<Stream> open, bool async = false, int readSize = 81920)
        {
            var result = new MemoryStream();
            Exception? error = null;
            var t = new Thread(() =>
            {
                try
                {
                    using (var d = open())
                    {
                        if (async)
                            d.CopyToAsync(result, readSize).GetAwaiter().GetResult();
                        else
                        {
                            var buf = new byte[readSize];
                            int r;
                            while ((r = d.Read(buf, 0, buf.Length)) > 0)
                                result.Write(buf, 0, r);
                        }
                    }
                }
                catch (Exception e) { error = e; }
            }) { IsBackground = true };
            t.Start();
            Assert.True(t.Join(TimeSpan.FromSeconds(60)), "the decoder didn't finish in 60 s");
            Assert.Null(error);
            return result.ToArray();
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma, 1)]
        [InlineData(CompressionAlgorithm.Lzma, 4)]
        [InlineData(CompressionAlgorithm.Lzma, 16)]
        [InlineData(CompressionAlgorithm.Lzma, 1024)]
        [InlineData(CompressionAlgorithm.Lzma, 65537)]
        [InlineData(CompressionAlgorithm.Lzma2, 1)]
        [InlineData(CompressionAlgorithm.Lzma2, 4)]
        [InlineData(CompressionAlgorithm.Lzma2, 16)]
        [InlineData(CompressionAlgorithm.Lzma2, 1024)]
        [InlineData(CompressionAlgorithm.Lzma2, 65537)]
        public void ShortSourceReads_DecodeFully(CompressionAlgorithm alg, int step)
        {
            byte[] data = Mixed(300_000, 1);
            var (c, props) = Compress(alg, data);
            Assert.True(ReadAll(() => Decoder(alg, new Trickle(c, step), props)).AsSpan().SequenceEqual(data));
        }

        [Theory]
        [InlineData(CompressionAlgorithm.Lzma, 1)]
        [InlineData(CompressionAlgorithm.Lzma, 1024)]
        [InlineData(CompressionAlgorithm.Lzma2, 1)]
        [InlineData(CompressionAlgorithm.Lzma2, 1024)]
        public void ShortSourceReads_DecodeFully_Async(CompressionAlgorithm alg, int step)
        {
            byte[] data = Mixed(300_000, 2);
            var (c, props) = Compress(alg, data);
            Assert.True(ReadAll(() => Decoder(alg, new Trickle(c, step), props), async: true).AsSpan().SequenceEqual(data));
        }

        // As in a 7z archive read through GrindCore.SharpCompress (lzma.md 3.6.2): chunks cross the 128 KiB refills. Seed 14
        // is one that truncated there (to 2,300,930 of 4,194,304 bytes): a byte inside a chunk, taken for a control byte,
        // was 0x00. Level 1's own dictionary, as that was found with (small: 256 KiB for LZMA2)
        [Theory]
        [InlineData(CompressionAlgorithm.Lzma)]
        [InlineData(CompressionAlgorithm.Lzma2)]
        public void SevenZipSubStreamReads_DecodeFully(CompressionAlgorithm alg)
        {
            byte[] data = Pages(4 << 20, 14);
            var (c, props) = Compress(alg, data, CompressionType.Level1, dictionarySize: null);
            Assert.True(ReadAll(() => Decoder(alg, new CacheRefill(c), props, 0x200000), readSize: 10 << 20).AsSpan().SequenceEqual(data));
        }

        // The source isn't read past LZMA2's 0x00 end byte, whatever follows it (an XZ block's padding and check do), even
        // when the call that takes the end byte also writes the last of the output (lzma.md 3.6.1 L3)
        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(65537)]
        public void Lzma2_SourceIsReadUpToTheEndByte(int step)
        {
            foreach (int run in new[] { 300, 4000 })
            {
                byte[] data = Noise(1000, run).Concat(Enumerable.Repeat((byte)'A', run)).ToArray();
                var (c, props) = Compress(CompressionAlgorithm.Lzma2, data);
                byte[] withTail = c.Concat(Enumerable.Repeat((byte)0xEE, 4096)).ToArray();
                for (int k = 1; k <= 300; k += 37)
                {
                    var src = new Trickle(withTail, step);
                    var got = new MemoryStream();
                    using (var d = Decoder(CompressionAlgorithm.Lzma2, src, props))
                    {
                        var buf = new byte[data.Length + 8192];
                        int r = d.Read(buf, 0, data.Length - k);
                        got.Write(buf, 0, r);
                        while ((r = d.Read(buf, 0, buf.Length)) > 0)
                            got.Write(buf, 0, r);
                    }
                    Assert.True(got.ToArray().AsSpan().SequenceEqual(data), $"run {run}, first read {data.Length - k}: wrong output");
                    Assert.Equal(c.Length, src.Position);
                }
            }
        }

        // A BufferSize under one LZMA2 chunk (64 KiB of data plus its header), or 0, decoded nothing or threw (lzma.md 3.6.1 L6)
        [Theory]
        [InlineData(CompressionAlgorithm.Lzma, 0)]
        [InlineData(CompressionAlgorithm.Lzma, 4096)]
        [InlineData(CompressionAlgorithm.Lzma2, 0)]
        [InlineData(CompressionAlgorithm.Lzma2, 4096)]
        [InlineData(CompressionAlgorithm.Lzma2, 16384)]
        [InlineData(CompressionAlgorithm.Lzma2, 65536)]
        public void SmallBufferSize_DecodesFully(CompressionAlgorithm alg, int bufferSize)
        {
            byte[] data = Noise(300_000, 4); // incompressible: LZMA2 stores it in full 64 KiB chunks
            var (c, props) = Compress(alg, data);
            Assert.True(ReadAll(() => Decoder(alg, new MemoryStream(c), props, bufferSize)).AsSpan().SequenceEqual(data));
        }
    }
#endif
}

using GrindCore.Tests.Utility;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Nanook.GrindCore.XXHash;
using Nanook.GrindCore;
using System.Diagnostics;

namespace GrindCore.Tests
{
    public class CompressionBufferTests
    {
        [Fact]
        public void BufferFullReadResetTest()
        {
            CompressionBuffer buff = new CompressionBuffer(0x1000);
            byte[] src = TestDataStream.Create(buff.AvailableWrite);
            byte[] dst = new byte[0x1000];
            Assert.Equal(0x1000, src.Length);

            int written = buff.Write(src, 0, 0x100);

            Assert.Equal(0x100, written);
            Assert.Equal(0x0, buff.Pos);
            Assert.Equal(0x100, buff.Size);
            Assert.Equal(0x100, buff.AvailableRead);
            Assert.Equal(0x1000 - 0x100, buff.AvailableWrite);
            Assert.True(src.Take(0x100).SequenceEqual(buff.Data.Take(0x100)));
            Assert.True((new byte[src.Length - 0x100]).SequenceEqual(buff.Data.Skip(0x100)));

            int read = buff.Read(dst, 0, 0x100);

            Assert.Equal(0x100, read);
            Assert.Equal(0x0, buff.Pos); //all bytes read so pos should be reset
            Assert.Equal(0x0, buff.Size);
            Assert.Equal(0x0, buff.AvailableRead);
            Assert.Equal(0x1000, buff.AvailableWrite); //full size available
            Assert.True(src.Take(0x100).SequenceEqual(dst.Take(0x100)));
            Assert.True((new byte[src.Length - 0x100]).SequenceEqual(dst.Skip(0x100)));
        }

        [Fact]
        public void BufferPartialResetTest()
        {
            CompressionBuffer buff = new CompressionBuffer(0x1000);
            byte[] src = TestDataStream.Create(buff.AvailableWrite);
            byte[] dst = new byte[0x1000];
            Assert.Equal(0x1000, src.Length);

            int written = buff.Write(src, 0, 0x1000);

            Assert.Equal(0x1000, written);
            Assert.Equal(0x0, buff.Pos);
            Assert.Equal(0x1000, buff.Size);
            Assert.Equal(0x1000, buff.AvailableRead);
            Assert.Equal(0x0, buff.AvailableWrite);
            Assert.True(src.SequenceEqual(buff.Data));

            int read = buff.Read(dst, 0, 0x600);

            Assert.Equal(0x600, read);
            Assert.Equal(0x600, buff.Pos); //all bytes read so pos should be reset
            Assert.Equal(0x1000, buff.Size);
            Assert.Equal(0xa00, buff.AvailableRead);
            Assert.Equal(0x0, buff.AvailableWrite); //full size available


            read = buff.Read(dst, 0x600, 0x600); //leaves 0x400 remaining which causes the remaining data to be pulled back to pos 0

            Assert.Equal(0x600, read);
            Assert.Equal(0x0, buff.Pos); //Size <= 0x400 so pos should be reset
            Assert.Equal(0x400, buff.Size);
            Assert.Equal(0x400, buff.AvailableRead);
            Assert.Equal(0xc00, buff.AvailableWrite); //full size available
            Assert.True(src.Take(0xc00).SequenceEqual(dst.Take(0xc00)));
            Assert.True((new byte[src.Length - 0xc00]).SequenceEqual(dst.Skip(0xc00))); //remaining 0x400 is still empty
        }

        [Fact]
        public void BufferFullReadWriteTest()
        {
            CompressionBuffer buff = new CompressionBuffer(0x1000);
            byte[] src = TestDataStream.Create(buff.AvailableWrite);
            byte[] dst = new byte[0x1000];
            Assert.Equal(0x1000, src.Length);

            int written = buff.Write(src, 0, 0x1000);

            Assert.Equal(0x1000, written);
            Assert.Equal(0x0, buff.Pos);
            Assert.Equal(0x1000, buff.Size);
            Assert.Equal(0x1000, buff.AvailableRead);
            Assert.Equal(0x0, buff.AvailableWrite);
            Assert.True(src.SequenceEqual(buff.Data));

            int read = buff.Read(dst, 0, 0x1000);

            Assert.Equal(0x1000, read);
            Assert.Equal(0x0, buff.Pos); //Size <= 0x400 so pos should be reset
            Assert.Equal(0x0, buff.Size);
            Assert.Equal(0x0, buff.AvailableRead);
            Assert.Equal(0x1000, buff.AvailableWrite); //full size available
            Assert.True(src.SequenceEqual(dst));
        }
    }
}

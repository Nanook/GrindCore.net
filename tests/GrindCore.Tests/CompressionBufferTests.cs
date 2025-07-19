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
        private void testEmpty(CompressionBuffer buff)
        {
            //buff.Data.Length should not be used and is only for these tests
            Assert.Equal(0x0, buff.Pos);
            Assert.Equal(0x0, buff.Size);
            Assert.Equal(buff.Data.Length, buff.MaxSize);
            Assert.Equal(0x0, buff.AvailableRead);
            Assert.Equal(buff.Data.Length, buff.AvailableWrite);
        }

        private void testFullAndNotRead(CompressionBuffer buff)
        {
            //buff.Data.Length should not be used and is only for these tests
            Assert.Equal(0, buff.Pos);
            Assert.Equal(buff.Data.Length, buff.Size);
            Assert.Equal(buff.Data.Length, buff.MaxSize);
            Assert.Equal(buff.Size, buff.AvailableRead); //everything still to read
            Assert.Equal(0, buff.AvailableWrite); //write is maxed out
        }

        private void testPartialWriteAndNotRead(CompressionBuffer buff, int written)
        {
            //buff.Data.Length should not be used and is only for these tests
            Assert.Equal(0, buff.Pos);
            Assert.Equal(written, buff.Size);
            Assert.Equal(buff.Data.Length, buff.MaxSize);
            Assert.Equal(written, buff.AvailableRead); //everything still to read
            Assert.Equal(buff.MaxSize - written, buff.AvailableWrite); //write is maxed out
        }

        private void testFullAndReadButNotReset(CompressionBuffer buff)
        {
            //buff.Data.Length should not be used and is only for these tests
            Assert.Equal(buff.Data.Length, buff.Pos);
            Assert.Equal(buff.Data.Length, buff.Size);
            Assert.Equal(buff.Data.Length, buff.MaxSize);
            Assert.Equal(0x0, buff.AvailableRead); //nothing left to read
            Assert.Equal(buff.Data.Length, buff.AvailableWrite); //everything is maxed out, but still fully available as it will get reset on write
        }


        [Fact]
        public void FullBufferStateTests()
        {
            CompressionBuffer buff = new CompressionBuffer(0x1000);
            byte[] src = TestDataStream.Create(buff.AvailableWrite);
            byte[] dst = new byte[0x1000];

            testEmpty(buff);

            int written = buff.Write(src, 0, 0x1000);

            testFullAndNotRead(buff);

            int read = buff.Read(dst, 0, 0x1000);

            testFullAndReadButNotReset(buff);

            buff.Tidy();

            testEmpty(buff);
        }

        [Fact]
        public void FullBufferWithResetStateTest()
        {
            CompressionBuffer buff = new CompressionBuffer(0x1000);
            byte[] src = TestDataStream.Create(buff.AvailableWrite);
            byte[] dst = new byte[0x1000];

            testEmpty(buff);

            int written = buff.Write(src, 0, 0x1000);

            testFullAndNotRead(buff);

            int read = buff.Read(dst, 0, 0x1000);

            testFullAndReadButNotReset(buff);

            //there is enough room if the read data is tidied up.
            written = buff.Write(src, 0, 0x100);
            Assert.Equal(0x100, written);
            testPartialWriteAndNotRead(buff, written); //buffer now only contains the new 0x100 bytes
        }

        [Fact]
        public void PartialBufferWriteTest()
        {
            CompressionBuffer buff = new CompressionBuffer(0x1000);
            byte[] src = TestDataStream.Create(buff.AvailableWrite);
            byte[] dst = new byte[0x1000];

            Assert.Equal(0x1000, src.Length);

            testEmpty(buff);

            int written = 0;
            for (int i = 1; i <= 0x10; i++)
            {
                written += buff.Write(src, 0, 0x100);
                Assert.Equal(0x100 * i, written);
                testPartialWriteAndNotRead(buff, written);
            }

            testFullAndNotRead(buff);

            int read = buff.Read(dst, 0, 0x1000);
            Assert.Equal(0x1000, read); //we read a full buffer

        }

        [Fact]
        public void BufferPartialResetTheFullTest()
        {
            CompressionBuffer buff = new CompressionBuffer(0x1000);
            byte[] src = TestDataStream.Create(buff.AvailableWrite);
            byte[] dst = new byte[0x1000];
            Assert.Equal(0x1000, src.Length);

            int written = buff.Write(src, 0, 0x1000);
            testFullAndNotRead(buff);
            Assert.True(src.SequenceEqual(buff.Data));

            int read = buff.Read(dst, 0, 0x600);

            Assert.Equal(0x600, read);
            Assert.Equal(0x600, buff.Pos); //all bytes read so pos should be reset
            Assert.Equal(0x1000, buff.Size); //this is at the end of the buffer
            Assert.Equal(0xa00, buff.AvailableRead); //the last 0xa00 is available to read
            Assert.Equal(0x600, buff.AvailableWrite);

            written = buff.Write(src, 0, 0x600);
            testFullAndNotRead(buff);

            read = buff.Read(dst, 0, 0x1000);
            Assert.Equal(0x1000, read); //we read a full buffer
        }

        [Fact]
        public void BufferWriteAndReadTooMuchTest()
        {
            CompressionBuffer buff = new CompressionBuffer(0x1000);
            byte[] src = TestDataStream.Create(buff.AvailableWrite);
            byte[] dst = new byte[0x1100];
            Assert.Equal(0x1000, src.Length);

            int written = buff.Write(src, 0, 0x800);
            testPartialWriteAndNotRead(buff, 0x800);

            written = buff.Write(src, 0, 0x900);
            Assert.Equal(0x800, written); //should only write 800

            testFullAndNotRead(buff);

            int read = buff.Read(dst, 0, 0x1100);
            Assert.Equal(0x1000, read); //should only write 800
            testFullAndReadButNotReset(buff);
            buff.Tidy();
            testEmpty(buff);


        }


    }
}

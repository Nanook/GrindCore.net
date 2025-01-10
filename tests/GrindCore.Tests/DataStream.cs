using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GrindCore.Tests
{
    internal class DataStream : Stream
    {
        private long _position = 0;
        private byte _prev = 0;
        private byte _current = 1;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();

        public static byte[] Create(int size)
        {
            // Initialize a new byte array with the specified size
            byte[] data = new byte[size];
            using (DataStream ds = new DataStream())
            {
                int bufferSize = 64 * 1024; // 64k blocks
                byte[] buffer = new byte[bufferSize];
                int bytesRead = 0;
                int totalBytesRead = 0;

                while (totalBytesRead < size)
                {
                    bytesRead = ds.Read(buffer, 0, Math.Min(bufferSize, size - totalBytesRead));
                    Array.Copy(buffer, 0, data, totalBytesRead, bytesRead);
                    totalBytesRead += bytesRead;
                }
            }

            return data;
        }


        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesRead = 0;
            while (bytesRead < count)
            {
                buffer[offset + bytesRead] = _prev;
                byte next = (byte)((_prev + _current) % 256);
                _prev = _current;
                _current = next;
                bytesRead++;
                _position++;
            }
            return bytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

}

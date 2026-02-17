using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GrindCore.Tests.Utility
{
    /// <summary>
    /// Generates consistent, non-compressible bytes from a deterministic seed.
    /// </summary>
    internal class TestNonCompressibleDataStream : Stream
    {
        private long _position = 0;
        private byte[] _seed;
        private byte[] _currentHash;
        private int _index;

        public TestNonCompressibleDataStream()
        {
            _seed = Encoding.UTF8.GetBytes("FixedSeedForDeterministicOutput");
            _currentHash = new byte[32]; // SHA-256 produces 32-byte hashes
            generateNextBlock();
            _index = 0;
        }

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
            byte[] data = new byte[size];
            using (var ds = new TestNonCompressibleDataStream())
            {
                int bufferSize = 64 * 1024; // 64 KB blocks
                byte[] buffer = new byte[bufferSize];
                int bytesRead;
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
                buffer[offset + bytesRead] = _currentHash[_index];
                _index++;

                if (_index >= _currentHash.Length)
                {
                    generateNextBlock();
                    _index = 0;
                }

                bytesRead++;
                _position++;
            }
            return bytesRead;
        }

        private void generateNextBlock()
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                _currentHash = sha256.ComputeHash(_seed);
                _seed = _currentHash; // Feed previous hash into the next round
            }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    /// <summary>
    /// A light class to wrap a buffer
    /// </summary>
    internal class CompressionBuffer: IDisposable
    {
        public byte[] Data;
        public int Pos;
        public int Size;
        public int TidyThreshold = 0x400;

        public CompressionBuffer(int size)
        {
            Data = BufferPool.Rent(size);
            Size = 0;
        }

        public int AvailableRead => Size - Pos;
        public int AvailableWrite => Data.Length - Size;

        public void Dispose()
        {
            BufferPool.Return(Data);
        }

        public void Tidy()
        {
            if (Size - Pos != 0)
                Array.Copy(Data, Pos, Data, 0, Size - Pos); //move data to start
            Size = Size - Pos;
            Pos = 0;
        }

        /// <summary>
        /// Update if data was modified in the Data buffer by other means
        /// </summary>
        public int Read(int length)
        {
            return Read(null, 0, length);
        }

        public int Read(byte[]? data, int offset, int length)
        {
            int sz = Math.Min(Size - Pos, length);
            if (data != null)
                Array.Copy(Data, Pos, data, offset, sz);
            Pos += sz;
            if (Pos != 0 && Size - Pos <= TidyThreshold)
                Tidy();
            return sz;
        }

        /// <summary>
        /// Update if data was modified in the Data buffer by other means
        /// </summary>
        public int Write(int length)
        {
            return Write(null, 0, length);
        }

        public int Write(byte[]? data, int offset, int length)
        {
            int sz = Math.Min(Data.Length - Size, length);
            if (sz != 0)
            {
                if (data != null)
                    Array.Copy(data, offset, Data, Size, sz);
                Size += sz;
            }
            return sz;
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        public int Read(Span<byte> data)
        {
            int sz = Math.Min(Size - Pos, data.Length);

            // Copy data using Span.CopyTo
            Data.AsSpan(Pos, sz).CopyTo(data);
            Pos += sz;

            if (Pos != 0 && Size - Pos < 0x400)
            {
                // Move data to start using Span.CopyTo
                Data.AsSpan(Pos, Size - Pos).CopyTo(Data.AsSpan());
                Size = Size - Pos;
                Pos = 0;
            }

            return sz;
        }

        public int Write(ReadOnlySpan<byte> data)
        {
            int sz = Math.Min(Data.Length - Size, data.Length);
            if (sz != 0)
            {
                data.CopyTo(Data.AsSpan(Size, sz));
                Size += sz;
            }
            return sz;
        }
#endif
    }
}

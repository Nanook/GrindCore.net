using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    /// <summary>
    /// A lightweight class to wrap an output buffer for compression and decompression operations.
    /// </summary>
    internal class CompressionBuffer : IDisposable
    {
        /// <summary>
        /// Gets the underlying data buffer.
        /// </summary>
        public byte[] Data;
        /// <summary>
        /// Gets or sets the current read/write position in the buffer.
        /// </summary>
        public int Pos;
        /// <summary>
        /// Gets or sets the current size of valid data in the buffer.
        /// </summary>
        public int Size;
        /// <summary>
        /// Gets or sets the threshold at which the buffer is tidied.
        /// </summary>
        public int TidyThreshold = 0x400;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionBuffer"/> class with the specified buffer size.
        /// </summary>
        /// <param name="size">The size of the buffer to allocate.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is not positive.</exception>
        public CompressionBuffer(long size)
        {
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Buffer size must be positive.");
            Data = BufferPool.Rent(size);
            Size = 0;
        }

        /// <summary>
        /// Gets the number of bytes available to read from the buffer.
        /// </summary>
        public int AvailableRead => Size - Pos;

        /// <summary>
        /// Gets the number of bytes available to write to the buffer.
        /// </summary>
        public int AvailableWrite => Data.Length - Size;

        /// <summary>
        /// Releases the buffer back to the pool.
        /// </summary>
        public void Dispose()
        {
            BufferPool.Return(Data);
        }

        /// <summary>
        /// Tidies the buffer by moving unread data to the start.
        /// </summary>
        public void Tidy()
        {
            if (Size - Pos != 0)
                Array.Copy(Data, Pos, Data, 0, Size - Pos); //move data to start
            Size = Size - Pos;
            Pos = 0;
        }

        /// <summary>
        /// Reads up to <paramref name="length"/> bytes from the buffer and advances the position.
        /// </summary>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The number of bytes actually read.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is negative.</exception>
        public int Read(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
            return Read(null, 0, length);
        }

        /// <summary>
        /// Reads up to <paramref name="length"/> bytes from the buffer into the specified array and advances the position.
        /// </summary>
        /// <param name="data">The destination array, or null to skip copying.</param>
        /// <param name="offset">The offset in the destination array at which to begin writing.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The number of bytes actually read.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="length"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="data"/> is not null and the sum of <paramref name="offset"/> and <paramref name="length"/> is greater than the array length.</exception>
        public int Read(byte[]? data, int offset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
            if (data != null && (data.Length - offset < length))
                throw new ArgumentException("The sum of offset and length is greater than the array length.", nameof(data));

            int sz = Math.Min(Size - Pos, length);
            if (data != null)
                Array.Copy(Data, Pos, data, offset, sz);
            Pos += sz;
            if (Pos != 0 && Size - Pos <= TidyThreshold)
                Tidy();
            return sz;
        }

        /// <summary>
        /// Writes up to <paramref name="length"/> bytes to the buffer from the specified array and advances the size.
        /// </summary>
        /// <param name="length">The number of bytes to write.</param>
        /// <returns>The number of bytes actually written.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="length"/> is negative.</exception>
        public int Write(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
            return Write(null, 0, length);
        }

        /// <summary>
        /// Writes up to <paramref name="length"/> bytes to the buffer from the specified array and advances the size.
        /// </summary>
        /// <param name="data">The source array, or null to skip copying.</param>
        /// <param name="offset">The offset in the source array at which to begin reading.</param>
        /// <param name="length">The number of bytes to write.</param>
        /// <returns>The number of bytes actually written.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="length"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="data"/> is not null and the sum of <paramref name="offset"/> and <paramref name="length"/> is greater than the array length.</exception>
        public int Write(byte[]? data, int offset, int length)
        {
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
            if (data != null && (data.Length - offset < length))
                throw new ArgumentException("The sum of offset and length is greater than the array length.", nameof(data));

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
        /// <summary>
        /// Reads data from the buffer into the specified span and advances the position.
        /// </summary>
        /// <param name="data">The destination span.</param>
        /// <returns>The number of bytes actually read.</returns>
        public int Read(Span<byte> data)
        {
            int sz = Math.Min(Size - Pos, data.Length);

            // Copy data using Span.CopyTo
            Data.AsSpan(Pos, sz).CopyTo(data);
            Pos += sz;

            if (Pos != 0 && Size - Pos < TidyThreshold)
            {
                // Move data to start using Span.CopyTo
                Data.AsSpan(Pos, Size - Pos).CopyTo(Data.AsSpan());
                Size = Size - Pos;
                Pos = 0;
            }

            return sz;
        }

        /// <summary>
        /// Writes data from the specified span to the buffer and advances the size.
        /// </summary>
        /// <param name="data">The source span.</param>
        /// <returns>The number of bytes actually written.</returns>
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

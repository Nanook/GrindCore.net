using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore
{
#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)

    /// <summary>
    /// Represents a block of data for compression or decompression operations compatible with span and all versions of dotnet, using spans for efficient memory access.
    /// </summary>
    internal readonly ref struct DataBlock
    {
        private readonly Span<byte> _mutableData; // Represents the mutable span for writable memory

        /// <summary>
        /// Gets the readonly memory span for this data block.
        /// </summary>
        public readonly ReadOnlySpan<byte> Data;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBlock"/> struct from a <see cref="ReadOnlySpan{Byte}"/>.
        /// </summary>
        /// <param name="span">The source span.</param>
        /// <param name="offset">The offset within the span.</param>
        /// <param name="length">The length of the data block.</param>
        public DataBlock(ReadOnlySpan<byte> span, int offset, int length)
        {
            _mutableData = Span<byte>.Empty; // Initialize as empty since ReadOnlySpan is immutable
            Data = span;
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBlock"/> struct from a <see cref="ReadOnlySpan{Byte}"/> covering the entire span.
        /// </summary>
        /// <param name="span">The source span.</param>
        public DataBlock(ReadOnlySpan<byte> span) : this(span, 0, span.Length) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBlock"/> struct from a <see cref="Span{Byte}"/>.
        /// </summary>
        /// <param name="span">The source span.</param>
        /// <param name="offset">The offset within the span.</param>
        /// <param name="length">The length of the data block.</param>
        public DataBlock(Span<byte> span, int offset, int length)
        {
            _mutableData = span;
            Data = span;
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBlock"/> struct from a <see cref="Span{Byte}"/> covering the entire span.
        /// </summary>
        /// <param name="span">The source span.</param>
        public DataBlock(Span<byte> span) : this(span, 0, span.Length) { }

        /// <summary>
        /// Gets the offset within the underlying span or array.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Gets the length of the data block.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Exposes the mutable span for writing, if available.
        /// </summary>
        /// <returns>The writable <see cref="Span{Byte}"/>.</returns>
        public Span<byte> AsWritableSpan()
        {
            return _mutableData;
        }

        /// <summary>
        /// Reads data from this block into a target byte array.
        /// </summary>
        /// <param name="sourceOffset">The offset within this block to start reading from.</param>
        /// <param name="target">The target byte array.</param>
        /// <param name="targetOffset">The offset within the target array to start writing to.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the source or target range is out of bounds.</exception>
        public void Read(int sourceOffset, byte[] target, int targetOffset, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            Data.Slice(this.Offset + sourceOffset, length).CopyTo(target.AsSpan(targetOffset, length));
        }

        /// <summary>
        /// Reads data from this block and writes it to a <see cref="CompressionBuffer"/>.
        /// </summary>
        /// <param name="sourceOffset">The offset within this block to start reading from.</param>
        /// <param name="buffer">The target <see cref="CompressionBuffer"/>.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the source range is out of bounds.</exception>
        public void Read(int sourceOffset, CompressionBuffer buffer, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            buffer.Write(Data.Slice(this.Offset + sourceOffset, length));
        }

        /// <summary>
        /// Writes data from a <see cref="CompressionBuffer"/> into this block.
        /// </summary>
        /// <param name="sourceOffset">The offset within this block to start writing to.</param>
        /// <param name="buffer">The source <see cref="CompressionBuffer"/>.</param>
        /// <param name="length">The number of bytes to write.</param>
        /// <exception cref="NotSupportedException">Thrown if this block is not writable.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the source range is out of bounds.</exception>
        public void Write(int sourceOffset, CompressionBuffer buffer, int length)
        {
            if (_mutableData.IsEmpty)
                throw new NotSupportedException("ReadOnlySpan is not writable");
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            buffer.Read(_mutableData.Slice(this.Offset + sourceOffset, length));
        }

        /// <summary>
        /// Copies data from this block to another <see cref="DataBlock"/>.
        /// </summary>
        /// <param name="target">The target <see cref="DataBlock"/>.</param>
        /// <param name="sourceOffset">The offset within this block to start reading from.</param>
        /// <param name="targetOffset">The offset within the target block to start writing to.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any offset or length is negative, or if the source or target range is out of bounds.</exception>
        public void CopyTo(DataBlock target, int sourceOffset, int targetOffset, int length)
        {
            if (sourceOffset < 0 || targetOffset < 0 || length < 0)
                throw new ArgumentOutOfRangeException("Offsets and length must be non-negative.");

            if (sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset), "Target range is out of bounds.");

            Data.Slice(Offset + sourceOffset, length).CopyTo(target.AsWritableSpan().Slice(target.Offset + targetOffset, length));
        }
    }

#else

    /// <summary>
    /// Represents a block of data for compression or decompression operations, using arrays for compatibility.
    /// </summary>
    internal readonly struct DataBlock
    {
        /// <summary>
        /// Gets the underlying data array.
        /// </summary>
        public readonly byte[] Data;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBlock"/> struct as an empty block.
        /// </summary>
        public DataBlock() : this(new byte[0], 0, 0) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBlock"/> struct from a byte array, offset, and length.
        /// </summary>
        /// <param name="data">The source array.</param>
        /// <param name="offset">The offset within the array.</param>
        /// <param name="length">The length of the data block.</param>
        public DataBlock(byte[] data, int offset, int length)
        {
            Data = data;
            Offset = offset;
            Length = length;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataBlock"/> struct from a byte array covering the entire array.
        /// </summary>
        /// <param name="data">The source array.</param>
        public DataBlock(byte[] data) : this(data, 0, data.Length) { }

        /// <summary>
        /// Gets the offset within the underlying array.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// Gets the length of the data block.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Reads data from this block into a target byte array.
        /// </summary>
        /// <param name="sourceOffset">The offset within this block to start reading from.</param>
        /// <param name="target">The target byte array.</param>
        /// <param name="targetOffset">The offset within the target array to start writing to.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the source or target range is out of bounds.</exception>
        public void Read(int sourceOffset, byte[] target, int targetOffset, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            Array.Copy(Data, Offset + sourceOffset, target, targetOffset, length);
        }

        /// <summary>
        /// Reads data from this block and writes it to a <see cref="CompressionBuffer"/>.
        /// </summary>
        /// <param name="sourceOffset">The offset within this block to start reading from.</param>
        /// <param name="buffer">The target <see cref="CompressionBuffer"/>.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the source range is out of bounds.</exception>
        public void Read(int sourceOffset, CompressionBuffer buffer, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            buffer.Write(Data, Offset + sourceOffset, length);
        }

        /// <summary>
        /// Writes data from a <see cref="CompressionBuffer"/> into this block.
        /// </summary>
        /// <param name="sourceOffset">The offset within this block to start writing to.</param>
        /// <param name="buffer">The source <see cref="CompressionBuffer"/>.</param>
        /// <param name="length">The number of bytes to write.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the source range is out of bounds.</exception>
        public void Write(int sourceOffset, CompressionBuffer buffer, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            buffer.Read(Data, Offset + sourceOffset, length);
        }

        /// <summary>
        /// Copies data from this block to another <see cref="DataBlock"/>.
        /// </summary>
        /// <param name="target">The target <see cref="DataBlock"/>.</param>
        /// <param name="sourceOffset">The offset within this block to start reading from.</param>
        /// <param name="targetOffset">The offset within the target block to start writing to.</param>
        /// <param name="length">The number of bytes to copy.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if any offset or length is negative, or if the source or target range is out of bounds.</exception>
        public void CopyTo(DataBlock target, int sourceOffset, int targetOffset, int length)
        {
            if (sourceOffset < 0 || targetOffset < 0 || length < 0)
                throw new ArgumentOutOfRangeException("Offsets and length must be non-negative.");

            if (sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            if (targetOffset + length > target.Length)
                throw new ArgumentOutOfRangeException(nameof(targetOffset), "Target range is out of bounds.");

            Array.Copy(Data, Offset + sourceOffset, target.Data, target.Offset + targetOffset, length);
        }
    }
#endif
}

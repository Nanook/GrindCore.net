using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore
{
#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)

    internal readonly ref struct DataBlock
    {
        private readonly Span<byte> _mutableData; // Represents the mutable span for writable memory
        public readonly ReadOnlySpan<byte> Data; // Represents the readonly memory span

        // Constructor for ReadOnlySpan<byte>
        public DataBlock(ReadOnlySpan<byte> span, int offset, int length)
        {
            _mutableData = Span<byte>.Empty; // Initialize as empty since ReadOnlySpan is immutable
            Data = span;
            Offset = offset;
            Length = length;
        }

        public DataBlock(ReadOnlySpan<byte> span) : this(span, 0, span.Length) { }

        // Constructor for Span<byte>
        public DataBlock(Span<byte> span, int offset, int length)
        {
            _mutableData = span;
            Data = span;
            Offset = offset;
            Length = length;
        }

        public DataBlock(Span<byte> span) : this(span, 0, span.Length) { }

        public int Offset { get; }
        public int Length { get; }

        // Expose the mutable span for writing
        public Span<byte> AsWritableSpan()
        {
            return _mutableData;
        }

        // Read method to replicate the logic and copy data to the target byte array
        public void Read(int sourceOffset, byte[] target, int targetOffset, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            Data.Slice(this.Offset + sourceOffset, length).CopyTo(target.AsSpan(targetOffset, length));
        }

        public void Read(int sourceOffset, CompressionBuffer buffer, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            buffer.Write(Data.Slice(this.Offset + sourceOffset, length));
        }

        public void Write(int sourceOffset, CompressionBuffer buffer, int length)
        {
            if (_mutableData.IsEmpty)
                throw new NotSupportedException("ReadOnlySpan is not writable");
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            buffer.Read(_mutableData.Slice(this.Offset + sourceOffset, length));
        }

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

    internal readonly struct DataBlock
    {
        public readonly byte[] Data;

        public DataBlock() : this(new byte[0], 0, 0) { }

        public DataBlock(byte[] data, int offset, int length)
        {
            Data = data;
            Offset = offset;
            Length = length;
        }

        public DataBlock(byte[] data) : this(data, 0, data.Length) { }

        public int Offset { get; }
        public int Length { get; }

        // Read method to replicate the logic and copy data to the target byte array
        public void Read(int sourceOffset, byte[] target, int targetOffset, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            Array.Copy(Data, Offset + sourceOffset, target, targetOffset, length);
        }

        public void Read(int sourceOffset, CompressionBuffer buffer, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            buffer.Write(Data, Offset + sourceOffset, length);
        }

        public void Write(int sourceOffset, CompressionBuffer buffer, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            buffer.Read(Data, Offset + sourceOffset, length);
        }

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
using System;


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
            Data = span.Slice(offset, length);
            Offset = offset;
            Length = length;
        }

        // Constructor for Span<byte>
        public DataBlock(Span<byte> span, int offset, int length)
        {
            _mutableData = span;
            Data = span.Slice(offset, length);
            Offset = offset;
            Length = length;
        }

        public int Offset { get; }
        public int Length { get; }

        // Expose the mutable span for writing
        public Span<byte> AsWritableSpan()
        {
            return _mutableData.Slice(Offset, Length);
        }

        // Copy method to replicate the logic and copy data to the target byte array
        public void Copy(int sourceOffset, byte[] target, int targetOffset, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            Data.Slice(sourceOffset, length).CopyTo(target.AsSpan(targetOffset, length));
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

        public int Offset { get; }
        public int Length { get; }

        // Copy method to replicate the logic and copy data to the target byte array
        public void Copy(int sourceOffset, byte[] target, int targetOffset, int length)
        {
            if (sourceOffset < 0 || length < 0 || sourceOffset + length > Length)
                throw new ArgumentOutOfRangeException(nameof(sourceOffset), "Source range is out of bounds.");

            Array.Copy(Data, Offset + sourceOffset, target, targetOffset, length);
        }
    }
#endif
}
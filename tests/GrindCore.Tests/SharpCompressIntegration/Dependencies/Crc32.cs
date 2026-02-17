#nullable disable

using System;

namespace SharpCompress.Compressors.Xz;

[CLSCompliant(false)]
public static class Crc32
{
    public const uint DefaultPolynomial = 0xedb88320u;
    public const uint DefaultSeed = 0xffffffffu;

    private static uint[] _DefaultTable;

    public static uint Compute(byte[] buffer) => Compute(DefaultSeed, buffer);

    public static uint Compute(uint seed, byte[] buffer) =>
        Compute(DefaultPolynomial, seed, buffer);

    public static uint Compute(uint polynomial, uint seed, byte[] buffer) =>
        ~calculateHash(initializeTable(polynomial), seed, buffer);

    private static uint[] initializeTable(uint polynomial)
    {
        if (polynomial == DefaultPolynomial && _DefaultTable != null)
        {
            return _DefaultTable;
        }

        var createTable = new uint[256];
        for (var i = 0; i < 256; i++)
        {
            var entry = (uint)i;
            for (var j = 0; j < 8; j++)
            {
                if ((entry & 1) == 1)
                {
                    entry = (entry >> 1) ^ polynomial;
                }
                else
                {
                    entry >>= 1;
                }
            }

            createTable[i] = entry;
        }

        if (polynomial == DefaultPolynomial)
        {
            _DefaultTable = createTable;
        }

        return createTable;
    }

    private static uint calculateHash(uint[] table, uint seed, ReadOnlySpan<byte> buffer)
    {
        var crc = seed;
        var len = buffer.Length;
        for (var i = 0; i < len; i++)
        {
            crc = (crc >> 8) ^ table[(buffer[i] ^ crc) & 0xff];
        }

        return crc;
    }
}

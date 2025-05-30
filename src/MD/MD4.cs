using Nanook.GrindCore;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

/// <summary>
/// Provides implementation of the MD4 hashing algorithm.
/// </summary>
public unsafe class MD4 : HashAlgorithm
{
    private const int _hashSizeBytes = 16;
    private Interop.MD4_CTX _ctx;
    private const int BufferSize = 256 * 1024 * 1024; // 256 MiB buffer

    /// <summary>
    /// Initializes a new instance of the MD4 class.
    /// </summary>
    public MD4()
    {
        // Set the hash size value to 128 bits (16 bytes) for MD4
        HashSizeValue = _hashSizeBytes << 3;
        Initialize();
    }

    /// <summary>
    /// Computes the hash value for the specified byte array.
    /// </summary>
    /// <param name="data">The input data to compute the hash code for.</param>
    /// <returns>The computed hash code.</returns>
    public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

    /// <summary>
    /// Computes the hash value for the specified region of the byte array.
    /// </summary>
    /// <param name="data">The input data to compute the hash code for.</param>
    /// <param name="offset">The offset in the byte array to start at.</param>
    /// <param name="length">The number of bytes to process.</param>
    /// <returns>The computed hash code.</returns>
    public static byte[] Compute(byte[] data, int offset, int length)
    {
        Interop.MD4_CTX ctx = new Interop.MD4_CTX();
        byte[] result = new byte[_hashSizeBytes]; // MD4_DIGEST_LENGTH is 16

        // Pin the data array and result in memory to obtain pointers
        fixed (byte* dataPtr = data)
        fixed (byte* resultPtr = result)
        {
            Interop.MD.SZ_MD4_Init(&ctx);
            // Process the data in 256 MiB chunks
            processData(dataPtr, offset, length, &ctx);
            // Finalize the hash
            Interop.MD.SZ_MD4_Final(resultPtr, &ctx);
        }

        return result;
    }

    /// <summary>
    /// Processes the specified region of the byte array in 256 MiB chunks.
    /// </summary>
    private static void processData(byte* dataPtr, int offset, int length, Interop.MD4_CTX* ctx)
    {
        int remainingSize = length;
        while (remainingSize > 0)
        {
            // Determine the size of the current chunk to process
            int bytesRead = Math.Min(remainingSize, BufferSize);
            // Update the hash context with the current chunk
            Interop.MD.SZ_MD4_Update(ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
            // Decrease the remaining size by the number of bytes read
            remainingSize -= bytesRead;
        }
    }

    /// <summary>
    /// Creates a new instance of the MD4 class.
    /// </summary>
    /// <returns>A new instance of the MD4 class.</returns>
    public static new MD4 Create() => new MD4();

    /// <summary>
    /// Initializes the hash algorithm.
    /// </summary>
    public override void Initialize()
    {
        _ctx = new Interop.MD4_CTX();
        // Initialize the hash context
        fixed (Interop.MD4_CTX* ctxPtr = &_ctx)
            Interop.MD.SZ_MD4_Init(ctxPtr);
    }

    /// <summary>
    /// Routes data written to the object into the hash algorithm for computing the hash.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="offset">The offset in the byte array to start at.</param>
    /// <param name="size">The number of bytes to process.</param>
    protected override void HashCore(byte[] data, int offset, int size)
    {
        // Pin the data array and the hash context in memory to obtain pointers
        fixed (byte* dataPtr = data)
        fixed (Interop.MD4_CTX* ctxPtr = &_ctx)
            processData(dataPtr, offset, size, ctxPtr);
    }

    /// <summary>
    /// Finalizes the hash computation after the last data is written to the object.
    /// </summary>
    /// <returns>The computed hash code.</returns>
    protected override byte[] HashFinal()
    {
        byte[] result = new byte[_hashSizeBytes]; // MD4_DIGEST_LENGTH is 16
        // Pin the result array in memory to obtain a pointer
        fixed (byte* resultPtr = result)
        fixed (Interop.MD4_CTX* ctxPtr = &_ctx)
            Interop.MD.SZ_MD4_Final(resultPtr, ctxPtr);
        return result;
    }
}

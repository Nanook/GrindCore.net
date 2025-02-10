using Nanook.GrindCore;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

/// <summary>
/// Provides implementation of the MD2 hashing algorithm.
/// </summary>
public unsafe class MD2 : HashAlgorithm
{
    private const int _hashSizeBytes = 16;
    private Interop.MD2_CTX _ctx;
    private HashBuffer _buffer;
    private const int BufferSize = 256 * 1024 * 1024; // 256 MiB buffer

    /// <summary>
    /// Initializes a new instance of the MD2 class.
    /// </summary>
    public MD2()
    {
        // Set the hash size value to 128 bits (16 bytes) for MD2
        HashSizeValue = _hashSizeBytes << 3;
        _buffer = new HashBuffer(_hashSizeBytes);
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
        Interop.MD2_CTX ctx = new Interop.MD2_CTX();
        HashBuffer buffer = new HashBuffer(_hashSizeBytes);
        byte[] result = new byte[_hashSizeBytes]; // MD2_DIGEST_LENGTH is 16

        // Pin the data array and result in memory to obtain pointers
        fixed (byte* resultPtr = result)
        {
            Interop.MD.SZ_MD2_Init(&ctx);
            Interop.MD2_CTX* ctxPtr = &ctx;
            // Process the data in 256 MiB chunks
            processData(data, offset, length, ctxPtr, buffer);
            // Complete the buffer and finalize the hash
            buffer.Complete((d, o, s) => bufferPadProcess(ctxPtr, d, o, s));
            Interop.MD.SZ_MD2_Final(resultPtr, &ctx);
        }

        return result;
    }

    /// <summary>
    /// Processes the specified region of the byte array in 256 MiB chunks.
    /// </summary>
    private static void processData(byte[] data, int offset, int length, Interop.MD2_CTX* ctx, HashBuffer buffer)
    {
        int remainingSize = length;
        while (remainingSize > 0)
        {
            // Determine the size of the current chunk to process
            int bytesRead = Math.Min(remainingSize, BufferSize);
            // Process the buffer with the current chunk
            buffer.Process(data, offset, bytesRead, (d, o, s) => bufferProcess(ctx, d, o, s));
            // Decrease the remaining size by the number of bytes read
            remainingSize -= bytesRead;
            // Increment the offset by the number of bytes read
            offset += bytesRead;
        }
    }

    /// <summary>
    /// Creates a new instance of the MD2 class.
    /// </summary>
    /// <returns>A new instance of the MD2 class.</returns>
    public static new MD2 Create() => new MD2();

    /// <summary>
    /// Initializes the hash algorithm.
    /// </summary>
    public override void Initialize()
    {
        _ctx = new Interop.MD2_CTX();
        // Initialize the hash context
        fixed (Interop.MD2_CTX* ctxPtr = &_ctx)
            Interop.MD.SZ_MD2_Init(ctxPtr);
    }

    /// <summary>
    /// Processes the buffer with padding.
    /// </summary>
    private static void bufferPadProcess(Interop.MD2_CTX* ctx, byte[] data, int offset, int size)
    {
        byte paddingValue = (byte)(data.Length - size);

        // Pad the buffer with the padding value
        for (int i = size; i < data.Length; i++)
            data[i] = paddingValue;

        bufferProcess(ctx, data, offset, size);
    }

    /// <summary>
    /// Processes the specified region of the byte array.
    /// </summary>
    private static void bufferProcess(Interop.MD2_CTX* ctx, byte[] data, int offset, int size)
    {
        int remainingSize = size;
        // Pin the data array in memory to obtain a pointer
        fixed (byte* dataPtr = data)
        {
            while (remainingSize > 0)
            {
                // Determine the size of the current chunk to process
                int bytesRead = Math.Min(remainingSize, BufferSize);
                // Update the hash context with the current chunk
                Interop.MD.SZ_MD2_Update(ctx, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                // Decrease the remaining size by the number of bytes read
                remainingSize -= bytesRead;
            }
        }
    }

    /// <summary>
    /// Routes data written to the object into the hash algorithm for computing the hash.
    /// </summary>
    /// <param name="data">The input data.</param>
    /// <param name="offset">The offset in the byte array to start at.</param>
    /// <param name="size">The number of bytes to process.</param>
    protected override void HashCore(byte[] data, int offset, int size)
    {
        _buffer.Process(data, offset, size, (d, o, s) =>
        {
            // Pin the hash context in memory to obtain a pointer
            fixed (Interop.MD2_CTX* ctxPtr = &_ctx)
                bufferProcess(ctxPtr, d, o, s);
        });
    }

    /// <summary>
    /// Finalizes the hash computation after the last data is written to the object.
    /// </summary>
    /// <returns>The computed hash code.</returns>
    protected override byte[] HashFinal()
    {
        // Complete the buffer and finalize the hash
        _buffer.Complete((d, o, s) =>
        {
            // Pin the hash context in memory to obtain a pointer
            fixed (Interop.MD2_CTX* ctxPtr = &_ctx)
                bufferPadProcess(ctxPtr, d, o, s);
        });

        byte[] result = new byte[_hashSizeBytes]; // MD2_DIGEST_LENGTH is 16
        // Pin the result array in memory to obtain a pointer
        fixed (byte* resultPtr = result)
        fixed (Interop.MD2_CTX* ctxPtr = &_ctx)
            Interop.MD.SZ_MD2_Final(resultPtr, ctxPtr);
        return result;
    }
}

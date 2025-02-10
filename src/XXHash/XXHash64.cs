using Nanook.GrindCore.XXHash;
using System;
using System.IO;
using System.Security.Cryptography;

/// <summary>
/// Provides implementation of the XXHash64 hashing algorithm.
/// </summary>
namespace Nanook.GrindCore.XXHash
{
    /// <summary>
    /// Represents the XXHash64 hashing algorithm.
    /// </summary>
    public unsafe class XXHash64 : HashAlgorithm
    {
        private XXH64_CTX _ctx;
        private const int BufferSize = 256 * 1024 * 1024; // 256 MiB buffer

        /// <summary>
        /// Initializes a new instance of the XXHash64 class.
        /// </summary>
        public XXHash64()
        {
            // Set the hash size value to 64 bits (8 bytes) for XXH64
            HashSizeValue = 64;
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
            XXH64_CTX ctx = new XXH64_CTX();
            // Pin the data array in memory to obtain a pointer
            fixed (byte* dataPtr = data)
            {
                XXHash.SZ_XXH64_Reset(&ctx);
                // Process the data in 256 MiB chunks
                processData(dataPtr, offset, length, &ctx);
                // Compute and return the final hash
                return XXHash.SZ_XXH64_Digest(&ctx).ToByteArray();
            }
        }

        /// <summary>
        /// Processes the specified region of the byte array in 256 MiB chunks.
        /// </summary>
        private static void processData(byte* dataPtr, int offset, int length, XXH64_CTX* ctx)
        {
            int remainingSize = length;
            while (remainingSize > 0)
            {
                // Determine the size of the current chunk to process
                int bytesRead = Math.Min(remainingSize, BufferSize);
                // Update the hash context with the current chunk
                XXHash.SZ_XXH64_Update(ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                // Decrease the remaining size by the number of bytes read
                remainingSize -= bytesRead;
            }
        }

        /// <summary>
        /// Creates a new instance of the XXHash64 class.
        /// </summary>
        /// <returns>A new instance of the XXHash64 class.</returns>
        public static new XXHash64 Create() => new XXHash64();

        /// <summary>
        /// Initializes the hash algorithm.
        /// </summary>
        public override void Initialize()
        {
            _ctx = new XXH64_CTX();
            // Reset the hash context
            fixed (XXH64_CTX* ctxPtr = &_ctx)
                XXHash.SZ_XXH64_Reset(ctxPtr);
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
            fixed (XXH64_CTX* ctxPtr = &_ctx)
                processData(dataPtr, offset, size, ctxPtr);
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is written to the object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal()
        {
            // Compute and return the final hash
            fixed (XXH64_CTX* ctxPtr = &_ctx)
                return XXHash.SZ_XXH64_Digest(ctxPtr).ToByteArray();
        }

    }
}

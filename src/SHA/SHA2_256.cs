using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

/// <summary>
/// Provides implementation of the SHA2_256 hashing algorithm.
/// </summary>
namespace Nanook.GrindCore.SHA
{
    /// <summary>
    /// Represents the SHA2_256 hashing algorithm.
    /// </summary>
    public unsafe class SHA2_256 : HashAlgorithm
    {
        private const int _hashSizeBytes = 32;
        private Interop.CSha256 _ctx;
        private const int BufferSize = 256 * 1024 * 1024; // 256 MiB _outBuffer

        /// <summary>
        /// Initializes a new instance of the SHA2_256 class.
        /// </summary>
        public SHA2_256()
        {
            // Set the hash size value to 256 bits (32 bytes) for SHA2_256
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
            Interop.CSha256 ctx = new Interop.CSha256();
            byte[] result = new byte[_hashSizeBytes]; // SHA256_DIGEST_LENGTH is 32 bytes

            // Pin the data array and result in memory to obtain pointers
            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.SHA.SZ_Sha256_Init(&ctx);
                Interop.SHA.SZ_Sha256_SetFunction(&ctx, 0); // Optionally, set a specific function
                // Process the data in 256 MiB chunks
                processData(dataPtr, offset, length, &ctx);
                // Finalize the hash
                Interop.SHA.SZ_Sha256_Final(&ctx, resultPtr);
            }

            return result;
        }

        /// <summary>
        /// Processes the specified region of the byte array in 256 MiB chunks.
        /// </summary>
        private static void processData(byte* dataPtr, int offset, int length, Interop.CSha256* ctx)
        {
            int remainingSize = length;
            while (remainingSize > 0)
            {
                // Determine the size of the current chunk to process
                int bytesRead = Math.Min(remainingSize, BufferSize);
                // Update the hash context with the current chunk
                Interop.SHA.SZ_Sha256_Update(ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                // Decrease the remaining size by the number of bytes read
                remainingSize -= bytesRead;
            }
        }

        /// <summary>
        /// Creates a new instance of the SHA2_256 class.
        /// </summary>
        /// <returns>A new instance of the SHA2_256 class.</returns>
        public static new SHA2_256 Create() => new SHA2_256();

        /// <summary>
        /// Initializes the hash algorithm.
        /// </summary>
        public override void Initialize()
        {
            _ctx = new Interop.CSha256();
            // Initialize the hash context
            fixed (Interop.CSha256* ctxPtr = &_ctx)
            {
                Interop.SHA.SZ_Sha256_Init(ctxPtr);
                // Optionally, set a specific function
                Interop.SHA.SZ_Sha256_SetFunction(ctxPtr, 0);
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
            // Pin the data array and the hash context in memory to obtain pointers
            fixed (byte* dataPtr = data)
            fixed (Interop.CSha256* ctxPtr = &_ctx)
                processData(dataPtr, offset, size, ctxPtr);
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is written to the object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // SHA256_DIGEST_LENGTH is 32 bytes
            // Pin the result array in memory to obtain a pointer
            fixed (byte* resultPtr = result)
            fixed (Interop.CSha256* ctxPtr = &_ctx)
                Interop.SHA.SZ_Sha256_Final(ctxPtr, resultPtr);
            return result;
        }
    }
}

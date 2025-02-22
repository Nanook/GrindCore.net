using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

/// <summary>
/// Provides implementation of the Blake3 hashing algorithm.
/// </summary>
namespace Nanook.GrindCore.Blake
{
    /// <summary>
    /// Represents the Blake3 hashing algorithm.
    /// </summary>
    public unsafe class Blake3 : HashAlgorithm
    {
        private const int _hashSizeBytes = 32;
        private Interop.Blake3Hasher _hasher;
        private const int BufferSize = 256 * 1024 * 1024; // 256 MiB buffer

        /// <summary>
        /// Initializes a new instance of the Blake3 class.
        /// </summary>
        public Blake3()
        {
            // Set the hash size value to 256 bits (32 bytes) for Blake3
            HashSizeValue = 256;
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
            Interop.Blake3Hasher hasher = new Interop.Blake3Hasher();
            // Initialize hasher
            Interop.Blake.SZ_blake3_hasher_init(&hasher);
            // Pin the data array and update hasher in chunks
            fixed (byte* dataPtr = data)
                processData(dataPtr, offset, length, &hasher);
            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Adjust size according to your needs
            fixed (byte* resultPtr = result)
                Interop.Blake.SZ_blake3_hasher_finalize(&hasher, resultPtr, (UIntPtr)result.Length);
            return result;
        }

        /// <summary>
        /// Processes the specified region of the byte array in 256 MiB chunks.
        /// </summary>
        private static void processData(byte* dataPtr, int offset, int length, Interop.Blake3Hasher* hasher)
        {
            int remainingSize = length;
            while (remainingSize > 0)
            {
                // Determine the size of the current chunk to process
                int bytesRead = Math.Min(remainingSize, BufferSize);
                // Update the hash context with the current chunk
                Interop.Blake.SZ_blake3_hasher_update(hasher, (IntPtr)(dataPtr + offset + (length - remainingSize)), (UIntPtr)bytesRead);
                // Decrease the remaining size by the number of bytes read
                remainingSize -= bytesRead;
            }
        }

        /// <summary>
        /// Creates a new instance of the Blake3 class.
        /// </summary>
        /// <returns>A new instance of the Blake3 class.</returns>
        public static new Blake3 Create() => new Blake3();

        /// <summary>
        /// Initializes the hash algorithm.
        /// </summary>
        public override void Initialize()
        {
            _hasher = new Interop.Blake3Hasher();
            // Initialize the hasher
            fixed (Interop.Blake3Hasher* hasherPtr = &_hasher)
                Interop.Blake.SZ_blake3_hasher_init(hasherPtr);
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
            fixed (Interop.Blake3Hasher* hasherPtr = &_hasher)
                processData(dataPtr, offset, size, hasherPtr);
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is written to the object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal()
        {
            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Adjust size according to your needs
            fixed (byte* resultPtr = result)
            fixed (Interop.Blake3Hasher* hasherPtr = &_hasher)
                Interop.Blake.SZ_blake3_hasher_finalize(hasherPtr, resultPtr, (UIntPtr)result.Length);
            return result;
        }
    }
}

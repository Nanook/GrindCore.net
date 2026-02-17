using Nanook.GrindCore.XXHash;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Nanook.GrindCore.XXHash
{
    /// <summary>
    /// Represents the XXHash64 hashing algorithm.
    /// </summary>
    public unsafe class XXHash64 : HashAlgorithmGC
    {
        private XXH64_CTX _ctx;
        private const int _BufferSize = 256 * 1024 * 1024; // 256 MiB _outBuffer
        private ulong _finalHash;

        /// <summary>
        /// Initializes a new instance of the <see cref="XXHash64"/> class.
        /// </summary>
        public XXHash64()
        {
            // Set the hash size value to 64 bits (8 bytes) for XXH64
            HashSizeValue = 64;
            Initialize();
        }

        /// <summary>
        /// Computes the XXHash64 value for the specified byte array and returns it as a ulong.
        /// </summary>
        /// <param name="data">The input data to compute the hash code for.</param>
        /// <returns>The computed hash code as a ulong.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
        [CLSCompliant(false)]
        public static ulong Compute(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return Compute(data, 0, data.Length);
        }

        /// <summary>
        /// Computes the XXHash64 value for the specified region of the byte array and returns it as a ulong.
        /// </summary>
        /// <param name="data">The input data to compute the hash code for.</param>
        /// <param name="offset">The offset in the byte array to start at.</param>
        /// <param name="length">The number of bytes to process.</param>
        /// <returns>The computed hash code as a ulong.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="length"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of <paramref name="offset"/> and <paramref name="length"/> is greater than the buffer length.</exception>
        [CLSCompliant(false)]
        public static ulong Compute(byte[] data, int offset, int length)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
            if (data.Length - offset < length)
                throw new ArgumentException("The sum of offset and length is greater than the buffer length.");

            XXH64_CTX ctx = new XXH64_CTX();
            fixed (byte* dataPtr = data)
            {
                XXHash.SZ_XXH64_Reset(&ctx);
                processData(dataPtr, offset, length, &ctx);
                return XXHash.SZ_XXH64_Digest(&ctx);
            }
        }

        /// <summary>
        /// Computes the XXHash64 value for the specified byte array and returns it as a byte array (big-endian).
        /// </summary>
        public static byte[] ComputeBytes(byte[] data)
        {
            return ComputeBytes(data, 0, data.Length);
        }

        /// <summary>
        /// Computes the XXHash64 value for the specified region of the byte array and returns it as a byte array (big-endian).
        /// </summary>
        public static byte[] ComputeBytes(byte[] data, int offset, int length)
        {
            ulong val = Compute(data, offset, length);
            if (BitConverter.IsLittleEndian)
            {
                val = ((val & 0x00000000000000FFUL) << 56) |
                      ((val & 0x000000000000FF00UL) << 40) |
                      ((val & 0x0000000000FF0000UL) << 24) |
                      ((val & 0x00000000FF000000UL) << 8) |
                      ((val & 0x000000FF00000000UL) >> 8) |
                      ((val & 0x0000FF0000000000UL) >> 24) |
                      ((val & 0x00FF000000000000UL) >> 40) |
                      ((val & 0xFF00000000000000UL) >> 56);
            }
            return BitConverter.GetBytes(val);
        }

        /// <summary>
        /// Processes the specified region of the byte array in 256 MiB chunks.
        /// </summary>
        /// <param name="dataPtr">Pointer to the input data.</param>
        /// <param name="offset">The offset in the data to start at.</param>
        /// <param name="length">The number of bytes to process.</param>
        /// <param name="ctx">Pointer to the hash context.</param>
        private static void processData(byte* dataPtr, int offset, int length, XXH64_CTX* ctx)
        {
            int remainingSize = length;
            while (remainingSize > 0)
            {
                // Determine the size of the current chunk to process
                int bytesRead = Math.Min(remainingSize, _BufferSize);
                // Update the hash context with the current chunk
                XXHash.SZ_XXH64_Update(ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                // Decrease the remaining size by the number of bytes read
                remainingSize -= bytesRead;
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="XXHash64"/> class.
        /// </summary>
        /// <returns>A new instance of the <see cref="XXHash64"/> class.</returns>
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
            {
                _finalHash = XXHash.SZ_XXH64_Digest(ctxPtr);
                return _finalHash.ToByteArray();
            }
        }

        /// <summary>
        /// Returns the finalized hash value as the specified type.
        /// For <see cref="ulong"/>, this method returns the cached <c>_finalHash</c> value directly, avoiding unnecessary byte conversions.
        /// For all other types (such as <see cref="byte"/>), the base implementation is used.
        /// <para>
        /// Throws <see cref="InvalidOperationException"/> if the hash has not been finalized.
        /// </para>
        /// </summary>
        /// <typeparam name="T">
        /// The type to return the hash as. Supported: <see cref="ulong"/>, <see cref="byte"/> (via base).
        /// </typeparam>
        /// <returns>The hash value as the specified type.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the hash has not been finalized.</exception>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="T"/> is not supported.</exception>
        public override T HashAsType<T>()
        {
            // Ensure the hash is finalized
            if (State != 0)
                throw new InvalidOperationException("Hash not finalized.");

            // Use the cached _finalHash for numeric types
            if (typeof(T) == typeof(ulong))
                return (T)(object)(ulong)_finalHash;

            // Fallback to base for other types (e.g., byte[])
            return base.HashAsType<T>();
        }
    }
}



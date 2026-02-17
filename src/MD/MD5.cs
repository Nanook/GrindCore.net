using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.MD
{
    /// <summary>
    /// Represents the MD5 hashing algorithm.
    /// </summary>
    public unsafe class MD5 : HashAlgorithmGC
    {
        private const int _HashSizeBytes = 16;
        private Interop.MD5_CTX _ctx;
        private const int _BufferSize = 256 * 1024 * 1024; // 256 MiB _outBuffer

        /// <summary>
        /// Initializes a new instance of the <see cref="MD5"/> class.
        /// </summary>
        public MD5()
        {
            // Set the hash size value to 128 bits (16 bytes) for MD5
            HashSizeValue = _HashSizeBytes << 3;
            Initialize();
        }

        /// <summary>
        /// Computes the hash value for the specified byte array.
        /// </summary>
        /// <param name="data">The input data to compute the hash code for.</param>
        /// <returns>The computed hash code.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
        public static byte[] Compute(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return Compute(data, 0, data.Length);
        }

        /// <summary>
        /// Computes the hash value for the specified region of the byte array.
        /// </summary>
        /// <param name="data">The input data to compute the hash code for.</param>
        /// <param name="offset">The offset in the byte array to start at.</param>
        /// <param name="length">The number of bytes to process.</param>
        /// <returns>The computed hash code.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="length"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of <paramref name="offset"/> and <paramref name="length"/> is greater than the buffer length.</exception>
        public static byte[] Compute(byte[] data, int offset, int length)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
            if (data.Length - offset < length)
                throw new ArgumentException("The sum of offset and length is greater than the buffer length.");

            Interop.MD5_CTX ctx = new Interop.MD5_CTX();
            byte[] result = new byte[_HashSizeBytes]; // MD5_DIGEST_LENGTH is 16

            // Pin the data array and result in memory to obtain pointers
            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.MD.SZ_MD5_Init(&ctx);
                // Process the data in 256 MiB chunks
                processData(dataPtr, offset, length, &ctx);
                // Finalize the hash
                Interop.MD.SZ_MD5_Final(resultPtr, &ctx);
            }

            return result;
        }

        /// <summary>
        /// Processes the specified region of the byte array in 256 MiB chunks.
        /// </summary>
        /// <param name="dataPtr">Pointer to the input data.</param>
        /// <param name="offset">The offset in the data to start at.</param>
        /// <param name="length">The number of bytes to process.</param>
        /// <param name="ctx">Pointer to the hash context.</param>
        private static void processData(byte* dataPtr, int offset, int length, Interop.MD5_CTX* ctx)
        {
            int remainingSize = length;
            while (remainingSize > 0)
            {
                // Determine the size of the current chunk to process
                int bytesRead = Math.Min(remainingSize, _BufferSize);
                // Update the hash context with the current chunk
                Interop.MD.SZ_MD5_Update(ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                // Decrease the remaining size by the number of bytes read
                remainingSize -= bytesRead;
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MD5"/> class.
        /// </summary>
        /// <returns>A new instance of the <see cref="MD5"/> class.</returns>
        public static new MD5 Create() => new MD5();

        /// <summary>
        /// Initializes the hash algorithm.
        /// </summary>
        public override void Initialize()
        {
            _ctx = new Interop.MD5_CTX();
            // Initialize the hash context
            fixed (Interop.MD5_CTX* ctxPtr = &_ctx)
                Interop.MD.SZ_MD5_Init(ctxPtr);
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
            fixed (Interop.MD5_CTX* ctxPtr = &_ctx)
                processData(dataPtr, offset, size, ctxPtr);
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is written to the object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_HashSizeBytes]; // MD5_DIGEST_LENGTH is 16
            // Pin the result array in memory to obtain a pointer
            fixed (byte* resultPtr = result)
            fixed (Interop.MD5_CTX* ctxPtr = &_ctx)
                Interop.MD.SZ_MD5_Final(resultPtr, ctxPtr);
            return result;
        }
    }
}


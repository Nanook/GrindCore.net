using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    /// <summary>
    /// Represents the SHA3 hashing algorithm.
    /// </summary>
    public unsafe class SHA3 : HashAlgorithmGC
    {
        private int _hashSizeBytes;
        private Interop.SHA3_CTX _ctx;
        private const int BufferSize = 256 * 1024 * 1024; // 256 MiB _outBuffer

        /// <summary>
        /// Initializes a new instance of the <see cref="SHA3"/> class with the specified bit size.
        /// </summary>
        /// <param name="bitSize">The bit size of the SHA3 algorithm (224, 256, 384, 512).</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="bitSize"/> is not 224, 256, 384, or 512.</exception>
        public SHA3(int bitSize)
        {
            if (!(new int[] { 224, 256, 384, 512 }).Contains(bitSize))
                throw new ArgumentException("Unsupported bit size", nameof(bitSize));

            HashSizeValue = bitSize; // SHA3 bit size (224, 256, 384, 512)
            _hashSizeBytes = bitSize >> 3;
            Initialize();
        }

        /// <summary>
        /// Computes the hash value for the specified byte array with the specified bit size.
        /// </summary>
        /// <param name="data">The input data to compute the hash code for.</param>
        /// <param name="bitSize">The bit size of the SHA3 algorithm (224, 256, 384, 512).</param>
        /// <returns>The computed hash code.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="bitSize"/> is not 224, 256, 384, or 512.</exception>
        public static byte[] Compute(byte[] data, int bitSize)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return Compute(data, 0, data.Length, bitSize);
        }

        /// <summary>
        /// Computes the hash value for the specified region of the byte array with the specified bit size.
        /// </summary>
        /// <param name="data">The input data to compute the hash code for.</param>
        /// <param name="offset">The offset in the byte array to start at.</param>
        /// <param name="length">The number of bytes to process.</param>
        /// <param name="bitSize">The bit size of the SHA3 algorithm (224, 256, 384, 512).</param>
        /// <returns>The computed hash code.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="length"/> is negative.</exception>
        /// <exception cref="ArgumentException">Thrown if the sum of <paramref name="offset"/> and <paramref name="length"/> is greater than the buffer length, or if <paramref name="bitSize"/> is not 224, 256, 384, or 512.</exception>
        public static byte[] Compute(byte[] data, int offset, int length, int bitSize)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "Offset must be non-negative.");
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be non-negative.");
            if (data.Length - offset < length)
                throw new ArgumentException("The sum of offset and length is greater than the buffer length.");
            if (!(new int[] { 224, 256, 384, 512 }).Contains(bitSize))
                throw new ArgumentException("Unsupported bit size", nameof(bitSize));

            Interop.SHA3_CTX ctx = new Interop.SHA3_CTX();
            byte[] result = new byte[bitSize >> 3];

            // Pin the data array and result in memory to obtain pointers
            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.SHA.SZ_SHA3_Init(&ctx, (uint)bitSize);
                // Process the data in 256 MiB chunks
                processData(dataPtr, offset, length, &ctx);
                // Finalize the hash
                Interop.SHA.SZ_SHA3_Final(resultPtr, &ctx);
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
        private static void processData(byte* dataPtr, int offset, int length, Interop.SHA3_CTX* ctx)
        {
            int remainingSize = length;
            while (remainingSize > 0)
            {
                // Determine the size of the current chunk to process
                int bytesRead = Math.Min(remainingSize, BufferSize);
                // Update the hash context with the current chunk
                Interop.SHA.SZ_SHA3_Update(ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                // Decrease the remaining size by the number of bytes read
                remainingSize -= bytesRead;
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="SHA3"/> class with the specified bit size.
        /// </summary>
        /// <param name="bitSize">The bit size of the SHA3 algorithm (224, 256, 384, 512).</param>
        /// <returns>A new instance of the <see cref="SHA3"/> class.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="bitSize"/> is not 224, 256, 384, or 512.</exception>
        public static SHA3 Create(int bitSize) => new SHA3(bitSize);

        /// <summary>
        /// Initializes the hash algorithm.
        /// </summary>
        public override void Initialize()
        {
            _ctx = new Interop.SHA3_CTX();
            // Initialize the hash context
            fixed (Interop.SHA3_CTX* ctxPtr = &_ctx)
                Interop.SHA.SZ_SHA3_Init(ctxPtr, (uint)this.HashSizeValue);
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
            fixed (Interop.SHA3_CTX* ctxPtr = &_ctx)
                processData(dataPtr, offset, size, ctxPtr);
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is written to the object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes];
            // Pin the result array in memory to obtain a pointer
            fixed (byte* resultPtr = result)
            fixed (Interop.SHA3_CTX* ctxPtr = &_ctx)
                Interop.SHA.SZ_SHA3_Final(resultPtr, ctxPtr);
            return result;
        }
    }
}



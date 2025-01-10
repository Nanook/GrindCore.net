using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

/// <summary>
/// Provides implementation of the Blake2sp hashing algorithm.
/// </summary>
namespace Nanook.GrindCore.Blake
{
    /// <summary>
    /// Represents the Blake2sp hashing algorithm.
    /// </summary>
    public unsafe class Blake2sp : HashAlgorithm
    {
        private const int _hashSizeBytes = 32;
        private Interop.CBlake2sp _state;
        private const int BufferSize = 256 * 1024 * 1024; // 256 MiB buffer

        /// <summary>
        /// Initializes a new instance of the Blake2sp class.
        /// </summary>
        public Blake2sp()
        {
            // Set the hash size value to 256 bits (32 bytes) for Blake2sp
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
            Interop.CBlake2sp state = new Interop.CBlake2sp();
            // Initialize state
            Interop.Blake.SZ_Blake2sp_Init(&state);
            // Pin the data array and update state in chunks
            fixed (byte* dataPtr = data)
                processData(dataPtr, offset, length, &state);
            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Blake2sp typically produces a 32-byte (256-bit) hash
            fixed (byte* resultPtr = result)
                Interop.Blake.SZ_Blake2sp_Final(&state, resultPtr);
            return result;
        }

        /// <summary>
        /// Processes the specified region of the byte array in 256 MiB chunks.
        /// </summary>
        private static void processData(byte* dataPtr, int offset, int length, Interop.CBlake2sp* state)
        {
            int remainingSize = length;
            while (remainingSize > 0)
            {
                // Determine the size of the current chunk to process
                int bytesRead = Math.Min(remainingSize, BufferSize);
                // Update the hash context with the current chunk
                Interop.Blake.SZ_Blake2sp_Update(state, dataPtr + offset + (length - remainingSize), (ulong)bytesRead);
                // Decrease the remaining size by the number of bytes read
                remainingSize -= bytesRead;
            }
        }

        /// <summary>
        /// Creates a new instance of the Blake2sp class.
        /// </summary>
        /// <returns>A new instance of the Blake2sp class.</returns>
        public static new Blake2sp Create() => new Blake2sp();

        /// <summary>
        /// Initializes the hash algorithm.
        /// </summary>
        public override void Initialize()
        {
            _state = new Interop.CBlake2sp();
            // Reset the hash context
            fixed (Interop.CBlake2sp* statePtr = &_state)
                Interop.Blake.SZ_Blake2sp_Init(statePtr);
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
            fixed (Interop.CBlake2sp* statePtr = &_state)
                processData(dataPtr, offset, size, statePtr);
        }

        /// <summary>
        /// Finalizes the hash computation after the last data is written to the object.
        /// </summary>
        /// <returns>The computed hash code.</returns>
        protected override byte[] HashFinal()
        {
            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Blake2sp typically produces a 32-byte (256-bit) hash
            fixed (byte* resultPtr = result)
            fixed (Interop.CBlake2sp* statePtr = &_state)
                Interop.Blake.SZ_Blake2sp_Final(statePtr, resultPtr);
            return result;
        }
    }
}

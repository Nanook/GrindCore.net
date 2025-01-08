using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.Blake
{
    public unsafe class Blake3 : HashAlgorithm
    {
        private const int _hashSizeBytes = 32;
        private Interop.Blake3Hasher _hasher;

        public Blake3()
        {
            HashSizeValue = 256; // Blake3 typically produces a 256-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            Interop.Blake3Hasher hasher = new Interop.Blake3Hasher();

            // Initialize hasher
            Interop.Blake.SZ_blake3_hasher_init(&hasher);

            // Pin the data array and update hasher in chunks
            fixed (byte* dataPtr = data)
            {
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.Blake.SZ_blake3_hasher_update(&hasher, (IntPtr)(dataPtr + offset + (length - remainingSize)), (UIntPtr)bytesRead);
                    remainingSize -= bytesRead;
                }
            }

            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Adjust size according to your needs
            fixed (byte* resultPtr = result)
                Interop.Blake.SZ_blake3_hasher_finalize(&hasher, resultPtr, (UIntPtr)result.Length);

            return result;
        }

        public static new Blake3 Create()
        {
            return new Blake3();
        }

        public override void Initialize()
        {
            _hasher = new Interop.Blake3Hasher();
            fixed (Interop.Blake3Hasher* hasherPtr = &_hasher)
                Interop.Blake.SZ_blake3_hasher_init(hasherPtr);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (Interop.Blake3Hasher* hasherPtr = &_hasher)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.Blake.SZ_blake3_hasher_update(hasherPtr, (IntPtr)(dataPtr + offset + (size - remainingSize)), (UIntPtr)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

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
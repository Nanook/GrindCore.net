using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.Blake
{
    public class Blake3 : HashAlgorithm
    {
        private const int _hashSizeBytes = 32;
        private Interop.Blake3Hasher hasher;

        public Blake3()
        {
            HashSizeValue = 256; // Blake3 typically produces a 256-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            Interop.Blake3Hasher hasher = new Interop.Blake3Hasher();

            // Initialize hasher
            Interop.Blake.SZ_blake3_hasher_init(ref hasher);

            // Pin the data array and update hasher
            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    Interop.Blake.SZ_blake3_hasher_update(ref hasher, (IntPtr)(dataPtr + offset), (UIntPtr)length);
                }
            }

            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Adjust size according to your needs
            Interop.Blake.SZ_blake3_hasher_finalize(ref hasher, result, (UIntPtr)result.Length);

            return result;
        }

        public static new Blake3 Create()
        {
            return new Blake3();
        }

        public override void Initialize()
        {
            hasher = new Interop.Blake3Hasher();
            Interop.Blake.SZ_blake3_hasher_init(ref hasher);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            // Pin the data array and update hasher
            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    Interop.Blake.SZ_blake3_hasher_update(ref hasher, (IntPtr)(dataPtr + offset), (UIntPtr)size);
                }
            }
        }

        protected override byte[] HashFinal()
        {
            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Adjust size according to your needs
            Interop.Blake.SZ_blake3_hasher_finalize(ref hasher, result, (UIntPtr)result.Length);
            return result;
        }
    }
}

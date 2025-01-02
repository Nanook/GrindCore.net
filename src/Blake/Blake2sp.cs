using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.Blake
{
    public class Blake2sp : HashAlgorithm
    {
        private const int _hashSizeBytes = 32;
        private Interop.CBlake2sp state;

        public Blake2sp()
        {
            HashSizeValue = _hashSizeBytes << 3; // Blake2sp typically produces a 256-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            Interop.CBlake2sp state = new Interop.CBlake2sp();

            // Initialize state
            Interop.Blake.SZ_Blake2sp_Init(ref state);

            // Pin the data array and update state
            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    Interop.Blake.SZ_Blake2sp_Update(ref state, dataPtr + offset, (ulong)length);
                }
            }

            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Blake2sp typically produces a 32-byte (256-bit) hash
            unsafe
            {
                fixed (byte* resultPtr = result)
                {
                    Interop.Blake.SZ_Blake2sp_Final(ref state, resultPtr);
                }
            }

            return result;
        }

        public static new Blake2sp Create()
        {
            return new Blake2sp();
        }

        public override void Initialize()
        {
            state = new Interop.CBlake2sp();
            Interop.Blake.SZ_Blake2sp_Init(ref state);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            // Pin the data array and update state
            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    Interop.Blake.SZ_Blake2sp_Update(ref state, dataPtr + offset, (ulong)size);
                }
            }
        }

        protected override byte[] HashFinal()
        {
            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Blake2sp typically produces a 32-byte (256-bit) hash
            unsafe
            {
                fixed (byte* resultPtr = result)
                {
                    Interop.Blake.SZ_Blake2sp_Final(ref state, resultPtr);
                }
            }
            return result;
        }
    }
}

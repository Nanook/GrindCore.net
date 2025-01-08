using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.Blake
{
    public unsafe class Blake2sp : HashAlgorithm
    {
        private const int _hashSizeBytes = 32;
        private Interop.CBlake2sp _state;

        public Blake2sp()
        {
            HashSizeValue = _hashSizeBytes << 3; // Blake2sp typically produces a 256-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            Interop.CBlake2sp state = new Interop.CBlake2sp();

            // Initialize state
            Interop.Blake.SZ_Blake2sp_Init(&state);

            // Pin the data array and update state in chunks
            fixed (byte* dataPtr = data)
            {
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.Blake.SZ_Blake2sp_Update(&state, dataPtr + offset + (length - remainingSize), (ulong)bytesRead);
                    remainingSize -= bytesRead;
                }
            }

            // Finalize hash
            byte[] result = new byte[_hashSizeBytes]; // Blake2sp typically produces a 32-byte (256-bit) hash
            fixed (byte* resultPtr = result)
                Interop.Blake.SZ_Blake2sp_Final(&state, resultPtr);

            return result;
        }

        public static new Blake2sp Create()
        {
            return new Blake2sp();
        }

        public override void Initialize()
        {
            _state = new Interop.CBlake2sp();
            fixed (Interop.CBlake2sp* statePtr = &_state)
                Interop.Blake.SZ_Blake2sp_Init(statePtr);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (Interop.CBlake2sp* statePtr = &_state)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.Blake.SZ_Blake2sp_Update(statePtr, dataPtr + offset + (size - remainingSize), (ulong)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

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
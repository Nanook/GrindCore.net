using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public unsafe class SHA2_256 : HashAlgorithm
    {
        private const int _hashSizeBytes = 32;
        private Interop.CSha256 _ctx;

        public SHA2_256()
        {
            HashSizeValue = _hashSizeBytes << 3; // SHA2_256 produces a 256-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            Interop.CSha256 ctx = new Interop.CSha256();
            byte[] result = new byte[_hashSizeBytes]; // SHA256_DIGEST_LENGTH is 32 bytes

            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.SHA.SZ_Sha256_Init(&ctx);
                Interop.SHA.SZ_Sha256_SetFunction(&ctx, 0); // Or use SHA1_ALGO_HW or SHA1_ALGO_SW if defined
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_Sha256_Update(&ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
                Interop.SHA.SZ_Sha256_Final(&ctx, resultPtr);
            }

            return result;
        }

        public static new SHA2_256 Create()
        {
            return new SHA2_256();
        }

        public override void Initialize()
        {
            _ctx = new Interop.CSha256();
            fixed (Interop.CSha256* ctxPtr = &_ctx)
            {
                Interop.SHA.SZ_Sha256_Init(ctxPtr);
                // Optionally, set a specific function
                Interop.SHA.SZ_Sha256_SetFunction(ctxPtr, 0); // Or use SHA1_ALGO_HW or SHA1_ALGO_SW if defined
            }
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (Interop.CSha256* ctxPtr = &_ctx)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_Sha256_Update(ctxPtr, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // SHA256_DIGEST_LENGTH is 32 bytes
            fixed (byte* resultPtr = result)
            fixed (Interop.CSha256* ctxPtr = &_ctx)
                Interop.SHA.SZ_Sha256_Final(ctxPtr, resultPtr);
            return result;
        }
    }
}
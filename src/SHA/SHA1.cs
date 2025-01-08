using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public unsafe class SHA1 : HashAlgorithm
    {
        private const int _hashSizeBytes = 20;
        private Interop.CSha1 _ctx;

        public SHA1()
        {
            HashSizeValue = _hashSizeBytes << 3; // SHA1 produces a 160-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            Interop.CSha1 ctx = new Interop.CSha1();
            byte[] result = new byte[_hashSizeBytes]; // SHA1_DIGEST_LENGTH is 20 bytes

            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.SHA.SZ_Sha1_Init(&ctx);
                Interop.SHA.SZ_Sha1_SetFunction(&ctx, 0); // Or use SHA1_ALGO_HW or SHA1_ALGO_SW if defined
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_Sha1_Update(&ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
                Interop.SHA.SZ_Sha1_Final(&ctx, resultPtr);
            }

            return result;
        }

        public static new SHA1 Create()
        {
            return new SHA1();
        }

        public override void Initialize()
        {
            _ctx = new Interop.CSha1();
            fixed (Interop.CSha1* ctxPtr = &_ctx)
            {
                Interop.SHA.SZ_Sha1_Init(ctxPtr);
                // Optionally, set a specific function
                Interop.SHA.SZ_Sha1_SetFunction(ctxPtr, 0); // Or use SHA1_ALGO_HW or SHA1_ALGO_SW if defined
            }
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (Interop.CSha1* ctxPtr = &_ctx)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_Sha1_Update(ctxPtr, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // SHA1_DIGEST_LENGTH is 20 bytes
            fixed (byte* resultPtr = result)
            fixed (Interop.CSha1* ctxPtr = &_ctx)
                Interop.SHA.SZ_Sha1_Final(ctxPtr, resultPtr);
            return result;
        }
    }
}
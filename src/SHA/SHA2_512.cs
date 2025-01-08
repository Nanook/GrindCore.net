using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public unsafe class SHA2_512 : HashAlgorithm
    {
        private const int _hashSizeBytes = 64;
        private Interop.SHA512_CTX _ctx;

        public SHA2_512()
        {
            HashSizeValue = 512; // SHA2_512 produces a 512-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            Interop.SHA512_CTX ctx = new Interop.SHA512_CTX();
            byte[] result = new byte[_hashSizeBytes]; // SHA512_DIGEST_LENGTH is 64

            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.SHA.SZ_SHA512_Init(&ctx);
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_SHA512_Update(&ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
                Interop.SHA.SZ_SHA512_Final(resultPtr, &ctx);
            }

            return result;
        }

        public static new SHA2_512 Create()
        {
            return new SHA2_512();
        }

        public override void Initialize()
        {
            _ctx = new Interop.SHA512_CTX();
            fixed (Interop.SHA512_CTX* ctxPtr = &_ctx)
            {
                Interop.SHA.SZ_SHA512_Init(ctxPtr);
            }
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (Interop.SHA512_CTX* ctxPtr = &_ctx)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_SHA512_Update(ctxPtr, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // SHA512_DIGEST_LENGTH is 64
            fixed (byte* resultPtr = result)
            fixed (Interop.SHA512_CTX* ctxPtr = &_ctx)
                Interop.SHA.SZ_SHA512_Final(resultPtr, ctxPtr);
            return result;
        }
    }
}
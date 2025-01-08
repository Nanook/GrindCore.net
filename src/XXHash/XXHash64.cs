using Nanook.GrindCore.XXHash;
using System;
using System.IO;
using System.Security.Cryptography;

namespace Nanook.GrindCore.XXHash
{
    public unsafe class XXHash64 : HashAlgorithm
    {
        private XXH64_CTX _ctx;

        public XXHash64()
        {
            HashSizeValue = 64; // XXH64 produces a 64-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            XXH64_CTX ctx = new XXH64_CTX();

            fixed (byte* dataPtr = data)
            {
                XXHash.SZ_XXH64_Reset(&ctx);
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    XXHash.SZ_XXH64_Update(&ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
                return XXHash.SZ_XXH64_Digest(&ctx).ToByteArray();
            }
        }

        public static new XXHash64 Create()
        {
            return new XXHash64();
        }

        public override void Initialize()
        {
            _ctx = new XXH64_CTX();
            fixed (XXH64_CTX* ctxPtr = &_ctx)
                XXHash.SZ_XXH64_Reset(ctxPtr);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (XXH64_CTX* ctxPtr = &_ctx)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    XXHash.SZ_XXH64_Update(ctxPtr, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

        protected override byte[] HashFinal()
        {
            fixed (XXH64_CTX* ctxPtr = &_ctx)
                return XXHash.SZ_XXH64_Digest(ctxPtr).ToByteArray();
        }
    }
}
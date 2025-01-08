using Nanook.GrindCore.XXHash;
using System.IO;
using System.Security.Cryptography;
using System;

namespace Nanook.GrindCore.XXHash
{
    public unsafe class XXHash32 : HashAlgorithm
    {
        private XXH32_CTX _ctx;

        public XXHash32()
        {
            HashSizeValue = 32; // XXH32 produces a 32-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            XXH32_CTX ctx = new XXH32_CTX();

            fixed (byte* dataPtr = data)
            {
                XXHash.SZ_XXH32_Reset(&ctx);
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    XXHash.SZ_XXH32_Update(&ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
                return XXHash.SZ_XXH32_Digest(&ctx).ToByteArray();
            }
        }

        public static new XXHash32 Create()
        {
            return new XXHash32();
        }

        public override void Initialize()
        {
            _ctx = new XXH32_CTX();
            fixed (XXH32_CTX* ctxPtr = &_ctx)
                XXHash.SZ_XXH32_Reset(ctxPtr);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (XXH32_CTX* ctxPtr = &_ctx)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    XXHash.SZ_XXH32_Update(ctxPtr, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

        protected override byte[] HashFinal()
        {
            fixed (XXH32_CTX* ctxPtr = &_ctx)
                return XXHash.SZ_XXH32_Digest(ctxPtr).ToByteArray();
        }
    }
}
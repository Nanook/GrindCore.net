using Nanook.GrindCore.XXHash;
using System.IO;
using System.Security.Cryptography;
using System;

namespace Nanook.GrindCore.XXHash
{
    public class XXHash32 : HashAlgorithm
    {
        private XXH32_CTX ctx;

        public XXHash32()
        {
            HashSizeValue = 32; // XXH32 produces a 32-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            var ctx = new XXH32_CTX();

            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    XXHash.SZ_XXH32_Reset(ref ctx);
                    XXHash.SZ_XXH32_Update(ref ctx, dataPtr + offset, (nuint)length);
                    return XXHash.SZ_XXH32_Digest(ref ctx).ToByteArray();
                }
            }
        }

        public static byte[] Compute(Stream stream)
        {
            var ctx = new XXH32_CTX();
            const int bufferSize = 64 * 1024; // 64 KiB buffer
            byte[] buffer = new byte[bufferSize];

            unsafe
            {
                XXHash.SZ_XXH32_Reset(ref ctx);
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    fixed (byte* bufferPtr = buffer)
                        XXHash.SZ_XXH32_Update(ref ctx, bufferPtr, (nuint)bytesRead);
                }
                return XXHash.SZ_XXH32_Digest(ref ctx).ToByteArray();
            }
        }

        public static new XXHash32 Create()
        {
            return new XXHash32();
        }

        public override void Initialize()
        {
            ctx = new XXH32_CTX();
            XXHash.SZ_XXH32_Reset(ref ctx);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    while (remainingSize > 0)
                    {
                        bytesRead = Math.Min(remainingSize, bufferSize);
                        XXHash.SZ_XXH32_Update(ref ctx, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                        remainingSize -= bytesRead;
                    }
                }
            }
        }

        protected override byte[] HashFinal()
        {
            unsafe
            {
                return XXHash.SZ_XXH32_Digest(ref ctx).ToByteArray();
            }
        }
    }
}
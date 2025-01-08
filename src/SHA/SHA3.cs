using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public unsafe class SHA3 : HashAlgorithm
    {
        private int _hashSizeBytes;
        private Interop.SHA3_CTX _ctx;

        public SHA3(int bitSize)
        {
            if (!(new int[] { 224, 256, 384, 512 }).Contains(bitSize))
                throw new ArgumentException("Unsupported bit size");

            HashSizeValue = bitSize; // SHA3 bit size (224, 256, 384, 512)
            _hashSizeBytes = bitSize >> 3;
            Initialize();
        }

        public static byte[] Compute(byte[] data, int bitSize) => Compute(data, 0, data.Length, bitSize);

        public static byte[] Compute(byte[] data, int offset, int length, int bitSize)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            if (!(new int[] { 224, 256, 384, 512 }).Contains(bitSize))
                throw new ArgumentException("Unsupported bit size");

            Interop.SHA3_CTX ctx = new Interop.SHA3_CTX();
            byte[] result = new byte[bitSize >> 3];

            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.SHA.SZ_SHA3_Init(&ctx, (uint)bitSize);
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_SHA3_Update(&ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
                Interop.SHA.SZ_SHA3_Final(resultPtr, &ctx);
            }

            return result;
        }

        public static SHA3 Create(int bitSize)
        {
            return new SHA3(bitSize);
        }

        public override void Initialize()
        {
            _ctx = new Interop.SHA3_CTX();
            fixed (Interop.SHA3_CTX* ctxPtr = &_ctx)
            {
                Interop.SHA.SZ_SHA3_Init(ctxPtr, (uint)this.HashSizeValue);
            }
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (Interop.SHA3_CTX* ctxPtr = &_ctx)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_SHA3_Update(ctxPtr, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes];
            fixed (byte* resultPtr = result)
            fixed (Interop.SHA3_CTX* ctxPtr = &_ctx)
                Interop.SHA.SZ_SHA3_Final(resultPtr, ctxPtr);
            return result;
        }
    }
}
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.MD
{
    public unsafe class MD5 : HashAlgorithm
    {
        private const int _hashSizeBytes = 16;
        private Interop.MD5_CTX _ctx;

        public MD5()
        {
            HashSizeValue = _hashSizeBytes << 3; // MD5 produces a 128-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            Interop.MD5_CTX ctx = new Interop.MD5_CTX();
            byte[] result = new byte[_hashSizeBytes]; // MD5_DIGEST_LENGTH is 16

            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.MD.SZ_MD5_Init(&ctx);
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.MD.SZ_MD5_Update(&ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
                Interop.MD.SZ_MD5_Final(resultPtr, &ctx);
            }

            return result;
        }

        public static new MD5 Create()
        {
            return new MD5();
        }

        public override void Initialize()
        {
            _ctx = new Interop.MD5_CTX();
            fixed (Interop.MD5_CTX* ctxPtr = &_ctx)
                Interop.MD.SZ_MD5_Init(ctxPtr);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (Interop.MD5_CTX* ctxPtr = &_ctx)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.MD.SZ_MD5_Update(ctxPtr, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // MD5_DIGEST_LENGTH is 16
            fixed (byte* resultPtr = result)
            fixed (Interop.MD5_CTX* ctxPtr = &_ctx)
                Interop.MD.SZ_MD5_Final(resultPtr, ctxPtr);
            return result;
        }

    }
}
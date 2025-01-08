using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public unsafe class SHA2_384 : HashAlgorithm
    {
        private const int _hashSizeBytes = 48;
        private Interop.SHA384_CTX _ctx;

        public SHA2_384()
        {
            HashSizeValue = _hashSizeBytes << 3; // SHA2_384 produces a 384-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer
            Interop.SHA384_CTX ctx = new Interop.SHA384_CTX();
            byte[] result = new byte[_hashSizeBytes]; // SHA384_DIGEST_LENGTH is 48

            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.SHA.SZ_SHA384_Init(&ctx);
                int bytesRead;
                int remainingSize = length;
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_SHA384_Update(&ctx, dataPtr + offset + (length - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
                Interop.SHA.SZ_SHA384_Final(resultPtr, &ctx);
            }

            return result;
        }

        public static new SHA2_384 Create()
        {
            return new SHA2_384();
        }

        public override void Initialize()
        {
            _ctx = new Interop.SHA384_CTX();
            fixed (Interop.SHA384_CTX* ctxPtr = &_ctx)
            {
                Interop.SHA.SZ_SHA384_Init(ctxPtr);
            }
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            const int bufferSize = 256 * 1024 * 1024; // 256 MiB buffer

            int bytesRead;
            int remainingSize = size;
            fixed (byte* dataPtr = data)
            fixed (Interop.SHA384_CTX* ctxPtr = &_ctx)
            {
                while (remainingSize > 0)
                {
                    bytesRead = Math.Min(remainingSize, bufferSize);
                    Interop.SHA.SZ_SHA384_Update(ctxPtr, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // SHA384_DIGEST_LENGTH is 48
            fixed (byte* resultPtr = result)
            fixed (Interop.SHA384_CTX* ctxPtr = &_ctx)
                Interop.SHA.SZ_SHA384_Final(resultPtr, ctxPtr);
            return result;
        }
    }
}
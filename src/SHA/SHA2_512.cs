using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public class SHA2_512 : HashAlgorithm
    {
        private Interop.SHA512_CTX ctx;

        public SHA2_512()
        {
            HashSizeValue = 512; // SHA2_512 produces a 512-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            Interop.SHA512_CTX ctx = new Interop.SHA512_CTX();
            byte[] result = new byte[64]; // SHA512_DIGEST_LENGTH is 64

            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* resultPtr = result)
                {
                    Interop.SHA.SZ_SHA512_Init(ref ctx);
                    Interop.SHA.SZ_SHA512_Update(ref ctx, dataPtr + offset, (nuint)length);
                    Interop.SHA.SZ_SHA512_Final(resultPtr, ref ctx);
                }
            }

            return result;
        }

        public static new SHA2_512 Create()
        {
            return new SHA2_512();
        }

        public override void Initialize()
        {
            ctx = new Interop.SHA512_CTX();
            Interop.SHA.SZ_SHA512_Init(ref ctx);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            unsafe
            {
                fixed (byte* dataPtr = data)
                    Interop.SHA.SZ_SHA512_Update(ref ctx, dataPtr + offset, (nuint)size);
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[64]; // SHA512_DIGEST_LENGTH is 64
            unsafe
            {
                fixed (byte* resultPtr = result)
                    Interop.SHA.SZ_SHA512_Final(resultPtr, ref ctx);
            }
            return result;
        }
    }
}

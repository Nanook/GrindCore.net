using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public class SHA2_384 : HashAlgorithm
    {
        private const int _hashSizeBytes = 48;
        private Interop.SHA384_CTX ctx;

        public SHA2_384()
        {
            HashSizeValue = _hashSizeBytes << 3; // SHA2_384 produces a 384-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            Interop.SHA384_CTX ctx = new Interop.SHA384_CTX();
            byte[] result = new byte[_hashSizeBytes]; // SHA384_DIGEST_LENGTH is 48

            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* resultPtr = result)
                {
                    Interop.SHA.SZ_SHA384_Init(ref ctx);
                    Interop.SHA.SZ_SHA384_Update(ref ctx, dataPtr + offset, (nuint)length);
                    Interop.SHA.SZ_SHA384_Final(resultPtr, ref ctx);
                }
            }

            return result;
        }

        public static new SHA2_384 Create()
        {
            return new SHA2_384();
        }

        public override void Initialize()
        {
            ctx = new Interop.SHA384_CTX();
            Interop.SHA.SZ_SHA384_Init(ref ctx);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            unsafe
            {
                fixed (byte* dataPtr = data)
                    Interop.SHA.SZ_SHA384_Update(ref ctx, dataPtr + offset, (nuint)size);
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // SHA384_DIGEST_LENGTH is 48
            unsafe
            {
                fixed (byte* resultPtr = result)
                    Interop.SHA.SZ_SHA384_Final(resultPtr, ref ctx);
            }
            return result;
        }
    }
}

using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public class SHA1 : HashAlgorithm
    {
        private const int _hashSizeBytes = 20;
        private Interop.CSha1 ctx;

        public SHA1()
        {
            HashSizeValue = _hashSizeBytes << 3; // SHA1 produces a 160-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            Interop.CSha1 ctx = new Interop.CSha1();
            byte[] result = new byte[_hashSizeBytes]; // SHA1_DIGEST_LENGTH is 20 bytes

            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* resultPtr = result)
                {
                    Interop.SHA.SZ_Sha1_Init(ref ctx);
                    Interop.SHA.SZ_Sha1_SetFunction(ref ctx, 0); // Or use SHA1_ALGO_HW or SHA1_ALGO_SW if defined
                    Interop.SHA.SZ_Sha1_Update(ref ctx, dataPtr + offset, (nuint)length);
                    Interop.SHA.SZ_Sha1_Final(ref ctx, resultPtr);
                }
            }

            return result;
        }

        public static new SHA1 Create()
        {
            return new SHA1();
        }

        public override void Initialize()
        {
            ctx = new Interop.CSha1();
            Interop.SHA.SZ_Sha1_Init(ref ctx);
            // Optionally, set a specific function
            Interop.SHA.SZ_Sha1_SetFunction(ref ctx, 0); // Or use SHA1_ALGO_HW or SHA1_ALGO_SW if defined
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            unsafe
            {
                fixed (byte* dataPtr = data)
                    Interop.SHA.SZ_Sha1_Update(ref ctx, dataPtr + offset, (nuint)size);
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // SHA1_DIGEST_LENGTH is 20 bytes
            unsafe
            {
                fixed (byte* resultPtr = result)
                    Interop.SHA.SZ_Sha1_Final(ref ctx, resultPtr);
            }
            return result;
        }
    }
}

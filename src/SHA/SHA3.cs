using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public class SHA3 : HashAlgorithm
    {
        private Interop.SHA3_CTX ctx;
        private int bitSize;

        public SHA3(int bitSize)
        {
            this.bitSize = bitSize;
            HashSizeValue = bitSize; // SHA3 bit size (224, 256, 384, 512)
            Initialize();
        }

        public static byte[] Compute(byte[] data, int bitSize) => Compute(data, 0, data.Length, bitSize);

        public static byte[] Compute(byte[] data, int offset, int length, int bitSize)
        {
            Interop.SHA3_CTX ctx = new Interop.SHA3_CTX();
            byte[] result;

            switch (bitSize)
            {
                case 224:
                    result = new byte[28]; // SHA3_224_DIGEST_LENGTH is 28
                    break;
                case 256:
                    result = new byte[32]; // SHA3_256_DIGEST_LENGTH is 32
                    break;
                case 384:
                    result = new byte[48]; // SHA3_384_DIGEST_LENGTH is 48
                    break;
                case 512:
                    result = new byte[64]; // SHA3_512_DIGEST_LENGTH is 64
                    break;
                default:
                    throw new ArgumentException("Unsupported bit size");
            }

            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* resultPtr = result)
                {
                    Interop.SHA.SZ_SHA3_Init(ref ctx, (uint)bitSize);
                    Interop.SHA.SZ_SHA3_Update(ref ctx, dataPtr + offset, (nuint)length);
                    Interop.SHA.SZ_SHA3_Final(resultPtr, ref ctx);
                }
            }

            return result;
        }

        public static SHA3 Create(int bitSize)
        {
            return new SHA3(bitSize);
        }

        public override void Initialize()
        {
            ctx = new Interop.SHA3_CTX();
            Interop.SHA.SZ_SHA3_Init(ref ctx, (uint)bitSize);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            unsafe
            {
                fixed (byte* dataPtr = data)
                    Interop.SHA.SZ_SHA3_Update(ref ctx, dataPtr + offset, (nuint)size);
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[bitSize / 8];
            unsafe
            {
                fixed (byte* resultPtr = result)
                    Interop.SHA.SZ_SHA3_Final(resultPtr, ref ctx);
            }
            return result;
        }
    }
}

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.SHA
{
    public class SHA3 : HashAlgorithm
    {
        private int _hashSizeBytes;
        private Interop.SHA3_CTX ctx;

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
            if (!(new int[] { 224, 256, 384, 512 }).Contains(bitSize))
                throw new ArgumentException("Unsupported bit size");

            Interop.SHA3_CTX ctx = new Interop.SHA3_CTX();
            byte[] result = new byte[bitSize >> 3];

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
            Interop.SHA.SZ_SHA3_Init(ref ctx, (uint)this.HashSizeValue);
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
            byte[] result = new byte[_hashSizeBytes];
            unsafe
            {
                fixed (byte* resultPtr = result)
                    Interop.SHA.SZ_SHA3_Final(resultPtr, ref ctx);
            }
            return result;
        }
    }
}

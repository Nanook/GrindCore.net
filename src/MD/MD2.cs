using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.MD
{
    public class MD2 : HashAlgorithm
    {
        private Interop.MD2_CTX ctx;

        public MD2()
        {
            HashSizeValue = 128; // MD2 produces a 128-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            Interop.MD2_CTX ctx = new Interop.MD2_CTX();
            byte[] result = new byte[16]; // MD2_DIGEST_LENGTH is 16

            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* resultPtr = result)
                {
                    Interop.MD.SZ_MD2_Init(ref ctx);
                    Interop.MD.SZ_MD2_Update(ref ctx, dataPtr + offset, (nuint)length);
                    Interop.MD.SZ_MD2_Final(resultPtr, ref ctx);
                }
            }

            return result;
        }

        public static new MD2 Create()
        {
            return new MD2();
        }

        public override void Initialize()
        {
            ctx = new Interop.MD2_CTX();
            Interop.MD.SZ_MD2_Init(ref ctx);
        }

        protected override void HashCore(byte[] data, int offset, int size)
        {
            unsafe
            {
                fixed (byte* dataPtr = data)
                    Interop.MD.SZ_MD2_Update(ref ctx, dataPtr + offset, (nuint)size);
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[16]; // MD2_DIGEST_LENGTH is 16
            unsafe
            {
                fixed (byte* resultPtr = result)
                    Interop.MD.SZ_MD2_Final(resultPtr, ref ctx);
            }
            return result;
        }
    }
}

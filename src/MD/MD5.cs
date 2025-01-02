using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Nanook.GrindCore.MD
{
    public class MD5 : HashAlgorithm
    {
        private const int _hashSizeBytes = 16;
        private Interop.MD5_CTX ctx;

        public MD5()
        {
            HashSizeValue = _hashSizeBytes << 3; // MD5 produces a 128-bit hash
            Initialize();
        }

        public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

        public static byte[] Compute(byte[] data, int offset, int length)
        {
            if (length > 256 * 1024 * 1024) // 256 MiB
            {
                using (var stream = new MemoryStream(data, offset, length))
                    return Compute(stream);
            }

            Interop.MD5_CTX ctx = new Interop.MD5_CTX();
            byte[] result = new byte[_hashSizeBytes]; // MD5_DIGEST_LENGTH is 16

            unsafe
            {
                fixed (byte* dataPtr = data)
                fixed (byte* resultPtr = result)
                {
                    Interop.MD.SZ_MD5_Init(ref ctx);
                    Interop.MD.SZ_MD5_Update(ref ctx, dataPtr + offset, (nuint)length);
                    Interop.MD.SZ_MD5_Final(resultPtr, ref ctx);
                }
            }

            return result;
        }

        public static byte[] Compute(Stream stream)
        {
            Interop.MD5_CTX ctx = new Interop.MD5_CTX();
            byte[] result = new byte[16]; // MD5_DIGEST_LENGTH is 16
            const int bufferSize = 64 * 1024; // 64 KiB buffer
            byte[] buffer = new byte[bufferSize];

            unsafe
            {
                fixed (byte* resultPtr = result)
                {
                    Interop.MD.SZ_MD5_Init(ref ctx);
                    int bytesRead;
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        fixed (byte* bufferPtr = buffer)
                            Interop.MD.SZ_MD5_Update(ref ctx, bufferPtr, (nuint)bytesRead);
                    }
                    Interop.MD.SZ_MD5_Final(resultPtr, ref ctx);
                }
            }

            return result;
        }

        public static new MD5 Create()
        {
            return new MD5();
        }

        public override void Initialize()
        {
            ctx = new Interop.MD5_CTX();
            Interop.MD.SZ_MD5_Init(ref ctx);
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
                        Interop.MD.SZ_MD5_Update(ref ctx, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                        remainingSize -= bytesRead;
                    }
                }
            }
        }

        protected override byte[] HashFinal()
        {
            byte[] result = new byte[_hashSizeBytes]; // MD5_DIGEST_LENGTH is 16
            unsafe
            {
                fixed (byte* resultPtr = result)
                    Interop.MD.SZ_MD5_Final(resultPtr, ref ctx);
            }
            return result;
        }
    }
}

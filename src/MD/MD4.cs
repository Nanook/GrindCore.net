using Nanook.GrindCore;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

public class MD4 : HashAlgorithm
{
    private const int _hashSizeBytes = 16;
    private Interop.MD4_CTX ctx;

    public MD4()
    {
        HashSizeValue = _hashSizeBytes << 3; // MD4 produces a 128-bit hash
        Initialize();
    }

    public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

    public static byte[] Compute(byte[] data, int offset, int length)
    {
        Interop.MD4_CTX ctx = new Interop.MD4_CTX();
        byte[] result = new byte[_hashSizeBytes]; // MD5_DIGEST_LENGTH is 16

        unsafe
        {
            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.MD.SZ_MD4_Init(ref ctx);
                Interop.MD.SZ_MD4_Update(ref ctx, dataPtr + offset, (nuint)length);
                Interop.MD.SZ_MD4_Final(resultPtr, ref ctx);
            }
        }

        return result;
    }

    public static new MD4 Create()
    {
        return new MD4();
    }

    public override void Initialize()
    {
        ctx = new Interop.MD4_CTX();
        Interop.MD.SZ_MD4_Init(ref ctx);
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
                    Interop.MD.SZ_MD4_Update(ref ctx, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }
    }

    protected override byte[] HashFinal()
    {
        byte[] result = new byte[_hashSizeBytes]; // MD4_DIGEST_LENGTH is 16
        unsafe
        {
            fixed (byte* resultPtr = result)
                Interop.MD.SZ_MD4_Final(resultPtr, ref ctx);
        }
        return result;
    }
}


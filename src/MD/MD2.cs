using Nanook.GrindCore;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

public class MD2 : HashAlgorithm
{
    private const int _hashSizeBytes = 16;
    private Interop.MD2_CTX ctx;
    private HashBuffer _buffer;

    public MD2()
    {
        HashSizeValue = _hashSizeBytes << 3; // MD2 produces a 128-bit hash
        _buffer = new HashBuffer(_hashSizeBytes);
        Initialize();
    }

    public static byte[] Compute(byte[] data) => Compute(data, 0, data.Length);

    public static byte[] Compute(byte[] data, int offset, int length)
    {
        Interop.MD2_CTX ctx = new Interop.MD2_CTX();
        HashBuffer buffer = new HashBuffer(_hashSizeBytes);
        byte[] result = new byte[_hashSizeBytes]; // MD5_DIGEST_LENGTH is 16

        unsafe
        {
            fixed (byte* dataPtr = data)
            fixed (byte* resultPtr = result)
            {
                Interop.MD.SZ_MD2_Init(ref ctx);
                buffer.Process(data, offset, length, (d, o, s) => bufferProcess(ref ctx, d, o, s));
                buffer.Complete((d, o, s) => bufferPadProcess(ref ctx, d, o, s));
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

    private static void bufferPadProcess(ref Interop.MD2_CTX ctx, byte[] data, int offset, int size)
    {
        byte paddingValue = (byte)(data.Length - size);

        // Pad the buffer with the padding value
        for (int i = size; i < data.Length; i++)
            data[i] = paddingValue;

        bufferProcess(ref ctx, data, offset, size);
    }

    private static void bufferProcess(ref Interop.MD2_CTX ctx, byte[] data, int offset, int size)
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
                    Interop.MD.SZ_MD2_Update(ref ctx, dataPtr + offset + (size - remainingSize), (nuint)bytesRead);
                    remainingSize -= bytesRead;
                }
            }
        }
    }

    protected override void HashCore(byte[] data, int offset, int size)
    {
        _buffer.Process(data, offset, size, (d, o, s) => bufferProcess(ref ctx, d, o, s));
    }

    protected override byte[] HashFinal()
    {
        _buffer.Complete((d, o, s) => bufferPadProcess(ref ctx, d, o, s));

        byte[] result = new byte[_hashSizeBytes]; // MD2_DIGEST_LENGTH is 16
        unsafe
        {
            fixed (byte* resultPtr = result)
                Interop.MD.SZ_MD2_Final(resultPtr, ref ctx);
        }
        return result;
    }
}


using System;
using HashAlgorithm=System.Security.Cryptography.HashAlgorithm;
using Nanook.GrindCore.MD;
using Nanook.GrindCore.SHA;
using Nanook.GrindCore.Blake;

namespace Nanook.GrindCore
{
    public enum HashType
    {
        Blake3,
        MD2,
        MD4,
        MD5,
        SHA2_384,
        SHA2_512,
        SHA3_224,
        SHA3_256,
        SHA3_384,
        SHA3_512
    }

    public class HashFactory
    {
        public static HashAlgorithm Create(HashType type)
        {
            switch (type)
            {
                case HashType.Blake3:
                    return Blake3.Create();
                case HashType.MD2:
                    return MD2.Create();
                case HashType.MD4:
                    return MD4.Create();
                case HashType.MD5:
                    return MD5.Create();
                case HashType.SHA2_384:
                    return SHA2_384.Create();
                case HashType.SHA2_512:
                    return SHA2_512.Create();
                case HashType.SHA3_224:
                    return SHA3.Create(224);
                case HashType.SHA3_256:
                    return SHA3.Create(256);
                case HashType.SHA3_384:
                    return SHA3.Create(384);
                case HashType.SHA3_512:
                    return SHA3.Create(512);
                default:
                    throw new ArgumentException("Unsupported hash type", nameof(type));
            }
        }
    }
}

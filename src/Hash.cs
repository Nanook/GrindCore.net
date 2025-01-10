using System;
using HashAlgorithm = System.Security.Cryptography.HashAlgorithm;
using Nanook.GrindCore.MD;
using Nanook.GrindCore.SHA;
using Nanook.GrindCore.Blake;
using Nanook.GrindCore.XXHash;
using System.Collections.Generic;

namespace Nanook.GrindCore
{
    public enum HashType
    {
        Blake2sp,
        Blake3,
        XXHash32,
        XXHash64,
        MD2,
        MD4,
        MD5,
        SHA1,
        SHA2_256,
        SHA2_384,
        SHA2_512,
        SHA3_224,
        SHA3_256,
        SHA3_384,
        SHA3_512
    }


    public class Hash
    {
        private static readonly Dictionary<HashType, Func<HashAlgorithm>> hashCreators = new Dictionary<HashType, Func<HashAlgorithm>>()
        {
            { HashType.Blake2sp, () => Blake2sp.Create() },
            { HashType.Blake3, () => Blake3.Create() },
            { HashType.XXHash32, () => XXHash32.Create() },
            { HashType.XXHash64, () => XXHash64.Create() },
            { HashType.MD2, () => MD2.Create() },
            { HashType.MD4, () => MD4.Create() },
            { HashType.MD5, () => MD5.Create() },
            { HashType.SHA1, () => SHA1.Create() },
            { HashType.SHA2_256, () => SHA2_256.Create() },
            { HashType.SHA2_384, () => SHA2_384.Create() },
            { HashType.SHA2_512, () => SHA2_512.Create() },
            { HashType.SHA3_224, () => SHA3.Create(224) },
            { HashType.SHA3_256, () => SHA3.Create(256) },
            { HashType.SHA3_384, () => SHA3.Create(384) },
            { HashType.SHA3_512, () => SHA3.Create(512) }
        };

        public static HashAlgorithm Create(HashType type)
        {
            if (hashCreators.TryGetValue(type, out var creator))
                return creator();

            throw new ArgumentException("Unsupported hash type", nameof(type));
        }

        public static byte[] Compute(HashType type, byte[] data) => Compute(type, data, 0, data.Length);

        public static byte[] Compute(HashType type, byte[] data, int offset, int length)
        {
            using (var algorithm = Create(type))
                return algorithm.ComputeHash(data, offset, length);
        }
    }
}

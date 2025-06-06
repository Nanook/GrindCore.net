using System;
using HashAlgorithm = System.Security.Cryptography.HashAlgorithm;
using Nanook.GrindCore.MD;
using Nanook.GrindCore.SHA;
using Nanook.GrindCore.Blake;
using Nanook.GrindCore.XXHash;
using System.Collections.Generic;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Provides factory methods for creating and computing hash values using various supported hash algorithms.
    /// </summary>
    public class HashFactory
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

        /// <summary>
        /// Creates a new <see cref="HashAlgorithm"/> instance for the specified <see cref="HashType"/>.
        /// </summary>
        /// <param name="type">The hash algorithm type to create.</param>
        /// <returns>A new <see cref="HashAlgorithm"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the specified hash type is not supported.</exception>
        public static HashAlgorithm Create(HashType type)
        {
            if (hashCreators.TryGetValue(type, out var creator))
                return creator();

            throw new ArgumentException("Unsupported hash type", nameof(type));
        }

        /// <summary>
        /// Computes the hash value for the entire input data using the specified hash algorithm type.
        /// </summary>
        /// <param name="type">The hash algorithm type to use.</param>
        /// <param name="data">The input data to hash.</param>
        /// <returns>The computed hash value as a byte array.</returns>
        public static byte[] Compute(HashType type, byte[] data) => Compute(type, data, 0, data.Length);

        /// <summary>
        /// Computes the hash value for a segment of the input data using the specified hash algorithm type.
        /// </summary>
        /// <param name="type">The hash algorithm type to use.</param>
        /// <param name="data">The input data to hash.</param>
        /// <param name="offset">The offset in the input data at which to begin hashing.</param>
        /// <param name="length">The number of bytes to hash.</param>
        /// <returns>The computed hash value as a byte array.</returns>
        public static byte[] Compute(HashType type, byte[] data, int offset, int length)
        {
            using (var algorithm = Create(type))
                return algorithm.ComputeHash(data, offset, length);
        }
    }
}

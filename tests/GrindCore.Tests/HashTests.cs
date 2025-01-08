using System;
using System.Diagnostics;
using HashAlgorithm=System.Security.Cryptography.HashAlgorithm;
using CryptoStream=System.Security.Cryptography.CryptoStream;
using CryptoStreamMode=System.Security.Cryptography.CryptoStreamMode;
using Nanook.GrindCore;
using Nanook.GrindCore.Blake;
using Nanook.GrindCore.MD;
using Nanook.GrindCore.SHA;
using Nanook.GrindCore.XXHash;

namespace GrindCore.Tests
{
    /// <summary>
    /// Example tests to demonstrate usage
    /// </summary>
    public sealed class HashTests
    {
        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing 64 KiB of data.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResult64kSHA3_512)]
        public void Hash_ByteArray64k(HashType type, string expectedResult)
        {
            // create predictable data - this data is always the same fibonacci sequence
            byte[] data = Shared.CreateData(64 * 1024);

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                // calculate the hash using standard dotnet HashAlgorithm base class functionality
                string result = algorithm.ComputeHash(data).ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing an empty array of data.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResultEmptyBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResultEmptyBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResultEmptyXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResultEmptyXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResultEmptyMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResultEmptyMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResultEmptyMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResultEmptySHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResultEmptySHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResultEmptySHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResultEmptySHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResultEmptySHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResultEmptySHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResultEmptySHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResultEmptySHA3_512)]
        public void Hash_ByteArrayEmpty(HashType type, string expectedResult)
        {
            // empty byte array
            byte[] data = new byte[0];

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                // calculate the hash using standard dotnet HashAlgorithm base class functionality
                string result = algorithm.ComputeHash(data).ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing a bytearray of data in 1000 byte chunks.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResult64kSHA3_512)]
        public void Hash_ByteArray64k_Chunk1000b(HashType type, string expectedResult)
        {
            // create predictable data - this data is always the same fibonacci sequence
            byte[] data = Shared.CreateData(64 * 1024);
            int chunkSize = 1000; // 1000 byte chunk size - breaks 16 byte alignment etc

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                int offset = 0;
                while (offset < data.Length)
                {
                    int size = Math.Min(chunkSize, data.Length - offset);
                    hasher.TransformBlock(data, offset, size, null, 0);
                    offset += size;
                }
                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string result = hasher.Hash!.ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing a bytearray of data 1 byte at a time.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResult64kSHA3_512)]
        public void Hash_ByteArray64k_Chunk1b(HashType type, string expectedResult)
        {
            // create predictable data - this data is always the same fibonacci sequence
            byte[] data = Shared.CreateData(64 * 1024);
            int chunkSize = 1; // 1 byte at a time

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                int offset = 0;
                while (offset < data.Length)
                {
                    int size = Math.Min(chunkSize, data.Length - offset);
                    hasher.TransformBlock(data, offset, size, null, 0);
                    offset += size;
                }
                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string result = hasher.Hash!.ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing a stream of data in 1000 byte chunks.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResult64kSHA3_512)]
        public void Hash_Streamed64k_Chunk1000b(HashType type, string expectedResult)
        {
            // Create predictable data - this data is always the same Fibonacci sequence
            byte[] data = Shared.CreateData(64 * 1024);
            int chunkSize = 1000; // 1000 byte chunk size - breaks 16 byte alignment etc

            // Create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                using (Stream dataStream = new MemoryStream(data))
                {
                    int bytesRead;
                    byte[] buffer = new byte[chunkSize];

                    while ((bytesRead = dataStream.Read(buffer, 0, buffer.Length)) > 0)
                        hasher.TransformBlock(buffer, 0, bytesRead, null, 0);
                }

                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string result = hasher.Hash!.ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing a stream of data in 1000 byte chunks.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResult64kSHA3_512)]
        public void Hash_CryptoStreamed64k(HashType type, string expectedResult)
        {
            // Create predictable data - this data is always the same Fibonacci sequence
            byte[] data = Shared.CreateData(64 * 1024);

            // Create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                using (CryptoStream cryptoStream = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write))
                {
                    using (Stream dataStream = new MemoryStream(data))
                    {
                        dataStream.CopyTo(cryptoStream);
                        cryptoStream.FlushFinalBlock();
                    }
                }
                string result = hasher.Hash!.ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing a stream of data in 1 byte at a time.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResult64kSHA3_512)]
        public void Hash_Streamed64k_Chunk1b(HashType type, string expectedResult)
        {
            // create predictable data - this data is always the same fibonacci sequence
            byte[] data = Shared.CreateData(64 * 1024);
            int chunkSize = 1; // 1 byte at a time

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                using (Stream dataStream = new MemoryStream(data))
                {
                    int bytesRead;
                    byte[] buffer = new byte[chunkSize];

                    while ((bytesRead = dataStream.Read(buffer, 0, buffer.Length)) > 0)
                        hasher.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                hasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string result = hasher.Hash!.ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate static hash Compute method, processing 64 KiB of data.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResult64kSHA3_512)]
        public void HashCompute_ByteArray64k(HashType type, string expectedResult)
        {
            // create predictable data - this data is always the same fibonacci sequence
            byte[] data = Shared.CreateData(64 * 1024);
            byte[] result;

            switch (type)
            {
                case HashType.Blake2sp:
                    result = Blake2sp.Compute(data);
                    break;
                case HashType.Blake3:
                    result = Blake3.Compute(data);
                    break;
                case HashType.XXHash32:
                    result = XXHash32.Compute(data);
                    break;
                case HashType.XXHash64:
                    result = XXHash64.Compute(data);
                    break;
                case HashType.MD2:
                    result = MD2.Compute(data);
                    break;
                case HashType.MD4:
                    result = MD4.Compute(data);
                    break;
                case HashType.MD5:
                    result = MD5.Compute(data);
                    break;
                case HashType.SHA1:
                    result = SHA1.Compute(data);
                    break;
                case HashType.SHA2_256:
                    result = SHA2_256.Compute(data);
                    break;
                case HashType.SHA2_384:
                    result = SHA2_384.Compute(data);
                    break;
                case HashType.SHA2_512:
                    result = SHA2_512.Compute(data);
                    break;
                case HashType.SHA3_224:
                    result = SHA3.Compute(data, 224);
                    break;
                case HashType.SHA3_256:
                    result = SHA3.Compute(data, 256);
                    break;
                case HashType.SHA3_384:
                    result = SHA3.Compute(data, 384);
                    break;
                case HashType.SHA3_512:
                    result = SHA3.Compute(data, 512);
                    break;
                default:
                    throw new ArgumentException("Unsupported hash type", nameof(type));
            }

            Assert.Equal(expectedResult, result.ToHexString());
        }

        /// <summary>
        /// Test and demonstrate static hash Compute method, processing 6 bytes at offset 8.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, "bc6c1daaea7a6b0645967b49f5f74c613afd86105188d3837a79d3e62894d447")]
        [InlineData(HashType.Blake3, "e777710dd2c3181d6ea14e4bda8289bea33278d6be5dd56411998e3614535a9a")]
        [InlineData(HashType.XXHash32, "5a5b3357")]
        [InlineData(HashType.XXHash64, "e8cd893b963242a1")]
        [InlineData(HashType.MD2, "f36164764d36c0404fdbb53640c00254")]
        [InlineData(HashType.MD4, "41bd3a366e5f3ffaf2d965c5d5407f46")]
        [InlineData(HashType.MD5, "a86f8e2d2784da236a59ccd6a7e28bde")]
        [InlineData(HashType.SHA1, "50f054bfebd5efe33ab33558631ea285a0027af1")]
        [InlineData(HashType.SHA2_256, "26101343ebf799735f3c5a9abe389511e6e9abcc560b1b3c703a47136da15f0a")]
        [InlineData(HashType.SHA2_384, "535439aaf57a7206371eb128e965f3b85cb06e0fa2d0d56e4bc7173b39aa68baa5a8ac8cc6bf05cd06b6dd0a4cb9f72a")]
        [InlineData(HashType.SHA2_512, "205e468bf4fed324c4773faffb18f6f04d490c21b9f08fb4903bcca77749ebd42382342e8d5405af51a2084608eddd8920d02b9520864a6949051b9c98462335")]
        [InlineData(HashType.SHA3_224, "de82002920fe92e8c3fa82d965e3bc936c5f1ea43685f1ffcab0d4b0")]
        [InlineData(HashType.SHA3_256, "7a989069840e0bca92b03e328d55308fff2269e5428d15fff92e73e2d1ffe103")]
        [InlineData(HashType.SHA3_384, "82ace2cf1b5ce42e9cbb9183bd4ee38ae988e7623cc6d4cbba79e97666b7296bdfa3e1d793dff63e558d149f28524659")]
        [InlineData(HashType.SHA3_512, "bce8412610443d3e22aeb3d83c4466052c31c41e99e39f32f78448eb1c0d9cdab9daa47f571cc6bd1f311d01f93bf64dbe177c5cbe95399f43ea0262f595a6b9")]
        public void HashCompute_ByteArray64k_Offset8_Size6b(HashType type, string expectedResult)
        {
            // create predictable data - this data is always the same fibonacci sequence
            byte[] data = Shared.CreateData(64 * 1024);
            byte[] result;

            switch (type)
            {
                case HashType.Blake2sp:
                    result = Blake2sp.Compute(data, 8, 6);
                    break;
                case HashType.Blake3:
                    result = Blake3.Compute(data, 8, 6);
                    break;
                case HashType.XXHash32:
                    result = XXHash32.Compute(data, 8, 6);
                    break;
                case HashType.XXHash64:
                    result = XXHash64.Compute(data, 8, 6);
                    break;
                case HashType.MD2:
                    result = MD2.Compute(data, 8, 6);
                    break;
                case HashType.MD4:
                    result = MD4.Compute(data, 8, 6);
                    break;
                case HashType.MD5:
                    result = MD5.Compute(data, 8, 6);
                    break;
                case HashType.SHA1:
                    result = SHA1.Compute(data, 8, 6);
                    break;
                case HashType.SHA2_256:
                    result = SHA2_256.Compute(data, 8, 6);
                    break;
                case HashType.SHA2_384:
                    result = SHA2_384.Compute(data, 8, 6);
                    break;
                case HashType.SHA2_512:
                    result = SHA2_512.Compute(data, 8, 6);
                    break;
                case HashType.SHA3_224:
                    result = SHA3.Compute(data, 8, 6, 224);
                    break;
                case HashType.SHA3_256:
                    result = SHA3.Compute(data, 8, 6, 256);
                    break;
                case HashType.SHA3_384:
                    result = SHA3.Compute(data, 8, 6, 384);
                    break;
                case HashType.SHA3_512:
                    result = SHA3.Compute(data, 8, 6, 512);
                    break;
                default:
                    throw new ArgumentException("Unsupported hash type", nameof(type));
            }

            Assert.Equal(expectedResult, result.ToHexString());
        }
    }
}

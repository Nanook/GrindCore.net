using System;
using System.Diagnostics;
using HashAlgorithm = System.Security.Cryptography.HashAlgorithm;
using CryptoStream = System.Security.Cryptography.CryptoStream;
using CryptoStreamMode = System.Security.Cryptography.CryptoStreamMode;
using Nanook.GrindCore;
using Nanook.GrindCore.Blake;
using Nanook.GrindCore.MD;
using Nanook.GrindCore.SHA;
using Nanook.GrindCore.XXHash;
using GrindCore.Tests.Utility;

namespace GrindCore.Tests
{
    /// <summary>
    /// Example tests to demonstrate usage
    /// </summary>
    public sealed class HashTests
    {
        private static byte[] _dataEmpty;
        private static byte[] _data64KiB;

        static HashTests()
        {
            _dataEmpty = new byte[0];
            _data64KiB = TestDataStream.Create(64 * 1024);
        }

#if WIN_X64
        private static byte[]? _dataHalfGiB;

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing one 512GiB array of data.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult512MiBBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult512MiBBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult512MiBMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult512MiBMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult512MiBMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult512MiBSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult512MiBSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult512MiBSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult512MiBSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult512MiBSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult512MiBSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult512MiBSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult512MiBSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult512MiBXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult512MiBXXHash64)]
        public void Hash_ByteArray512GiB(HashType type, string expectedResult)
        {
            if (_dataHalfGiB == null)
                _dataHalfGiB = TestDataStream.Create(512 * 1024 * 1024);

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                // calculate the hash using standard dotnet HashAlgorithm base class functionality
                string result = algorithm.ComputeHash(_dataHalfGiB).ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }
#endif

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing 64 KiB of data.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult64kBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult64kSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult64kXXHash64)]
        public void Hash_ByteArray64KiB(HashType type, string expectedResult)
        {
            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                // calculate the hash using standard dotnet HashAlgorithm base class functionality
                string result = algorithm.ComputeHash(_data64KiB).ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing an empty array of data.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResultEmptyBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResultEmptyBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResultEmptyMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResultEmptyMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResultEmptyMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResultEmptySHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResultEmptySHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResultEmptySHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResultEmptySHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResultEmptySHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResultEmptySHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResultEmptySHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResultEmptySHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResultEmptyXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResultEmptyXXHash64)]
        public void Hash_ByteArrayEmpty(HashType type, string expectedResult)
        {
            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                // calculate the hash using standard dotnet HashAlgorithm base class functionality
                string result = algorithm.ComputeHash(_dataEmpty).ToHexString();
                Assert.Equal(expectedResult, result);
            }
        }

        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing a bytearray of data in 1000 byte chunks.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult64kBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult64kSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult64kXXHash64)]
        public void Hash_ByteArray64k_Chunk1000B(HashType type, string expectedResult)
        {
            int chunkSize = 1000; // 1000 byte chunk size - breaks 16 byte alignment etc

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                int offset = 0;
                while (offset < _data64KiB.Length)
                {
                    int size = Math.Min(chunkSize, _data64KiB.Length - offset);
                    hasher.TransformBlock(_data64KiB, offset, size, null, 0);
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
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult64kBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult64kSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult64kXXHash64)]
        public void Hash_ByteArray64k_Chunk1B(HashType type, string expectedResult)
        {
            int chunkSize = 1; // 1 byte at a time

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                int offset = 0;
                while (offset < _data64KiB.Length)
                {
                    int size = Math.Min(chunkSize, _data64KiB.Length - offset);
                    hasher.TransformBlock(_data64KiB, offset, size, null, 0);
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
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult64kBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult64kSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult64kXXHash64)]
        public void Hash_Streamed64k_Chunk1000B(HashType type, string expectedResult)
        {
            int chunkSize = 1000; // 1000 byte chunk size - breaks 16 byte alignment etc

            // Create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                using (Stream dataStream = new MemoryStream(_data64KiB))
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
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult64kBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult64kSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult64kXXHash64)]
        public void Hash_CryptoStreamed64KiB(HashType type, string expectedResult)
        {
            // Create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                using (CryptoStream cryptoStream = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write))
                {
                    using (Stream dataStream = new MemoryStream(_data64KiB))
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
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult64kBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult64kSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult64kXXHash64)]
        public void Hash_Streamed64k_Chunk1B(HashType type, string expectedResult)
        {
            int chunkSize = 1; // 1 byte at a time

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = HashFactory.Create(type))
            {
                using (Stream dataStream = new MemoryStream(_data64KiB))
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
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult64kBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult64kSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult64kXXHash64)]
        public void HashCompute_ByteArray64KiB(HashType type, string expectedResult)
        {
            byte[] result;

            switch (type)
            {
                case HashType.Blake2sp:
                    result = Blake2sp.Compute(_data64KiB);
                    break;
                case HashType.Blake3:
                    result = Blake3.Compute(_data64KiB);
                    break;
                case HashType.XXHash32:
                    result = XXHash32.Compute(_data64KiB);
                    break;
                case HashType.XXHash64:
                    result = XXHash64.Compute(_data64KiB);
                    break;
                case HashType.MD2:
                    result = MD2.Compute(_data64KiB);
                    break;
                case HashType.MD4:
                    result = MD4.Compute(_data64KiB);
                    break;
                case HashType.MD5:
                    result = MD5.Compute(_data64KiB);
                    break;
                case HashType.SHA1:
                    result = SHA1.Compute(_data64KiB);
                    break;
                case HashType.SHA2_256:
                    result = SHA2_256.Compute(_data64KiB);
                    break;
                case HashType.SHA2_384:
                    result = SHA2_384.Compute(_data64KiB);
                    break;
                case HashType.SHA2_512:
                    result = SHA2_512.Compute(_data64KiB);
                    break;
                case HashType.SHA3_224:
                    result = SHA3.Compute(_data64KiB, 224);
                    break;
                case HashType.SHA3_256:
                    result = SHA3.Compute(_data64KiB, 256);
                    break;
                case HashType.SHA3_384:
                    result = SHA3.Compute(_data64KiB, 384);
                    break;
                case HashType.SHA3_512:
                    result = SHA3.Compute(_data64KiB, 512);
                    break;
                default:
                    throw new ArgumentException("Unsupported hash type", nameof(type));
            }

            Assert.Equal(expectedResult, result.ToHexString());
        }

        /// <summary>
        /// Test and demonstrate static hash Compute method, processing a subset of data using the static Compute() method.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResultSubsetBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResultSubsetBlake3)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResultSubsetMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResultSubsetMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResultSubsetMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResultSubsetSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResultSubsetSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResultSubsetSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResultSubsetSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResultSubsetSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResultSubsetSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResultSubsetSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResultSubsetSHA3_512)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResultSubsetXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResultSubsetXXHash64)]
        public void HashCompute_ByteArray64KiB_StaticSubset(HashType type, string expectedResult)
        {
            byte[] result;

            switch (type)
            {
                case HashType.Blake2sp:
                    result = Blake2sp.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.Blake3:
                    result = Blake3.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.XXHash32:
                    result = XXHash32.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.XXHash64:
                    result = XXHash64.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.MD2:
                    result = MD2.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.MD4:
                    result = MD4.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.MD5:
                    result = MD5.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.SHA1:
                    result = SHA1.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.SHA2_256:
                    result = SHA2_256.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.SHA2_384:
                    result = SHA2_384.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.SHA2_512:
                    result = SHA2_512.Compute(_data64KiB, 8, 6);
                    break;
                case HashType.SHA3_224:
                    result = SHA3.Compute(_data64KiB, 8, 6, 224);
                    break;
                case HashType.SHA3_256:
                    result = SHA3.Compute(_data64KiB, 8, 6, 256);
                    break;
                case HashType.SHA3_384:
                    result = SHA3.Compute(_data64KiB, 8, 6, 384);
                    break;
                case HashType.SHA3_512:
                    result = SHA3.Compute(_data64KiB, 8, 6, 512);
                    break;
                default:
                    throw new ArgumentException("Unsupported hash type", nameof(type));
            }

            Assert.Equal(expectedResult, result.ToHexString());
        }
    }
}

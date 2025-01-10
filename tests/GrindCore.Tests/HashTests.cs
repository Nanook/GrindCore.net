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
        private static byte[] _dataEmpty;
        private static byte[] _data64KiB;
#if WIN_X64
        private static byte[] _dataHalfGiB;
#endif
        static HashTests()
        {
            _dataEmpty = new byte[0];
            _data64KiB = DataStream.Create(64 * 1024);
#if WIN_X64
            _dataHalfGiB = DataStream.Create(512 * 1024 * 1024);
#endif
        }

#if WIN_X64
        /// <summary>
        /// Test and demonstrate an instance of a hashing algorithm, processing one 512GiB array of data.
        /// The hasher is set to type HashAlgorith, this allows it to be used with other classes in the framework.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, "f154b8559195aa2ae4bfc8f14468004dbeebcc23090545f75df4534113e914b6")]
        [InlineData(HashType.Blake3, "c3ccdab391aaf3796448cd33df12b43af77f471c239002110e8b9bbfc2abb6b6")]
        [InlineData(HashType.XXHash32, "0a0b9340")]
        [InlineData(HashType.XXHash64, "c668fabe6e6e9235")]
        [InlineData(HashType.MD2, "67aabd667b657b803e589b6efa881cc3")]
        [InlineData(HashType.MD4, "931853c4f8f053b80e359e8583d08ab3")]
        [InlineData(HashType.MD5, "a015668048777f4053294805c7464c1e")]
        [InlineData(HashType.SHA1, "e94e01a6ac07e0756f725bd990a3ea7255a619ab")]
        [InlineData(HashType.SHA2_256, "24b2e231ac75592e3398dfb633dec8c5f1f2622e336e002601c5e9ca65292034")]
        [InlineData(HashType.SHA2_384, "eaef364656be00ee25cc5f9eeb03618c166a18f58d7c3c8c6cc26102333f7ef88f4b5f4a4ca549af75e40366d2b4b1b7")]
        [InlineData(HashType.SHA2_512, "66d6c86811bd9a97ab5495a7f5567a1cf99d34723f91dfcab3bdebb9bc73d64efac6509df0b20207b1452247d21616a552001de368c4c8a687343698626f0959")]
        [InlineData(HashType.SHA3_224, "58f0af4fa13505abccb78d813269113bedf549a8b97cda06b2b017b1")]
        [InlineData(HashType.SHA3_256, "09f4feaae6c05dda1ffb85e3091d2f588d45dfdac8428f91ac62394dcf7c7d24")]
        [InlineData(HashType.SHA3_384, "453e46e18031b60319e063c4ed6017fbde730a9a3c8b9110e078b3bc62e39644c3996a1dfd50d399583fd2fa40a45257")]
        [InlineData(HashType.SHA3_512, "ea5e7f94964cd553b88a161316ad7b34c958cb2d3975fdfce3e3c9bafd49803bd09b64c9358b8b63e319768b70ba63e2c03d2d0964c99de49718e887c7ff4dcd")]
        public void Hash_ByteArray512GiB(HashType type, string expectedResult)
        {
            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm algorithm = Hash.Create(type))
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
            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm algorithm = Hash.Create(type))
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
            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm algorithm = Hash.Create(type))
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
            int chunkSize = 1000; // 1000 byte chunk size - breaks 16 byte alignment etc

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = Hash.Create(type))
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
            int chunkSize = 1; // 1 byte at a time

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = Hash.Create(type))
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
            int chunkSize = 1000; // 1000 byte chunk size - breaks 16 byte alignment etc

            // Create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = Hash.Create(type))
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
            // Create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = Hash.Create(type))
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
            int chunkSize = 1; // 1 byte at a time

            // create the hash using the factory, via switch (not reflection)
            using (HashAlgorithm hasher = Hash.Create(type))
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
        public void HashCompute_ByteArray64k_StaticSubset(HashType type, string expectedResult)
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

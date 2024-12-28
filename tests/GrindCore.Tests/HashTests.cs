using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Xunit;
using Nanook.GrindCore;

namespace GrindCore.Tests
{
    public sealed class HashTests
    {
        [Theory]
        [InlineData(HashType.MD2, 0x10000, "739c02d1a383c5b87252e6ceadc7301b")]
        [InlineData(HashType.MD4, 0x10000, "4f5f5bdedc32a4c4771cce9b53861baf")]
        [InlineData(HashType.MD5, 0x10000, "8721441d106901d27a5835122e6eae8f")]
        [InlineData(HashType.SHA2_384, 0x10000, "3af6a866616e4b616a3b4bd39c2c3f1475e1adae103c02d87d7ce8f760c738d5efd94f1476f5b94e6c44951dfd179585")]
        [InlineData(HashType.SHA2_512, 0x10000, "2cc9df9261e95e3acafce1b3d49ac1079bfed3618ae3e7615251768bf6c1f2ebd42173e0d3790653aa2fd5c69d413db0c4fab99d134c621353f1469fdd9dd757")]
        [InlineData(HashType.SHA3_224, 0x10000, "56db86f2db22847e884b9c2a1cef3e2203b7121133c565f041c868f2")]
        [InlineData(HashType.SHA3_256, 0x10000, "83628ca2866070d71859ca650c4cbe7fa5bfafc6e833223424c6f30baff137b9")]
        [InlineData(HashType.SHA3_384, 0x10000, "8b927bc922c5eee02e43f6332f8b66413418bb46fa913f4d8733fed36f700320a651afe950b5eecec8c2449c4aa88130")]
        [InlineData(HashType.SHA3_512, 0x10000, "337f525a855f2a00f84e0012dd34268e0fba9e88a8e2dc3bc4f8449afddb2dc697b22faa69b284b4a261f65a90bfb13aa12887b86fa54e3306942ecfb8c3e202")]
        [InlineData(HashType.Blake3, 0x10000, "fb7ff288a1f6f138c2d29a43cc34a42b75b4b40bf5552207160f06877b7d8d11")]
        public void TestHash(HashType type, int dataSize, string expectedResult)
        {
            byte[] data = Shared.CreateData(dataSize);

            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                byte[] hash = algorithm.ComputeHash(data);
                string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                Assert.Equal(expectedResult, hashString);
            }
        }

        [Theory]
        [InlineData(HashType.MD2, 0, "8350e5a3e24c153df2275c9f80692773")]
        [InlineData(HashType.MD4, 0, "31d6cfe0d16ae931b73c59d7e0c089c0")]
        [InlineData(HashType.MD5, 0, "d41d8cd98f00b204e9800998ecf8427e")]
        [InlineData(HashType.SHA2_384, 0, "38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b")]
        [InlineData(HashType.SHA2_512, 0, "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e")]
        [InlineData(HashType.SHA3_224, 0, "6b4e03423667dbb73b6e15454f0eb1abd4597f9a1b078e3f5b5a6bc7")]
        [InlineData(HashType.SHA3_256, 0, "a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a")]
        [InlineData(HashType.SHA3_384, 0, "0c63a75b845e4f7d01107d852e4c2485c51a50aaaa94fc61995e71bbee983a2ac3713831264adb47fb6bd1e058d5f004")]
        [InlineData(HashType.SHA3_512, 0, "a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a615b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26")]
        [InlineData(HashType.Blake3, 0, "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262")]
        public void TestEmptyHash(HashType type, int dataSize, string expectedResult)
        {
            byte[] data = Shared.CreateData(dataSize);

            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                byte[] hash = algorithm.ComputeHash(data);
                string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                Trace.WriteLine(hashString);
                Assert.Equal(expectedResult, hashString);
            }
        }
    }
}
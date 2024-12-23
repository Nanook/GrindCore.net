using System;
using System.Diagnostics;
using HashAlgorithm=System.Security.Cryptography.HashAlgorithm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nanook.GrindCore.MD;
using Nanook.GrindCore.SHA;
using Nanook.GrindCore.Blake;
using Nanook.GrindCore;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto;

namespace GrindCore.Tests
{
    [TestClass]
    public sealed class HashTests
    {
        [DataTestMethod]
        [DataRow(HashType.MD2, 0x10000, "739c02d1a383c5b87252e6ceadc7301b")]
        [DataRow(HashType.MD4, 0x10000, "4f5f5bdedc32a4c4771cce9b53861baf")]
        [DataRow(HashType.MD5, 0x10000, "8721441d106901d27a5835122e6eae8f")]
        [DataRow(HashType.SHA2_384, 0x10000, "3af6a866616e4b616a3b4bd39c2c3f1475e1adae103c02d87d7ce8f760c738d5efd94f1476f5b94e6c44951dfd179585")]
        [DataRow(HashType.SHA2_512, 0x10000, "2cc9df9261e95e3acafce1b3d49ac1079bfed3618ae3e7615251768bf6c1f2ebd42173e0d3790653aa2fd5c69d413db0c4fab99d134c621353f1469fdd9dd757")]
        [DataRow(HashType.SHA3_224, 0x10000, "56db86f2db22847e884b9c2a1cef3e2203b7121133c565f041c868f2")]
        [DataRow(HashType.SHA3_256, 0x10000, "83628ca2866070d71859ca650c4cbe7fa5bfafc6e833223424c6f30baff137b9")]
        [DataRow(HashType.SHA3_384, 0x10000, "8b927bc922c5eee02e43f6332f8b66413418bb46fa913f4d8733fed36f700320a651afe950b5eecec8c2449c4aa88130")]
        [DataRow(HashType.SHA3_512, 0x10000, "337f525a855f2a00f84e0012dd34268e0fba9e88a8e2dc3bc4f8449afddb2dc697b22faa69b284b4a261f65a90bfb13aa12887b86fa54e3306942ecfb8c3e202")]
        [DataRow(HashType.Blake3, 0x10000, "fb7ff288a1f6f138c2d29a43cc34a42b75b4b40bf5552207160f06877b7d8d11")]
        public void TestHash(HashType type, int dataSize, string expectedResult)
        {
            byte[] data = Shared.CreateData(dataSize);

            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                byte[] hash = algorithm.ComputeHash(data);
                string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                Assert.AreEqual(expectedResult, hashString);
            }
        }

        [DataTestMethod]
        [DataRow(HashType.MD2, 0, "8350e5a3e24c153df2275c9f80692773")]
        [DataRow(HashType.MD4, 0, "31d6cfe0d16ae931b73c59d7e0c089c0")]
        [DataRow(HashType.MD5, 0, "d41d8cd98f00b204e9800998ecf8427e")]
        [DataRow(HashType.SHA2_384, 0, "38b060a751ac96384cd9327eb1b1e36a21fdb71114be07434c0cc7bf63f6e1da274edebfe76f65fbd51ad2f14898b95b")]
        [DataRow(HashType.SHA2_512, 0, "cf83e1357eefb8bdf1542850d66d8007d620e4050b5715dc83f4a921d36ce9ce47d0d13c5d85f2b0ff8318d2877eec2f63b931bd47417a81a538327af927da3e")]
        [DataRow(HashType.SHA3_224, 0, "6b4e03423667dbb73b6e15454f0eb1abd4597f9a1b078e3f5b5a6bc7")]
        [DataRow(HashType.SHA3_256, 0, "a7ffc6f8bf1ed76651c14756a061d662f580ff4de43b49fa82d80a4b80f8434a")]
        [DataRow(HashType.SHA3_384, 0, "0c63a75b845e4f7d01107d852e4c2485c51a50aaaa94fc61995e71bbee983a2ac3713831264adb47fb6bd1e058d5f004")]
        [DataRow(HashType.SHA3_512, 0, "a69f73cca23a9ac5c8b567dc185a756e97c982164fe25859e0d1dcc1475c80a615b2123af1f5f94c11e3e9402c3ac558f500199d95b6d3e301758586281dcd26")]
        [DataRow(HashType.Blake3, 0, "af1349b9f5f9a1a6a0404dea36dcc9499bcb25c9adc112b7cc9a93cae41f3262")]
        public void TestEmptyHash(HashType type, int dataSize, string expectedResult)
        {
            byte[] data = Shared.CreateData(dataSize);

            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                byte[] hash = algorithm.ComputeHash(data);
                string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                Trace.WriteLine(hashString);
                Assert.AreEqual(expectedResult, hashString);
            }
        }




        //[TestMethod]
        //public void TestMethod1()
        //{
        //    // Create instances of each hash algorithm and assign a name to each
        //    var hashAlgorithms = new (object algorithm, string name)[]
        //    {
        //        (System.Security.Cryptography.SHA1.Create(), "DotNet       SHA1    "),
        //        (new Sha1Digest(), "BouncyCastle SHA1    "),
        //        (MD2.Create(), "GrindCore    MD2     "),
        //        (new MD2Digest(), "BouncyCastle MD2     "),
        //        (MD4.Create(), "GrindCore    MD4     "),
        //        (new MD4Digest(), "BouncyCastle MD4     "),
        //        (MD5.Create(), "GrindCore    MD5     "),
        //        (System.Security.Cryptography.MD5.Create(), "DotNet       MD5     "),
        //        (new MD5Digest(), "BouncyCastle MD5     "),
        //        (SHA2_384.Create(), "GrindCore    SHA2-384"),
        //        (new Sha384Digest(), "BouncyCastle SHA2-384"),
        //        (SHA2_512.Create(), "GrindCore    SHA2-512"),
        //        (new Sha512Digest(), "BouncyCastle SHA2-512"),
        //        (SHA3.Create(224), "GrindCore    SHA3-224"),
        //        (new Sha3Digest(224), "BouncyCastle SHA3-224"),
        //        (SHA3.Create(256), "GrindCore    SHA3-256"),
        //        (new Sha3Digest(256), "BouncyCastle SHA3-256"),
        //        (SHA3.Create(384), "GrindCore    SHA3-384"),
        //        (new Sha3Digest(384), "BouncyCastle SHA3-384"),
        //        (SHA3.Create(512), "GrindCore    SHA3-512"),
        //        (new Sha3Digest(512), "BouncyCastle SHA3-512"),
        //        (Blake3.Create(), "GrindCore    Blake3  "),
        //        (new Blake3Digest(), "BouncyCastle Blake3  ")
        //    };

        //    // Example usage: compute hash for a sample byte array
        //    byte[] data = Shared.CreateData(0x10000);

        //    foreach (var (algorithm, name) in hashAlgorithms)
        //    {
        //        // Measure the time taken to compute the hash
        //        Stopwatch stopwatch = Stopwatch.StartNew();
        //        byte[] hash;

        //        if (algorithm is IDigest bouncyCastleDigest)
        //        {
        //            bouncyCastleDigest.BlockUpdate(data, 0, data.Length);
        //            hash = new byte[bouncyCastleDigest.GetDigestSize()];
        //            bouncyCastleDigest.DoFinal(hash, 0);
        //        }
        //        else if (algorithm is HashAlgorithm hashAlgorithm)
        //        {
        //            hash = hashAlgorithm.ComputeHash(data);
        //        }
        //        else
        //        {
        //            throw new InvalidOperationException("Unsupported algorithm type");
        //        }

        //        stopwatch.Stop();

        //        // Format the output
        //        string formattedOutput = $"{name,-20} {stopwatch.ElapsedMilliseconds,5} ms  : {BitConverter.ToString(hash).Replace("-", "").ToLower()}";
        //        Trace.WriteLine(formattedOutput);
        //    }
        //}
    }
}

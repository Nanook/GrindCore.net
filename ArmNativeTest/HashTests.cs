using System;
using System.Diagnostics;
using HashAlgorithm=System.Security.Cryptography.HashAlgorithm;
using Nanook.GrindCore;

namespace GrindCore.Tests
{
    public sealed class HashTests
    {
        public void TestHash(HashType type, int dataSize, string expectedResult)
        {
            byte[] data = Shared.CreateData(dataSize);

            using (HashAlgorithm algorithm = HashFactory.Create(type))
            {
                byte[] hash = algorithm.ComputeHash(data);
                string hashString = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                Console.WriteLine($"{(expectedResult == hashString ? "Y" : "N")} {type} {hashString}");
            }
        }
    }
}

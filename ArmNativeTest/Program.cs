using GrindCore.Tests;
using Nanook.GrindCore;

namespace ArmNativeTest
{
    internal class Program
    {
        static void Main(string[] args)
        {
            HashTests test = new HashTests();
            test.TestHash(HashType.MD2, 0x10000, "739c02d1a383c5b87252e6ceadc7301b");
            test.TestHash(HashType.MD4, 0x10000, "4f5f5bdedc32a4c4771cce9b53861baf");
            test.TestHash(HashType.MD5, 0x10000, "8721441d106901d27a5835122e6eae8f");
            test.TestHash(HashType.SHA2_384, 0x10000, "3af6a866616e4b616a3b4bd39c2c3f1475e1adae103c02d87d7ce8f760c738d5efd94f1476f5b94e6c44951dfd179585");
            test.TestHash(HashType.SHA2_512, 0x10000, "2cc9df9261e95e3acafce1b3d49ac1079bfed3618ae3e7615251768bf6c1f2ebd42173e0d3790653aa2fd5c69d413db0c4fab99d134c621353f1469fdd9dd757");
            test.TestHash(HashType.SHA3_224, 0x10000, "56db86f2db22847e884b9c2a1cef3e2203b7121133c565f041c868f2");
            test.TestHash(HashType.SHA3_256, 0x10000, "83628ca2866070d71859ca650c4cbe7fa5bfafc6e833223424c6f30baff137b9");
            test.TestHash(HashType.SHA3_384, 0x10000, "8b927bc922c5eee02e43f6332f8b66413418bb46fa913f4d8733fed36f700320a651afe950b5eecec8c2449c4aa88130");
            test.TestHash(HashType.SHA3_512, 0x10000, "337f525a855f2a00f84e0012dd34268e0fba9e88a8e2dc3bc4f8449afddb2dc697b22faa69b284b4a261f65a90bfb13aa12887b86fa54e3306942ecfb8c3e202");
            test.TestHash(HashType.Blake3, 0x10000, "fb7ff288a1f6f138c2d29a43cc34a42b75b4b40bf5552207160f06877b7d8d11");


            Console.WriteLine("Hello, World!");
        }
    }
}

namespace Nanook.GrindCore
{
    /// <summary>
    /// Specifies supported hash algorithms for integrity or fingerprinting.
    /// </summary>
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
}

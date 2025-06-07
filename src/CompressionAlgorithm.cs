namespace Nanook.GrindCore
{
    /// <summary>
    /// Supported Compression Algorithms
    /// </summary>
    public enum CompressionAlgorithm
    {
        Copy,
        GZip,
        ZLib,
        Deflate,
        GZipNg,
        ZLibNg,
        DeflateNg,
        Brotli,
        Lzma,
        Lzma2,
        FastLzma2,
        Lz4,
        ZStd
    }
}

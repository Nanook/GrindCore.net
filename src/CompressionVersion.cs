




using System;

namespace Nanook.GrindCore
{
    public enum ZLibVersion
    {
        v1_3_1 = 1,
        Latest = v1_3_1
    }
    public enum ZLibNgVersion
    {
        v2_2_1 = 1,
        Latest = v2_2_1
    }
    public enum BrotliVersion
    {
      v1_1_0 = 1,
      Latest = v1_1_0
    }
    public enum LzmaVersion
    {
        v24_7_0 = 1,
        Latest = v24_7_0
    }
    public enum Lzma2Version
    {
        v24_7_0 = 1,
        Latest = v24_7_0
    }
    public enum FastLzma2Version
    {
        v1_0_1 = 1,
        Latest = v1_0_1
    }

    public class CompressionVersion
    {
        public static string LATEST = "";
        public static string ZLIB_v1_3_1 = "1.3.1";
        public static string ZLIBNG_v2_2_1 = "2.2.1";
        public static string BROTLI_v1_1_0 = "1.1.0";
        public static string LZMA_v24_7_0 = "24.7.0";
        public static string LZMA2_v24_7_0 = "24.7.0";
        public static string FASTLZMA2_v1_0_1 = "1.0.1";

        private static string enumStringToVersionString(string enumName) => enumName.Replace("v", "").Replace("_", ".");

        public static CompressionVersion Create(CompressionAlgorithm algorithm, string version)
        {
            return create(algorithm, version);
        }

        public static CompressionVersion BrotliLatest() => Brotli(BrotliVersion.Latest);
        public static CompressionVersion Brotli(BrotliVersion version) => create(CompressionAlgorithm.Brotli, enumStringToVersionString(Enum.GetName(typeof(BrotliVersion), (int)version)!)); //ensure .Latest is converted to .vx_x_x item

        public static CompressionVersion ZLibLatest() => ZLib(ZLibVersion.Latest);
        public static CompressionVersion ZLib(ZLibVersion version) => create(CompressionAlgorithm.ZLib, enumStringToVersionString(Enum.GetName(typeof(ZLibVersion), (int)version)!)); //ensure .Latest is converted to .vx_x_x item

        public static CompressionVersion ZLibNgLatest() => ZLibNg(ZLibNgVersion.Latest);
        public static CompressionVersion ZLibNg(ZLibNgVersion version) => create(CompressionAlgorithm.ZLibNg, enumStringToVersionString(Enum.GetName(typeof(ZLibNgVersion), (int)version)!)); //ensure .Latest is converted to .vx_x_x item

        public static CompressionVersion LzmaLatest() => Lzma(LzmaVersion.Latest);
        public static CompressionVersion Lzma(LzmaVersion version) => create(CompressionAlgorithm.Lzma, enumStringToVersionString(Enum.GetName(typeof(LzmaVersion), (int)version)!)); //ensure .Latest is converted to .vx_x_x item

        public static CompressionVersion Lzma2Latest() => Lzma2(Lzma2Version.Latest);
        public static CompressionVersion Lzma2(Lzma2Version version) => create(CompressionAlgorithm.Lzma2, enumStringToVersionString(Enum.GetName(typeof(Lzma2Version), (int)version)!)); //ensure .Latest is converted to .vx_x_x item

        public static CompressionVersion FastLzma2Latest() => FastLzma2(FastLzma2Version.Latest);
        public static CompressionVersion FastLzma2(FastLzma2Version version) => create(CompressionAlgorithm.Lzma2, enumStringToVersionString(Enum.GetName(typeof(FastLzma2Version), (int)version)!)); //ensure .Latest is converted to .vx_x_x item


        private CompressionVersion()
        {
        }

        private static CompressionVersion create(CompressionAlgorithm algorithm, string version)
        {
            CompressionVersion result = new CompressionVersion();
            result.Index = -1;

            switch (algorithm)
            {
                case CompressionAlgorithm.Lzma:
                    if (string.IsNullOrEmpty(version) || version == LZMA_v24_7_0)
                    {
                        result.Version = LZMA_v24_7_0;
                        result.Index = 0;
                    }
                    break;
                case CompressionAlgorithm.Lzma2:
                    if (string.IsNullOrEmpty(version) || version == LZMA2_v24_7_0)
                    {
                        result.Version = LZMA2_v24_7_0;
                        result.Index = 0;
                    }
                    break;
                case CompressionAlgorithm.FastLzma2:
                    if (string.IsNullOrEmpty(version) || version == FASTLZMA2_v1_0_1)
                    {
                        result.Version = FASTLZMA2_v1_0_1;
                        result.Index = 0;
                    }
                    break;
                case CompressionAlgorithm.Brotli:
                    if (string.IsNullOrEmpty(version) || version == BROTLI_v1_1_0)
                    {
                        result.Version = BROTLI_v1_1_0;
                        result.Index = 0;
                    }
                    break;
                case CompressionAlgorithm.DeflateNg:
                case CompressionAlgorithm.GZipNg:
                case CompressionAlgorithm.ZLibNg:
                    if (string.IsNullOrEmpty(version) || version == ZLIBNG_v2_2_1)
                    {
                        result.Version = ZLIBNG_v2_2_1;
                        result.Index = 0;
                    }
                    break;
                case CompressionAlgorithm.Deflate:
                case CompressionAlgorithm.GZip:
                case CompressionAlgorithm.ZLib:
                    if (string.IsNullOrEmpty(version) || version == ZLIB_v1_3_1)
                    {
                        result.Version = ZLIB_v1_3_1;
                        result.Index = 1;
                    }
                    break;
                default:
                    throw new ArgumentException($"CompressionAlgorithm unknown {algorithm}", nameof(algorithm));
            }
            if (result.Index == -1)
                throw new ArgumentException($"Compression version \"{version ?? ""}\" is not supported for Algorithm {algorithm}");
            result.Algorithm = algorithm;

            return result;
        }

        public CompressionAlgorithm Algorithm { get; private set; }

        public string Version { get; private set; }

        /// <summary>
        /// Internal id for fast version lookup
        /// </summary>
        internal int Index { get; private set; }
    }
}

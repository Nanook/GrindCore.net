




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
      v1_0_9 = 1,
      Latest = v1_0_9
    }

    public class CompressionVersion
    {
        public static string LATEST = "";
        public static string ZLIB_v1_3_1 = "1.3.1";
        public static string ZLIBNG_v2_2_1 = "2.2.1";
        public static string BROTLI_v1_0_9 = "1.0.9";

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

        private CompressionVersion()
        {
        }

        private static CompressionVersion create(CompressionAlgorithm algorithm, string version)
        {
            CompressionVersion result = new CompressionVersion();
            result.Index = -1;

            switch (algorithm)
            {
                case CompressionAlgorithm.Brotli:
                    if (string.IsNullOrEmpty(version) || version == BROTLI_v1_0_9)
                    {
                        result.Version = BROTLI_v1_0_9;
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

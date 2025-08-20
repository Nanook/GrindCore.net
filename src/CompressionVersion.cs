using System;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Specifies supported versions for the Copy algorithm.
    /// </summary>
    public enum CopyVersion
    {
        v0_0_0 = 1,
        Latest = v0_0_0
    }

    /// <summary>
    /// Specifies supported versions for the ZLib algorithm.
    /// </summary>
    public enum ZLibVersion
    {
        v1_3_1 = 1,
        Latest = v1_3_1
    }

    /// <summary>
    /// Specifies supported versions for the ZLibNg algorithm.
    /// </summary>
    public enum ZLibNgVersion
    {
        v2_2_1 = 1,
        Latest = v2_2_1
    }

    /// <summary>
    /// Specifies supported versions for the Brotli algorithm.
    /// </summary>
    public enum BrotliVersion
    {
        v1_1_0 = 1,
        Latest = v1_1_0
    }

    /// <summary>
    /// Specifies supported versions for the LZMA algorithm.
    /// </summary>
    public enum LzmaVersion
    {
        v25_1_0 = 1,
        Latest = v25_1_0
    }

    /// <summary>
    /// Specifies supported versions for the LZMA2 algorithm.
    /// </summary>
    public enum Lzma2Version
    {
        v25_1_0 = 1,
        Latest = v25_1_0
    }

    /// <summary>
    /// Specifies supported versions for the FastLzma2 algorithm.
    /// </summary>
    public enum FastLzma2Version
    {
        v1_0_1 = 1,
        Latest = v1_0_1
    }

    /// <summary>
    /// Specifies supported versions for the LZ4 algorithm.
    /// </summary>
    public enum Lz4Version
    {
        v1_10_0 = 1,
        Latest = v1_10_0
    }

    /// <summary>
    /// Specifies supported versions for the ZStd algorithm.
    /// </summary>
    public enum ZStdVersion
    {
        v1_5_2 = 1,
        v1_5_7 = 2,
        Latest = v1_5_7
    }

    /// <summary>
    /// Represents a version for a specific compression algorithm.
    /// </summary>
    public class CompressionVersion
    {
        /// <summary>
        /// Gets a string representing the latest version.
        /// </summary>
        public const string LATEST = "";
        public const string COPY_v0_0_0 = "0.0.0";
        public const string ZLIB_v1_3_1 = "1.3.1";
        public const string ZLIBNG_v2_2_1 = "2.2.1";
        public const string BROTLI_v1_1_0 = "1.1.0";
        public const string LZMA_v25_1_0 = "25.1.0";
        public const string LZMA2_v25_1_0 = "25.1.0";
        public const string FASTLZMA2_v1_0_1 = "1.0.1";
        public const string LZ4_v1_10_0 = "1.10.0";
        public const string ZSTD_v1_5_2 = "1.5.2";
        public const string ZSTD_v1_5_7 = "1.5.7";

        /// <summary>
        /// Converts an enum name to a version string (e.g., v1_2_3 to 1.2.3).
        /// </summary>
        /// <param name="enumName">The enum name.</param>
        /// <returns>The version string.</returns>
        private static string enumStringToVersionString(string enumName) => enumName.Replace("v", "").Replace("_", ".");

        /// <summary>
        /// Creates a <see cref="CompressionVersion"/> for the specified algorithm and the latest version.
        /// </summary>
        /// <param name="algorithm">The compression algorithm.</param>
        /// <returns>A <see cref="CompressionVersion"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the algorithm is unknown or not supported.</exception>
        public static CompressionVersion Create(CompressionAlgorithm algorithm)
        {
            return create(algorithm, "");
        }

        /// <summary>
        /// Creates a <see cref="CompressionVersion"/> for the specified algorithm and version string.
        /// </summary>
        /// <param name="algorithm">The compression algorithm.</param>
        /// <param name="version">The version string.</param>
        /// <returns>A <see cref="CompressionVersion"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the algorithm or version is not supported.</exception>
        public static CompressionVersion Create(CompressionAlgorithm algorithm, string? version)
        {
            return create(algorithm, version ?? "");
        }

        /// <summary>
        /// Gets the latest version for the Copy algorithm.
        /// </summary>
        public static CompressionVersion CopyLatest() => Copy(CopyVersion.Latest);

        /// <summary>
        /// Gets a specific version for the Copy algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion Copy(CopyVersion version) => create(CompressionAlgorithm.Copy, enumStringToVersionString(Enum.GetName(typeof(CopyVersion), (int)version)!));

        /// <summary>
        /// Gets the latest version for the Brotli algorithm.
        /// </summary>
        public static CompressionVersion BrotliLatest() => Brotli(BrotliVersion.Latest);

        /// <summary>
        /// Gets a specific version for the Brotli algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion Brotli(BrotliVersion version) => create(CompressionAlgorithm.Brotli, enumStringToVersionString(Enum.GetName(typeof(BrotliVersion), (int)version)!));

        /// <summary>
        /// Gets the latest version for the ZLib algorithm.
        /// </summary>
        public static CompressionVersion ZLibLatest() => ZLib(ZLibVersion.Latest);

        /// <summary>
        /// Gets a specific version for the ZLib algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion ZLib(ZLibVersion version) => create(CompressionAlgorithm.ZLib, enumStringToVersionString(Enum.GetName(typeof(ZLibVersion), (int)version)!));

        /// <summary>
        /// Gets the latest version for the ZLibNg algorithm.
        /// </summary>
        public static CompressionVersion ZLibNgLatest() => ZLibNg(ZLibNgVersion.Latest);

        /// <summary>
        /// Gets a specific version for the ZLibNg algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion ZLibNg(ZLibNgVersion version) => create(CompressionAlgorithm.ZLibNg, enumStringToVersionString(Enum.GetName(typeof(ZLibNgVersion), (int)version)!));

        /// <summary>
        /// Gets the latest version for the LZMA algorithm.
        /// </summary>
        public static CompressionVersion LzmaLatest() => Lzma(LzmaVersion.Latest);

        /// <summary>
        /// Gets a specific version for the LZMA algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion Lzma(LzmaVersion version) => create(CompressionAlgorithm.Lzma, enumStringToVersionString(Enum.GetName(typeof(LzmaVersion), (int)version)!));

        /// <summary>
        /// Gets the latest version for the LZMA2 algorithm.
        /// </summary>
        public static CompressionVersion Lzma2Latest() => Lzma2(Lzma2Version.Latest);

        /// <summary>
        /// Gets a specific version for the LZMA2 algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion Lzma2(Lzma2Version version) => create(CompressionAlgorithm.Lzma2, enumStringToVersionString(Enum.GetName(typeof(Lzma2Version), (int)version)!));

        /// <summary>
        /// Gets the latest version for the FastLzma2 algorithm.
        /// </summary>
        public static CompressionVersion FastLzma2Latest() => FastLzma2(FastLzma2Version.Latest);

        /// <summary>
        /// Gets a specific version for the FastLzma2 algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion FastLzma2(FastLzma2Version version) => create(CompressionAlgorithm.FastLzma2, enumStringToVersionString(Enum.GetName(typeof(FastLzma2Version), (int)version)!));

        /// <summary>
        /// Gets the latest version for the LZ4 algorithm.
        /// </summary>
        public static CompressionVersion Lz4Latest() => Lz4(Lz4Version.Latest);

        /// <summary>
        /// Gets a specific version for the LZ4 algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion Lz4(Lz4Version version) => create(CompressionAlgorithm.Lz4, enumStringToVersionString(Enum.GetName(typeof(Lz4Version), (int)version)!));

        /// <summary>
        /// Gets the latest version for the ZStd algorithm.
        /// </summary>
        public static CompressionVersion ZStdLatest() => ZStd(ZStdVersion.Latest);

        /// <summary>
        /// Gets a specific version for the ZStd algorithm.
        /// </summary>
        /// <param name="version">The version enum value.</param>
        public static CompressionVersion ZStd(ZStdVersion version) => create(CompressionAlgorithm.ZStd, enumStringToVersionString(Enum.GetName(typeof(ZStdVersion), (int)version)!));

        /// <summary>
        /// Private constructor to prevent direct instantiation.
        /// </summary>
        private CompressionVersion()
        {
        }

        /// <summary>
        /// Internal factory for creating a <see cref="CompressionVersion"/> for the specified algorithm and version string.
        /// </summary>
        /// <param name="algorithm">The compression algorithm.</param>
        /// <param name="version">The version string.</param>
        /// <returns>A <see cref="CompressionVersion"/> instance.</returns>
        /// <exception cref="ArgumentException">Thrown if the algorithm or version is not supported.</exception>
        private static CompressionVersion create(CompressionAlgorithm algorithm, string version)
        {
            CompressionVersion result = new CompressionVersion();
            result.Index = -1;

            switch (algorithm)
            {
                case CompressionAlgorithm.Copy:
                    if (string.IsNullOrEmpty(version) || version == COPY_v0_0_0)
                    {
                        result.Version = COPY_v0_0_0;
                        result.Index = 0;
                    }
                    break;
                case CompressionAlgorithm.ZStd:
                    if (!string.IsNullOrEmpty(version) && version == ZSTD_v1_5_2)
                    {
                        result.Version = ZSTD_v1_5_2;
                        result.Index = 1;
                    }
                    else
                    {
                        result.Version = ZSTD_v1_5_7;
                        result.Index = 0; //latest
                    }
                    break;
                case CompressionAlgorithm.Lz4:
                    if (string.IsNullOrEmpty(version) || version == LZ4_v1_10_0)
                    {
                        result.Version = LZ4_v1_10_0;
                        result.Index = 0;
                    }
                    break;
                case CompressionAlgorithm.Lzma:
                    if (string.IsNullOrEmpty(version) || version == LZMA_v25_1_0)
                    {
                        result.Version = LZMA_v25_1_0;
                        result.Index = 0;
                    }
                    break;
                case CompressionAlgorithm.Lzma2:
                    if (string.IsNullOrEmpty(version) || version == LZMA2_v25_1_0)
                    {
                        result.Version = LZMA2_v25_1_0;
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
                        result.Index = 1; //ZLibNg shares the same code as ZLib and uses a different internal version no
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

        /// <summary>
        /// Gets the compression algorithm for this version.
        /// </summary>
        public CompressionAlgorithm Algorithm { get; private set; }

        /// <summary>
        /// Gets the version string for this compression version.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// Internal id for fast version lookup.
        /// </summary>
        internal int Index { get; private set; }
    }
}

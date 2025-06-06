using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Provides default compression levels and version information for a given <see cref="CompressionAlgorithm"/>.
    /// </summary>
    internal class CompressionDefaults
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionDefaults"/> class using the specified algorithm and the latest version.
        /// </summary>
        /// <param name="algorithm">The compression algorithm.</param>
        public CompressionDefaults(CompressionAlgorithm algorithm) : this(algorithm, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionDefaults"/> class using the specified algorithm and version.
        /// </summary>
        /// <param name="algorithm">The compression algorithm.</param>
        /// <param name="version">The compression version, or <c>null</c> to use the latest version.</param>
        public CompressionDefaults(CompressionAlgorithm algorithm, CompressionVersion? version)
        {
            this.LevelFastest = CompressionType.Level1;
            switch (algorithm)
            {
                case CompressionAlgorithm.Copy:
                    this.LevelOptimal = CompressionType.Level1;
                    this.LevelSmallestSize = CompressionType.Level1;
                    this.Version = version ?? CompressionVersion.CopyLatest();
                    break;
                case CompressionAlgorithm.Deflate:
                case CompressionAlgorithm.GZip:
                case CompressionAlgorithm.ZLib:
                    this.LevelOptimal = CompressionType.Level6;
                    this.LevelSmallestSize = CompressionType.Level9;
                    this.Version = version ?? CompressionVersion.ZLibLatest();
                    break;
                case CompressionAlgorithm.DeflateNg:
                case CompressionAlgorithm.GZipNg:
                case CompressionAlgorithm.ZLibNg:
                    this.LevelOptimal = CompressionType.Level6;
                    this.LevelSmallestSize = CompressionType.Level9;
                    this.Version = version ?? CompressionVersion.ZLibNgLatest();
                    break;
                case CompressionAlgorithm.Brotli:
                    this.LevelOptimal = CompressionType.Level4;
                    this.LevelSmallestSize = CompressionType.Level11;
                    this.Version = version ?? CompressionVersion.BrotliLatest();
                    break;
                case CompressionAlgorithm.Lzma:
                    this.LevelOptimal = CompressionType.Level5;
                    this.LevelSmallestSize = CompressionType.Level9;
                    this.Version = version ?? CompressionVersion.LzmaLatest();
                    break;
                case CompressionAlgorithm.Lzma2:
                    this.LevelOptimal = CompressionType.Level5;
                    this.LevelSmallestSize = CompressionType.Level9;
                    this.Version = version ?? CompressionVersion.Lzma2Latest();
                    break;
                case CompressionAlgorithm.FastLzma2:
                    this.LevelOptimal = CompressionType.Level6;
                    this.LevelSmallestSize = CompressionType.Level10;
                    this.Version = version ?? CompressionVersion.FastLzma2Latest();
                    break;
                case CompressionAlgorithm.Lz4:
                    this.LevelOptimal = CompressionType.Level9;
                    this.LevelSmallestSize = CompressionType.Level12;
                    this.Version = version ?? CompressionVersion.Lz4Latest();
                    break;
                case CompressionAlgorithm.ZStd:
                    this.LevelOptimal = CompressionType.Level3;
                    this.LevelSmallestSize = CompressionType.Level22;
                    this.Version = version ?? CompressionVersion.ZStdLatest();
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Gets the recommended fastest compression level for the algorithm.
        /// </summary>
        public CompressionType LevelFastest { get; }

        /// <summary>
        /// Gets the recommended optimal compression level for the algorithm.
        /// </summary>
        public CompressionType LevelOptimal { get; }

        /// <summary>
        /// Gets the recommended smallest size compression level for the algorithm.
        /// </summary>
        public CompressionType LevelSmallestSize { get; }

        /// <summary>
        /// Gets the default or specified compression version for the algorithm.
        /// </summary>
        public CompressionVersion Version { get; }
    }
}

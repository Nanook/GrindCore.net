using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    internal class CompressionDefaults
    {
        public CompressionDefaults(CompressionAlgorithm algorithm) : this(algorithm, null)
        {
        }

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

        public CompressionType LevelFastest { get; }
        public CompressionType LevelOptimal { get; }
        public CompressionType LevelSmallestSize { get; }
        public CompressionVersion Version { get; }
    }
}

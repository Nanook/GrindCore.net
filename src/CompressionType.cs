


namespace Nanook.GrindCore
{
    /// <summary>
    /// Specifies values that indicate decompression or compression level.
    /// </summary>
    public enum CompressionType
    {
        /// <summary>
        /// The compression operation should create output as small as possible, even if the operation takes a longer time to complete.
        /// </summary>
        SmallestSize = -4,

        /// <summary>
        /// The compression operation should complete as quickly as possible, even if the resulting file is not optimally compressed.
        /// </summary>
        Fastest = -3,

        /// <summary>
        /// The compression operation should balance compression speed and output size.
        /// </summary>
        Optimal = -2,

        /// <summary>
        /// Decompress.
        /// </summary>
        Decompress = -1,

        /// <summary>
        /// No compression should be performed on the data, some algorithms seemingly still compress the data.
        /// </summary>
        NoCompression = 0,

        /// Named Levels (not supported by all algorithms).
        Level0 = NoCompression,
        Level1 = 1,
        Level2 = 2,
        Level3 = 3,
        Level4 = 4,
        Level5 = 5,
        Level6 = 6,
        Level7 = 7,
        Level8 = 8,
        Level9 = 9,
        Level10 = 10,
        Level11 = 11,
        Level12 = 12,
        Level13 = 13,
        Level14 = 14,
        Level15 = 15,
        Level16 = 16,
        Level17 = 17,
        Level18 = 18,
        Level19 = 19,
        Level20 = 20,
        Level21 = 21,
        Level22 = 22,
        MaxZLib = Level9,
        MaxLzma = Level9,
        MaxLzma2 = Level9,
        MaxLz4 = Level12,
        MaxFastLzma2 = Level10,
        MaxBrotli = Level11,
        MaxZStd = Level22,
    }

}

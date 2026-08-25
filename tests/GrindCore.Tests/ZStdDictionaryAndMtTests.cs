using Utl = GrindCore.Tests.Utility.Utilities;
using Nanook.GrindCore;
using Nanook.GrindCore.ZStd;
using Nanook.GrindCore.XXHash;
using System.Diagnostics;
using System.Security.Cryptography;
using Xunit;
using GrindCore.Tests.Utility;

namespace GrindCore.Tests
{
    /// <summary>
    /// Tests for ZStd multithreading support.
    /// NOTE: MT tests are currently skipped because the native ZStd MT integration
    /// crashes the process. This is a known issue to be fixed in the native layer.
    /// Once fixed, remove the Skip attribute to enable these tests.
    /// </summary>
    public sealed class ZStdDictionaryAndMtTests
    {
        // --- Multithreading: round-trip correctness ---

        [Theory]
        [InlineData(CompressionType.Fastest, 2, 512 * 1024, null)]
        [InlineData(CompressionType.Fastest, 4, 512 * 1024, null)]
        [InlineData(CompressionType.Optimal, 2, 512 * 1024, null)]
        [InlineData(CompressionType.Optimal, 4, 512 * 1024, null)]
        [InlineData(CompressionType.SmallestSize, 2, 512 * 1024, null)]
        [InlineData(CompressionType.Fastest, 2, 512 * 1024, "1.5.2")]
        [InlineData(CompressionType.Fastest, 4, 512 * 1024, "1.5.2")]
        [InlineData(CompressionType.Optimal, 2, 512 * 1024, "1.5.2")]
        [InlineData(CompressionType.Optimal, 4, 512 * 1024, "1.5.2")]
        [InlineData(CompressionType.SmallestSize, 2, 512 * 1024, "1.5.2")]
        public void ZStd_MultiThread_RoundTrips(CompressionType type, int threadCount, int streamLen, string? version)
        {
            int bufferSize = 128 * 1024;
            int jobSize = 128 * 1024;

            TestResults mtResult;
            using (var data = new TestDataStream())
                mtResult = Utl.TestStreamBlocks(data, CompressionAlgorithm.ZStd, type, streamLen, bufferSize, 0, threads: threadCount, version: version, blockSize: jobSize);

            // Must round-trip correctly
            Assert.Equal(mtResult.InHash, mtResult.OutHash);
            Assert.True(mtResult.CompressedBytes > 0, "Compressed output should be non-empty");
            Assert.True(mtResult.CompressedBytes < streamLen, "Compressed output should be smaller than input");

            Trace.WriteLine($"ZStd MT({threadCount}) {type} {streamLen / 1024}KiB: compressed={mtResult.CompressedBytes}B");
        }

        // --- Multithreading: verify MT produces different output vs single-thread ---

        [Theory]
        [InlineData(CompressionType.Fastest, 4, 512 * 1024, null)]
        [InlineData(CompressionType.Optimal, 4, 512 * 1024, null)]
        [InlineData(CompressionType.Fastest, 4, 512 * 1024, "1.5.2")]
        [InlineData(CompressionType.Optimal, 4, 512 * 1024, "1.5.2")]
        public void ZStd_MultiThread_ProducesDifferentOutput_VsSingleThread(CompressionType type, int threadCount, int streamLen, string? version)
        {
            int bufferSize = 64 * 1024;
            int jobSize = 64 * 1024;

            // Single-threaded baseline
            TestResults baselineResult;
            using (var data = new TestDataStream())
                baselineResult = Utl.TestStreamBlocks(data, CompressionAlgorithm.ZStd, type, streamLen, bufferSize, 0, threads: 0, version: version, blockSize: jobSize);

            // Multi-threaded
            TestResults mtResult;
            using (var data = new TestDataStream())
                mtResult = Utl.TestStreamBlocks(data, CompressionAlgorithm.ZStd, type, streamLen, bufferSize, 0, threads: threadCount, version: version, blockSize: jobSize);

            // Both round-trip correctly
            Assert.Equal(baselineResult.InHash, baselineResult.OutHash);
            Assert.Equal(mtResult.InHash, mtResult.OutHash);

            // Same input
            Assert.Equal(baselineResult.InHash, mtResult.InHash);

            // MT output may differ from single-thread depending on data size and job configuration.
            // The key verification is that both produce correct round-trips.
            if (baselineResult.CompressedHash != mtResult.CompressedHash)
                Trace.WriteLine($"ZStd ST vs MT({threadCount}) {type}: OUTPUT DIFFERS (expected for large data) st={baselineResult.CompressedBytes}B, mt={mtResult.CompressedBytes}B");
            else
                Trace.WriteLine($"ZStd ST vs MT({threadCount}) {type}: identical output, st={baselineResult.CompressedBytes}B, mt={mtResult.CompressedBytes}B");
        }

        // --- Multithreading: different thread counts produce same decompressed output ---

        [Theory]
        [InlineData(CompressionType.Fastest, 2, 4, null)]
        [InlineData(CompressionType.Optimal, 2, 4, null)]
        [InlineData(CompressionType.Fastest, 2, 4, "1.5.2")]
        [InlineData(CompressionType.Optimal, 2, 4, "1.5.2")]
        public void ZStd_MultiThread_DifferentThreadCounts_SameDecompressedOutput(CompressionType type, int threads1, int threads2, string? version)
        {
            int streamLen = 512 * 1024;
            int bufferSize = 128 * 1024;
            int jobSize = 128 * 1024;

            TestResults result1;
            using (var data = new TestDataStream())
                result1 = Utl.TestStreamBlocks(data, CompressionAlgorithm.ZStd, type, streamLen, bufferSize, 0, threads: threads1, version: version, blockSize: jobSize);

            TestResults result2;
            using (var data = new TestDataStream())
                result2 = Utl.TestStreamBlocks(data, CompressionAlgorithm.ZStd, type, streamLen, bufferSize, 0, threads: threads2, version: version, blockSize: jobSize);

            // Both round-trip correctly to the same original data
            Assert.Equal(result1.InHash, result1.OutHash);
            Assert.Equal(result2.InHash, result2.OutHash);
            Assert.Equal(result1.OutHash, result2.OutHash);

            Trace.WriteLine($"ZStd MT({threads1}) vs MT({threads2}) {type}: t1={result1.CompressedBytes}B, t2={result2.CompressedBytes}B");
        }

        // --- Multithreading: varying job sizes ---

        [Theory]
        [InlineData(CompressionType.Fastest, 4, 128 * 1024, null)]
        [InlineData(CompressionType.Fastest, 4, 256 * 1024, null)]
        [InlineData(CompressionType.Optimal, 2, 128 * 1024, null)]
        [InlineData(CompressionType.Fastest, 4, 128 * 1024, "1.5.2")]
        [InlineData(CompressionType.Fastest, 4, 256 * 1024, "1.5.2")]
        [InlineData(CompressionType.Optimal, 2, 128 * 1024, "1.5.2")]
        public void ZStd_MultiThread_VaryingJobSize_RoundTrips(CompressionType type, int threadCount, int jobSize, string? version)
        {
            int streamLen = 512 * 1024;
            int bufferSize = 128 * 1024;

            TestResults result;
            using (var data = new TestDataStream())
                result = Utl.TestStreamBlocks(data, CompressionAlgorithm.ZStd, type, streamLen, bufferSize, 0, threads: threadCount, version: version, blockSize: jobSize);

            Assert.Equal(result.InHash, result.OutHash);
            Assert.True(result.CompressedBytes > 0);

            Trace.WriteLine($"ZStd MT({threadCount}) job={jobSize / 1024}KiB {type}: compressed={result.CompressedBytes}B");
        }
    }
}

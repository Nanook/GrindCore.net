using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HashAlgorithm = System.Security.Cryptography.HashAlgorithm;
using Nanook.GrindCore;
using Nanook.GrindCore.Blake;
using Nanook.GrindCore.MD;
using Nanook.GrindCore.SHA;
using Nanook.GrindCore.XXHash;
using Xunit;
using System.Text;
using GrindCore.Tests.Utility;

#if WIN_X64
namespace GrindCore.Tests
{
    /// <summary>
    /// Memory leak tests to try and identify memory leaks. There's probably a much better approach.
    /// Run something many times, afterwards read the memory usage for the whole process - deduct the total managed heap size.
    /// Repeat this and if the memory drops exit with success. Run up to 10 times
    /// </summary>
    public sealed class MemoryLeakTests
    {
        private static byte[] _data;

        static MemoryLeakTests()
        {
            _data = TestDataStream.Create(64 * 1024);
        }

        /// <summary>
        /// Loop hashes to test for memory leaks.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, DataStreamHashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, DataStreamHashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, DataStreamHashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, DataStreamHashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, DataStreamHashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, DataStreamHashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, DataStreamHashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, DataStreamHashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, DataStreamHashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, DataStreamHashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, DataStreamHashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, DataStreamHashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, DataStreamHashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, DataStreamHashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, DataStreamHashConstants.HashResult64kSHA3_512)]
        public void MemLeak_Hash_ByteArray64k(HashType type, string expectedResult)
        {
            ulong[] total = new ulong[10];
            bool success = false;
            StringBuilder sb = new StringBuilder($"{type}: "); 
            for (int i = 0; i < total.Length; i++)
            {
                ulong initialMemory = getUnmanagedMemoryUsed();

                // Run the hash function 1000 more times
                for (int c = 0; c < 1000; c++)
                {
                    string result = HashFactory.Compute(type, _data).ToHexString();
                    Assert.Equal(expectedResult, result);
                }

                ulong afterGcMemory = getUnmanagedMemoryUsed();

                sb.Append($"{(i == 0 ? "" : ",")} {afterGcMemory / 1024} KB");
                total[i] = afterGcMemory;
                if (i != 0 && afterGcMemory <= total[i - 1])
                {
                    success = true;
                    break;
                }
            }
            Trace.WriteLine(sb.ToString());
            Assert.True(success);
        }

        /// <summary>
        /// Loop byte compression to test for memory leaks.
        /// </summary>
        [Theory]
        [InlineData(CompressionStreamType.Brotli, CompressionLevel.Optimal, 0x19b, "e39f3f4d64825537")]
        [InlineData(CompressionStreamType.Deflate, CompressionLevel.Optimal, 0x3cd, "eefbb1f99743a612")]
        [InlineData(CompressionStreamType.GZip, CompressionLevel.Optimal, 0x3df, "acf7f3fdc709207f")]
        [InlineData(CompressionStreamType.ZLib, CompressionLevel.Optimal, 0x3d3, "d94ee8404fc070f8")]
        public void MemLeak_CompressionStream_ByteArray64k(CompressionStreamType type, CompressionLevel level, int compressedSize, string xxh64)
        {
            ulong[] total = new ulong[10];
            bool success = false;
            StringBuilder sb = new StringBuilder($"{type}: ");
            for (int i = 0; i < total.Length; i++)
            {
                ulong initialMemory = getUnmanagedMemoryUsed();

                // Run the hash function 1000 more times
                for (int c = 0; c < 1000; c++)
                {
                    var compressed = CompressionStreamFactory.Compress(type, _data, level);
                    Assert.Equal(compressedSize, compressed.Length);
                    Assert.Equal(xxh64, XXHash64.Compute(compressed).ToHexString());
                    var decompressed = CompressionStreamFactory.Decompress(type, compressed);
                    Assert.Equal(_data, decompressed);
                }

                ulong afterGcMemory = getUnmanagedMemoryUsed();

                sb.Append($"{(i == 0 ? "" : ",")} {afterGcMemory / 1024} KB");
                total[i] = afterGcMemory;
                if (i != 0 && afterGcMemory <= total[i - 1])
                {
                    success = true;
                    break;
                }
            }
            Trace.WriteLine(sb.ToString());
            Assert.True(success);
        }

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint size);

        [StructLayout(LayoutKind.Sequential, Size = 72)]
        private struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public ulong PeakWorkingSetSize;
            public ulong WorkingSetSize;
            public ulong QuotaPeakPagedPoolUsage;
            public ulong QuotaPagedPoolUsage;
            public ulong QuotaPeakNonPagedPoolUsage;
            public ulong QuotaNonPagedPoolUsage;
            public ulong PagefileUsage;
            public ulong PeakPagefileUsage;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public ushort processorArchitecture;
            public ushort reserved;
            public uint pageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr activeProcessorMask;
            public uint numberOfProcessors;
            public uint processorType;
            public uint allocationGranularity;
            public ushort processorLevel;
            public ushort processorRevision;
        }

        private ulong getUnmanagedMemoryUsed()
        {
            // Perform garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Introduce a short pause
            System.Threading.Thread.Sleep(100); // 100 milliseconds

            // Get total process memory usage
            PROCESS_MEMORY_COUNTERS counters;
            GetProcessMemoryInfo(GetCurrentProcess(), out counters, (uint)Marshal.SizeOf(typeof(PROCESS_MEMORY_COUNTERS)));
            ulong totalMemory = counters.WorkingSetSize;

            // Get .NET runtime memory usage
            long dotNetMemory = GC.GetTotalMemory(forceFullCollection: false);

            // Calculate unmanaged memory usage
            ulong unmanagedMemory = totalMemory - (ulong)dotNetMemory;

            //Debug.WriteLine($"{label}: Total Memory = {totalMemory / 1024} KB, .NET Memory = {dotNetMemory / 1024} KB, Unmanaged Memory = {unmanagedMemory / 1024} KB");
            return unmanagedMemory;
        }

    }
}
#endif
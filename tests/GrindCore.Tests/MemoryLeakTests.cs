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

#if WIN_X64
namespace GrindCore.Tests
{
    /// <summary>
    /// Example tests to demonstrate usage
    /// </summary>
    public sealed class MemoryLeakTests
    {
        private static byte[] _data;

        static MemoryLeakTests()
        {
            _data = Shared.CreateData(64 * 1024);
        }

        /// <summary>
        /// Loop hashes to test for memory leaks.
        /// </summary>
        [Theory]
        [InlineData(HashType.Blake2sp, HashConstants.HashResult64kBlake2sp)]
        [InlineData(HashType.Blake3, HashConstants.HashResult64kBlake3)]
        [InlineData(HashType.XXHash32, HashConstants.HashResult64kXXHash32)]
        [InlineData(HashType.XXHash64, HashConstants.HashResult64kXXHash64)]
        [InlineData(HashType.MD2, HashConstants.HashResult64kMD2)]
        [InlineData(HashType.MD4, HashConstants.HashResult64kMD4)]
        [InlineData(HashType.MD5, HashConstants.HashResult64kMD5)]
        [InlineData(HashType.SHA1, HashConstants.HashResult64kSHA1)]
        [InlineData(HashType.SHA2_256, HashConstants.HashResult64kSHA2_256)]
        [InlineData(HashType.SHA2_384, HashConstants.HashResult64kSHA2_384)]
        [InlineData(HashType.SHA2_512, HashConstants.HashResult64kSHA2_512)]
        [InlineData(HashType.SHA3_224, HashConstants.HashResult64kSHA3_224)]
        [InlineData(HashType.SHA3_256, HashConstants.HashResult64kSHA3_256)]
        [InlineData(HashType.SHA3_384, HashConstants.HashResult64kSHA3_384)]
        [InlineData(HashType.SHA3_512, HashConstants.HashResult64kSHA3_512)]
        public void MemLeak_Hash_ByteArray64k(HashType type, string expectedResult)
        {
            ulong[] total = new ulong[10];
            bool success = false;
            StringBuilder sb = new StringBuilder($"{type}: "); 
            for (int i = 0; i < total.Length; i++)
            {
                ulong initialMemory = 0;
                ulong beforeGcMemory = 0;

                MemoryLeakTest tst = new MemoryLeakTest();

                initialMemory = GetMemoryUsage();

                tst.MemLeak_Hash_ByteArray64k(_data, type, expectedResult, 1000);

                beforeGcMemory = GetMemoryUsage();

                ulong afterGcMemory = GetMemoryUsage();

                sb.Append($"{(i == 0 ? "" : ",")} {afterGcMemory / 1024} KB");
                total[i] = afterGcMemory;
                if (i != 0 && afterGcMemory <= total[i - 1])
                    success = true;
            }
            Trace.WriteLine(sb.ToString());
            Assert.True(success);
        }

        [DllImport("kernel32.dll")]
        public static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll")]
        public static extern IntPtr GetCurrentProcess();

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint size);

        [StructLayout(LayoutKind.Sequential, Size = 72)]
        public struct PROCESS_MEMORY_COUNTERS
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
        public struct SYSTEM_INFO
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

        private ulong GetMemoryUsage()
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
    public sealed class MemoryLeakTest
    {

        public void MemLeak_Hash_ByteArray64k(byte[] data, HashType type, string expectedResult, int count)
        {
            // Run the hash function 1000 more times
            for (int i = 0; i < count; i++)
            {

                byte[] result;
                switch (type)
                {
                    case HashType.Blake2sp:
                        result = Blake2sp.Compute(data);
                        break;
                    case HashType.Blake3:
                        result = Blake3.Compute(data);
                        break;
                    case HashType.XXHash32:
                        result = XXHash32.Compute(data);
                        break;
                    case HashType.XXHash64:
                        result = XXHash64.Compute(data);
                        break;
                    case HashType.MD2:
                        result = MD2.Compute(data);
                        break;
                    case HashType.MD4:
                        result = MD4.Compute(data);
                        break;
                    case HashType.MD5:
                        result = MD5.Compute(data);
                        break;
                    case HashType.SHA1:
                        result = SHA1.Compute(data);
                        break;
                    case HashType.SHA2_256:
                        result = SHA2_256.Compute(data);
                        break;
                    case HashType.SHA2_384:
                        result = SHA2_384.Compute(data);
                        break;
                    case HashType.SHA2_512:
                        result = SHA2_512.Compute(data);
                        break;
                    case HashType.SHA3_224:
                        result = SHA3.Compute(data, 224);
                        break;
                    case HashType.SHA3_256:
                        result = SHA3.Compute(data, 256);
                        break;
                    case HashType.SHA3_384:
                        result = SHA3.Compute(data, 384);
                        break;
                    case HashType.SHA3_512:
                        result = SHA3.Compute(data, 512);
                        break;
                    default:
                        throw new ArgumentException("Unsupported hash type", nameof(type));
                }
                Assert.Equal(expectedResult, result.ToHexString());
            }
        }
    }
}
#endif
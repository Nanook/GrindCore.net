using GrindCore.Tests.Utility;
using Nanook.GrindCore;
using Nanook.NKit.Steps.Shared;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace StressLzma
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // Defaults
            CompressionType level = CompressionType.Optimal;
            int blockSize = 128 * 1024; // 128 KB
            int iterations = 10_000;
            int threads = Math.Max(1, Environment.ProcessorCount);

            // Simple arg parsing: --level|-l, --blockSize|-b, --iterations|-i, --threads|-t
            for (int i = 0; i < args.Length; i++)
            {
                var a = args[i];
                if (a.Equals("--help", StringComparison.OrdinalIgnoreCase) || a.Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    PrintUsage();
                    return;
                }

                if ((a.Equals("--level", StringComparison.OrdinalIgnoreCase) || a.Equals("-l", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    var v = args[++i];
                    if (!TryParseCompressionType(v, out level))
                    {
                        Console.WriteLine($"Invalid level '{v}'.");
                        PrintUsage();
                        return;
                    }
                }
                else if ((a.Equals("--blockSize", StringComparison.OrdinalIgnoreCase) || a.Equals("-b", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    var v = args[++i];
                    if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out blockSize) || blockSize <= 0)
                    {
                        Console.WriteLine($"Invalid blockSize '{v}'. Must be a positive integer.");
                        PrintUsage();
                        return;
                    }
                }
                else if ((a.Equals("--iterations", StringComparison.OrdinalIgnoreCase) || a.Equals("-i", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    var v = args[++i];
                    if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out iterations) || iterations <= 0)
                    {
                        Console.WriteLine($"Invalid iterations '{v}'. Must be a positive integer.");
                        PrintUsage();
                        return;
                    }
                }
                else if ((a.Equals("--threads", StringComparison.OrdinalIgnoreCase) || a.Equals("-t", StringComparison.OrdinalIgnoreCase)) && i + 1 < args.Length)
                {
                    var v = args[++i];
                    if (!int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out threads) || threads <= 0)
                    {
                        Console.WriteLine($"Invalid threads '{v}'. Must be a positive integer.");
                        PrintUsage();
                        return;
                    }
                }
                else
                {
                    // ignore unknown single args
                }
            }

            Console.WriteLine($"Starting stress test: Level={level}, BlockSize={blockSize}, Iterations={iterations}, Threads={threads}");
            var tester = new StressLzma(level, blockSize, threads);

            try
            {
                tester.Test(iterations);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception: {ex}");
            }

            Console.WriteLine("Done.");
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: StressLzma [--level <level>] [--blockSize <bytes>] [--iterations <n>] [--threads <n>]");
            Console.WriteLine("Options:");
            Console.WriteLine("  --level, -l       Compression level (name or numeric). Examples: Optimal, Fastest, SmallestSize, NoCompression, Level0..Level9");
            Console.WriteLine("  --blockSize, -b   Block size in bytes (positive integer). Default: 131072");
            Console.WriteLine("  --iterations, -i  Number of compress iterations to run. Default: 10000");
            Console.WriteLine("  --threads, -t     Worker thread count to use with CircularSequenceQueue. Default: Environment.ProcessorCount");
            Console.WriteLine("  --help, -h        Show this help");
        }

        static bool TryParseCompressionType(string s, out CompressionType result)
        {
            result = CompressionType.Optimal;
            if (Enum.TryParse<CompressionType>(s, true, out var parsed))
            {
                result = parsed;
                return true;
            }

            // allow numeric values
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
            {
                try
                {
                    result = (CompressionType)numeric;
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }

    internal class StressLzma
    {
        private readonly byte[] _data;
        private readonly byte[] _none;
        private readonly Random _rnd = new Random();
        private readonly CircularSequenceQueue<WorkItem> _threads;
        private long _totalSrcBytes;
        private long _totalDstBytes;
        private int _success;
        private int _failures;
        private readonly int _blockSize;
        private readonly CompressionAlgorithm _algo = CompressionAlgorithm.Lzma;
        private readonly CompressionType _level;
        private readonly int _threadCount;
        private readonly WorkItem[] _poolReference; // keep reference to dispose compressors later

        // WorkItem used by CircularSequenceQueue
        private class WorkItem
        {
            public byte[]? SrcBuf;
            public byte[]? DstBuf;
            public int SrcLen;
            public int DstLen;
            public int Iteration;
            public CompressionResultCode Result;
            public Exception? Exception;
            public CompressionBlock? Compressor;
        }

        public StressLzma(CompressionType level, int blockSize, int threadCount)
        {
            _blockSize = blockSize;
            _data = TestDataStream.Create(blockSize);
            _none = TestNonCompressibleDataStream.Create(blockSize);
            _level = level;
            _threadCount = Math.Max(1, threadCount);

            // Build pool size similar to ConvertWiiGcRvzStep: threadCount + 2 (one fill, one writer)
            int poolSize = Math.Max(2, _threadCount) + 2;
            WorkItem[] pool = new WorkItem[poolSize];
            _poolReference = pool;
            // create compressors per pool item to avoid concurrent use of a single compressor instance
            for (int i = 0; i < pool.Length; i++)
            {
                var options = new CompressionOptions() { Type = _level, BlockSize = _blockSize };
                var compressor = CompressionBlockFactory.Create(_algo, options);
                int dstSize = Math.Max(compressor.RequiredCompressOutputSize, _blockSize + (_blockSize >> 1) + 0x10);
                pool[i] = new WorkItem()
                {
                    SrcBuf = null,
                    DstBuf = new byte[dstSize],
                    SrcLen = 0,
                    DstLen = dstSize,
                    Iteration = -1,
                    Result = CompressionResultCode.Error,
                    Exception = null,
                    Compressor = compressor
                };
            }

            // processItem: runs on threadpool - perform compression using the per-item compressor
            void process(WorkItem w)
            {
                try
                {
                    int outSize = w.DstBuf!.Length;
                    var rc = w.Compressor!.Compress(w.SrcBuf ?? _data, 0, w.SrcLen, w.DstBuf, 0, ref outSize);
                    w.DstLen = outSize;
                    w.Result = rc;
                }
                catch (Exception ex)
                {
                    w.Exception = ex;
                    w.Result = CompressionResultCode.Error;
                }
            }

            // completeItem: called in sequence when the slot is the next one; update counters/stats
            void complete(WorkItem w)
            {
                if (w.Exception != null)
                {
                    Interlocked.Increment(ref _failures);
                    Console.WriteLine($"Iteration {w.Iteration}: Exception -> {w.Exception.GetType()}: {w.Exception.Message}");
                }
                else if (w.Result != CompressionResultCode.Success)
                {
                    Interlocked.Increment(ref _failures);
                    Console.WriteLine($"Iteration {w.Iteration}: Compress failed -> {w.Result}");
                }
                else
                {
                    Interlocked.Increment(ref _success);
                    Interlocked.Add(ref _totalSrcBytes, w.SrcLen);
                    Interlocked.Add(ref _totalDstBytes, w.DstLen);
                }

                // Reset WorkItem for reuse; keep DstBuf & Compressor allocated for reuse
                w.SrcBuf = null;
                w.SrcLen = 0;
                w.DstLen = w.DstBuf!.Length;
                w.Exception = null;
                w.Result = CompressionResultCode.Error;
                w.Iteration = -1;
            }

            _threads = new CircularSequenceQueue<WorkItem>(pool, process, complete);
        }

        /// <summary>
        /// Run compressions. Chooses the non-compressible buffer (_none) 3 out of 5 times,
        /// otherwise chooses the compressible buffer (_data). Uses CircularSequenceQueue to set work and process in parallel.
        /// </summary>
        /// <param name="iterations">Number of compress operations to perform.</param>
        public void Test(int iterations)
        {
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < iterations; i++)
            {
                bool chooseNone = _rnd.Next(5) < 3; // 3 out of 5 chance
                byte[] src = chooseNone ? _none : _data;

                // Get the fill slot, populate and queue it.
                var fill = _threads.FillItem;
                fill.SrcBuf = src;
                fill.SrcLen = src.Length;
                fill.Iteration = i;
                fill.DstLen = fill.DstBuf!.Length;

                _threads.ItemComplete();

                // occasional progress output
                if ((i + 1) % 1000 == 0)
                {
                    double avgRatio = Interlocked.Read(ref _totalSrcBytes) == 0 ? 0 : (double)Interlocked.Read(ref _totalDstBytes) / Interlocked.Read(ref _totalSrcBytes);
                    Console.WriteLine($"Enqueued {i + 1}/{iterations}: success={_success}, failures={_failures}, avgDst/src={avgRatio:F3}, elapsed={sw.Elapsed}");
                }
            }

            // Wait for all queued items to finish
            _threads.Complete();

            sw.Stop();
            Console.WriteLine($"Test complete: iterations={iterations}, success={_success}, failures={_failures}, elapsed={sw.Elapsed}");
            if (_totalSrcBytes > 0)
                Console.WriteLine($"Overall average compressed size ratio: {(double)_totalDstBytes / _totalSrcBytes:F3}");

            // Dispose per-item compressors
            foreach (var wi in _poolReference)
            {
                try { wi.Compressor?.Dispose(); } catch { }
            }
        }
    }
}

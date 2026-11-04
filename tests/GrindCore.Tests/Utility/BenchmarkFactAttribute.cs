using System;

namespace GrindCore.Tests.Utility
{
    /// <summary>
    /// A [Fact] that runs only when the GRINDCORE_BENCHMARKS environment variable is 1. It's for measurements, which take
    /// time and memory that a correctness run doesn't need. Otherwise the test is reported as skipped, with how to run it.
    /// </summary>
    public sealed class BenchmarkFactAttribute : FactAttribute
    {
        public const string Variable = "GRINDCORE_BENCHMARKS";

        public BenchmarkFactAttribute()
        {
            if (Environment.GetEnvironmentVariable(Variable) != "1")
                Skip = $"A benchmark: set {Variable}=1 to run it";
        }
    }
}

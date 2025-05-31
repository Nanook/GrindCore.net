using System.Collections.Generic;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.FastLzma2
{
    public class CompressionParameters
    {
        internal readonly Dictionary<FL2Parameter, int?> Values = new Dictionary<FL2Parameter, int?>();

        public CompressionParameters(int threads, int dictionarySize = 0)
        {
            this.Threads = threads;
            if (dictionarySize != 0)
                this.DictionarySize = dictionarySize;
        }

        private int? getValue(FL2Parameter parameter)
        {
            return Values.ContainsKey(parameter) ? Values[parameter] : null;
        }

        private void setValue(FL2Parameter parameter, int? value)
        {
            if (value == null)
                Values.Remove(parameter);
            else
                Values[parameter] = value;
        }

        /// <summary>
        /// Thread Count, auto = 0
        /// </summary>
        public int Threads { get; set; }

        /// <summary>
        /// Update all compression parameters according to pre-defined cLevel table
        /// Process Level [1..10], Default level is FL2_CLEVEL_DEFAULT==6.
        /// Setting FL2_p_highCompression to 1 switches to an alternate cLevel table.
        /// </summary>
        public int? CompressionLevel
        {
            get => getValue(FL2Parameter.CompressionLevel);
            set => setValue(FL2Parameter.CompressionLevel, value);
        }

        /// <summary>
        /// Maximize compression ratio for a given dictionary size.Levels 1..10 = dictionaryLog 20..29 (1 Mb..512 Mb).
        /// Typically provides a poor speed/ratio tradeoff.
        /// </summary>
        public int? HighCompression
        {
            get => getValue(FL2Parameter.HighCompression);
            set => setValue(FL2Parameter.HighCompression, value);
        }

        /// <summary>
        /// Maximum allowed back-reference distance, expressed as power of 2.
        /// Must be clamped between FL2_DICTLOG_MIN and FL2_DICTLOG_MAX.
        /// Default = 24
        /// </summary>
        public int? DictionaryLog
        {
            get => getValue(FL2Parameter.DictionaryLog);
            set => setValue(FL2Parameter.DictionaryLog, value);
        }

        /// <summary>
        /// Same as above but expressed as an absolute value.
        /// Must be clamped between FL2_DICTSIZE_MIN and FL2_DICTSIZE_MAX.
        /// Default = 16 Mb
        /// </summary>
        public int DictionarySize
        {
            get => getValue(FL2Parameter.DictionarySize) ?? 64 * 1024 * 1024;
            set => setValue(FL2Parameter.DictionarySize, value);
        }

        /// <summary>
        /// The radix match finder is block-based, so some overlap is retained from each block to improve compression of the next.
        /// This value is expressed as n / 16 of the block size (dictionary size). Larger values are slower.
        /// Values above 2 mostly yield only a small improvement in compression.
        /// A large value for a small dictionary may worsen multithreaded compression.
        /// Default = 2
        /// </summary>
        public int? OverlapFraction
        {
            get => getValue(FL2Parameter.OverlapFraction);
            set => setValue(FL2Parameter.OverlapFraction, value);
        }

        /// <summary>
        /// For multithreaded decompression. A dictionary reset will occur
        /// after each dictionarySize * resetInterval bytes of input.
        /// Default = 4
        /// </summary>
        public int? ResetInterval
        {
            get => getValue(FL2Parameter.ResetInterval);
            set => setValue(FL2Parameter.ResetInterval, value);
        }

        /// <summary>
        /// Buffering speeds up the matchfinder. Buffer resize determines the percentage of
        /// the normal _outBuffer size used, which depends on dictionary size.
        /// 0=50, 1=75, 2=100, 3=150, 4=200. Higher number = slower, better
        /// compression, higher memory usage. A CPU with a large memory cache
        /// may make effective use of a larger _outBuffer.
        /// Default = 2
        /// </summary>
        public int? BufferResize
        {
            get => getValue(FL2Parameter.BufferResize);
            set => setValue(FL2Parameter.BufferResize, value);
        }

        /// <summary>
        /// Size of the hybrid mode HC3 hash chain, as a power of 2.
        /// Resulting table size is (1 << (chainLog+2)) bytes.
        /// Larger tables result in better and slower compression.
        /// This parameter is only used by the hybrid "ultra" strategy.
        /// Default = 9
        /// </summary>
        public int? HybridChainLog
        {
            get => getValue(FL2Parameter.HybridChainLog);
            set => setValue(FL2Parameter.HybridChainLog, value);
        }

        /// <summary>
        /// Number of search attempts made by the HC3 match finder.
        /// Used only by the hybrid "ultra" strategy.
        /// More attempts result in slightly better and slower compression.
        /// Default = 1
        /// </summary>
        public int? HybridCycles
        {
            get => getValue(FL2Parameter.HybridCycles);
            set => setValue(FL2Parameter.HybridCycles, value);
        }

        /// <summary>
        /// Match finder will resolve string matches up to this length.
        /// If a longer match exists further back in the input, it will not be found.
        /// Default = 42
        /// </summary>
        public int? SearchDepth
        {
            get => getValue(FL2Parameter.SearchDepth);
            set => setValue(FL2Parameter.SearchDepth, value);
        }

        /// <summary>
        /// Only useful for strategies >= opt.
        /// Length of match considered "good enough" to stop search.
        /// Larger values make compression stronger and slower.
        /// Default = 48
        /// </summary>
        public int? FastLength
        {
            get => getValue(FL2Parameter.FastLength);
            set => setValue(FL2Parameter.FastLength, value);
        }

        /// <summary>
        /// Split long chains of 2-byte matches into shorter chains with a small overlap for further processing.
        /// Allows buffering of all chains at length 2.
        /// Faster, less compression. Generally a good tradeoff.
        /// Default = enabled
        /// </summary>
        public int? DivideAndConquer
        {
            get => getValue(FL2Parameter.DivideAndConquer);
            set => setValue(FL2Parameter.DivideAndConquer, value);
        }

        /// <summary>
        /// 1 = fast; 2 = optimized, 3 = ultra (hybrid mode).
        /// The higher the value of the selected strategy, the more complex it is,
        /// resulting in stronger and slower compression.
        /// Default = ultra
        /// </summary>
        public int? Strategy
        {
            get => getValue(FL2Parameter.Strategy);
            set => setValue(FL2Parameter.Strategy, value);
        }

        /// <summary>
        /// lc value for LZMA2 encoder
        /// Default = 3
        /// </summary>
        public int? LiteralCtxBits
        {
            get => getValue(FL2Parameter.LiteralCtxBits);
            set => setValue(FL2Parameter.LiteralCtxBits, value);
        }

        /// <summary>
        /// lp value for LZMA2 encoder
        /// Default = 0
        /// </summary>
        public int? LiteralPosBits
        {
            get => getValue(FL2Parameter.LiteralPosBits);
            set => setValue(FL2Parameter.LiteralPosBits, value);
        }

        /// <summary>
        /// pb value for LZMA2 encoder
        /// Default = 2
        /// </summary>
        public int? PosBits
        {
            get => getValue(FL2Parameter.posBits);
            set => setValue(FL2Parameter.posBits, value);
        }

        /// <summary>
        /// Omit the property byte at the start of the stream. For use within 7-zip
        /// or other containers which store the property byte elsewhere.
        /// A stream compressed under this setting cannot be decoded by this library.
        /// </summary>
        public int? Properties
        {
            get => getValue(FL2Parameter.Properties);
            set => setValue(FL2Parameter.Properties, value);
        }

        /// <summary>
        /// Calculate a 32-bit xxhash value from the input data and store it
        /// after the stream terminator. The value will be checked on decompression.
        /// 0 = do not calculate; 1 = calculate (default)
        /// </summary>
        public int? DoXXHash
        {
            get => getValue(FL2Parameter.DoXXHash);
            set => setValue(FL2Parameter.DoXXHash, value);
        }

        /// <summary>
        /// Use the reference matchfinder for development purposes. SLOW.
        /// </summary>
        public int? UseReferenceMF
        {
            get => getValue(FL2Parameter.UseReferenceMF);
            set => setValue(FL2Parameter.UseReferenceMF, value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal FL2CompressionParameters ToParams()
        {
            return new FL2CompressionParameters
            {
                DictionarySize = (nuint)this.DictionarySize,
                OverlapFraction = (uint)(this.OverlapFraction ?? 0),
                ChainLog = (uint)(this.HybridChainLog ?? 0),
                CyclesLog = (uint)(this.HybridCycles ?? 0),
                SearchDepth = (uint)(this.SearchDepth ?? 0),
                FastLength = (uint)(this.FastLength ?? 0),
                DivideAndConquer = (uint)(this.DivideAndConquer ?? 0),
                Strategy = (FL2Strategy)(this.Strategy ?? 0)
            };
        }
    }


    internal enum FL2Strategy
    {
        Fast,
        Opt,
        Ultra
    }

}
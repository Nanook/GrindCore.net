using System.Collections.Generic;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.FastLzma2
{
    /// <summary>
    /// Represents a set of parameters for configuring Fast-LZMA2 compression.
    /// </summary>
    public class CompressionParameters
    {
        /// <summary>
        /// Gets the dictionary of parameter values for Fast-LZMA2 compression.
        /// </summary>
        internal readonly Dictionary<FL2Parameter, int?> _values = new Dictionary<FL2Parameter, int?>();

        /// <summary>
        /// Initializes a new instance of the <see cref="CompressionParameters"/> class with the specified thread count and optional dictionary size.
        /// </summary>
        /// <param name="threads">The number of threads to use for compression.</param>
        /// <param name="dictionarySize">The dictionary size in bytes. If zero, the default is used.</param>
        public CompressionParameters(int threads, int dictionarySize = 0)
        {
            this.Threads = threads;
            if (dictionarySize != 0)
                this.DictionarySize = dictionarySize;
        }

        private int? getValue(FL2Parameter parameter)
        {
            return _values.ContainsKey(parameter) ? _values[parameter] : null;
        }

        private void setValue(FL2Parameter parameter, int? value)
        {
            if (value == null)
                _values.Remove(parameter);
            else
                _values[parameter] = value;
        }

        /// <summary>
        /// Gets or sets the thread count. Use 0 for auto.
        /// </summary>
        public int Threads { get; set; }

        /// <summary>
        /// Gets or sets the compression level [1..10]. Default is 6.
        /// Setting <see cref="HighCompression"/> to 1 switches to an alternate cLevel table.
        /// </summary>
        public int? CompressionLevel
        {
            get => getValue(FL2Parameter.CompressionLevel);
            set => setValue(FL2Parameter.CompressionLevel, value);
        }

        /// <summary>
        /// Gets or sets the high compression mode. Levels 1..10. Typically provides a poor speed/ratio tradeoff.
        /// </summary>
        public int? HighCompression
        {
            get => getValue(FL2Parameter.HighCompression);
            set => setValue(FL2Parameter.HighCompression, value);
        }

        /// <summary>
        /// Gets or sets the maximum allowed back-reference distance, as a power of 2. Default is 24.
        /// </summary>
        public int? DictionaryLog
        {
            get => getValue(FL2Parameter.DictionaryLog);
            set => setValue(FL2Parameter.DictionaryLog, value);
        }

        /// <summary>
        /// Gets or sets the dictionary size in bytes. Default is 64 MiB.
        /// </summary>
        public int DictionarySize
        {
            get => getValue(FL2Parameter.DictionarySize) ?? 64 * 1024 * 1024;
            set => setValue(FL2Parameter.DictionarySize, value);
        }

        /// <summary>
        /// Gets or sets the overlap fraction (n/16 of the block size). Default is 2.
        /// </summary>
        public int? OverlapFraction
        {
            get => getValue(FL2Parameter.OverlapFraction);
            set => setValue(FL2Parameter.OverlapFraction, value);
        }

        /// <summary>
        /// Gets or sets the reset interval for multithreaded decompression. Default is 4.
        /// </summary>
        public int? ResetInterval
        {
            get => getValue(FL2Parameter.ResetInterval);
            set => setValue(FL2Parameter.ResetInterval, value);
        }

        /// <summary>
        /// Gets or sets the buffer resize factor. 0=50%, 1=75%, 2=100%, 3=150%, 4=200%. Default is 2.
        /// </summary>
        public int? BufferResize
        {
            get => getValue(FL2Parameter.BufferResize);
            set => setValue(FL2Parameter.BufferResize, value);
        }

        /// <summary>
        /// Gets or sets the hybrid mode HC3 hash chain log2 size. Used only by the hybrid "ultra" strategy. Default is 9.
        /// </summary>
        public int? HybridChainLog
        {
            get => getValue(FL2Parameter.HybridChainLog);
            set => setValue(FL2Parameter.HybridChainLog, value);
        }

        /// <summary>
        /// Gets or sets the number of search attempts for the HC3 match finder. Used only by the hybrid "ultra" strategy. Default is 1.
        /// </summary>
        public int? HybridCycles
        {
            get => getValue(FL2Parameter.HybridCycles);
            set => setValue(FL2Parameter.HybridCycles, value);
        }

        /// <summary>
        /// Gets or sets the maximum match finder search depth. Default is 42.
        /// </summary>
        public int? SearchDepth
        {
            get => getValue(FL2Parameter.SearchDepth);
            set => setValue(FL2Parameter.SearchDepth, value);
        }

        /// <summary>
        /// Gets or sets the "good enough" match length for search. Default is 48.
        /// </summary>
        public int? FastLength
        {
            get => getValue(FL2Parameter.FastLength);
            set => setValue(FL2Parameter.FastLength, value);
        }

        /// <summary>
        /// Gets or sets whether to split long chains of 2-byte matches. Default is enabled.
        /// </summary>
        public int? DivideAndConquer
        {
            get => getValue(FL2Parameter.DivideAndConquer);
            set => setValue(FL2Parameter.DivideAndConquer, value);
        }

        /// <summary>
        /// Gets or sets the compression strategy: 1=fast, 2=optimized, 3=ultra (hybrid mode). Default is ultra.
        /// </summary>
        public int? Strategy
        {
            get => getValue(FL2Parameter.Strategy);
            set => setValue(FL2Parameter.Strategy, value);
        }

        /// <summary>
        /// Gets or sets the lc value for LZMA2 encoder. Default is 3.
        /// </summary>
        public int? LiteralCtxBits
        {
            get => getValue(FL2Parameter.LiteralCtxBits);
            set => setValue(FL2Parameter.LiteralCtxBits, value);
        }

        /// <summary>
        /// Gets or sets the lp value for LZMA2 encoder. Default is 0.
        /// </summary>
        public int? LiteralPosBits
        {
            get => getValue(FL2Parameter.LiteralPosBits);
            set => setValue(FL2Parameter.LiteralPosBits, value);
        }

        /// <summary>
        /// Gets or sets the pb value for LZMA2 encoder. Default is 2.
        /// </summary>
        public int? PosBits
        {
            get => getValue(FL2Parameter.posBits);
            set => setValue(FL2Parameter.posBits, value);
        }

        /// <summary>
        /// Gets or sets whether to omit the property byte at the start of the stream.
        /// </summary>
        public int? Properties
        {
            get => getValue(FL2Parameter.Properties);
            set => setValue(FL2Parameter.Properties, value);
        }

        /// <summary>
        /// Gets or sets whether to calculate a 32-bit xxhash value from the input data and store it after the stream terminator. Default is 1.
        /// </summary>
        public int? DoXXHash
        {
            get => getValue(FL2Parameter.DoXXHash);
            set => setValue(FL2Parameter.DoXXHash, value);
        }

        /// <summary>
        /// Gets or sets whether to use the reference matchfinder for development purposes. SLOW.
        /// </summary>
        public int? UseReferenceMF
        {
            get => getValue(FL2Parameter.UseReferenceMF);
            set => setValue(FL2Parameter.UseReferenceMF, value);
        }

        /// <summary>
        /// Converts the current <see cref="CompressionParameters"/> to a native <see cref="FL2CompressionParameters"/> struct.
        /// </summary>
        /// <returns>A <see cref="FL2CompressionParameters"/> struct with the current settings.</returns>
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

    /// <summary>
    /// Specifies the encoder strategy for Fast-LZMA2.
    /// </summary>
    internal enum FL2Strategy
    {
        /// <summary>
        /// Fast strategy.
        /// </summary>
        Fast,
        /// <summary>
        /// Optimized strategy.
        /// </summary>
        Opt,
        /// <summary>
        /// Ultra (hybrid) strategy.
        /// </summary>
        Ultra
    }
}

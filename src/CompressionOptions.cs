using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Represents options that apply to all compression streams and blocks.
    /// </summary>
    public class CompressionOptions
    {
        /// <summary>
        /// Gets or sets the compression type. Can be <see cref="CompressionType.Decompress"/> or a compression level.
        /// </summary>
        public CompressionType Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the base stream should be left open after the compression stream is disposed. Defaults to <c>false</c>.
        /// </summary>
        public bool LeaveOpen { get; set; }

        /// <summary>
        /// Gets or sets the version of the compression algorithm to use (e.g., ZLib supports v1.3.1 and Ng 2.2.1).
        /// </summary>
        public CompressionVersion? Version { get; set; }

        /// <summary>
        /// Gets or sets the thread count for supported algorithms.
        /// </summary>
        public int? ThreadCount { get; set; }

        /// <summary>
        /// Gets or sets the block size. For LZMA2, -1 will compress in full solid mode. 
        /// If threads &gt; 1 and BlockSize != -1, then the block is divided by the number of threads and processed in subblocks.
        /// If not -1, this will override ProcessSizeMin and BufferSize.
        /// </summary>
        public long? BlockSize { get; set; }

        /// <summary>
        /// Gets or sets the write limit during compression and the read limit during decompression. 
        /// This corresponds to the Position property of CompressionStream.If null, no limit is applied.
        /// </summary>
        public long? PositionLimit { get; set; }

        /// <summary>
        /// Gets or sets the buffer read limit during compression and the buffer write limit during decompression. 
        /// This corresponds to the PositionFullSize property of CompressionStream. If null, no limit is applied.
        /// </summary>
        public long? PositionFullSizeLimit { get; set; }

        /// <summary>
        /// Gets or sets the properties required for processing (e.g., LZMA/2 requires these for decoding; they can be read from the encoder and stored).
        /// </summary>
        public byte[]? InitProperties { get; set; }

        /// <summary>
        /// Gets or sets the buffer size. Compression/Decompression will be performed when the internal output buffer is at least this size.
        /// Useful when using <see cref="System.IO.Stream.WriteByte(byte)"/> etc. This is the maximum size of the output buffer and will be used where possible.
        /// </summary>
        public int? BufferSize { get; set; }

        /// <summary>
        /// Gets or sets dictionary and advanced compression options for algorithms that support fine-tuning.
        /// </summary>
        public CompressionDictionaryOptions? Dictionary { get; set; }

        /// <summary>
        /// Returns a <see cref="CompressionOptions"/> instance configured for decompression.
        /// </summary>
        public static CompressionOptions DefaultDecompress() => new CompressionOptions() { Type = CompressionType.Decompress };

        /// <summary>
        /// Returns a <see cref="CompressionOptions"/> instance configured for optimal compression.
        /// </summary>
        public static CompressionOptions DefaultCompressOptimal() => new CompressionOptions() { Type = CompressionType.Optimal };

        // === INSTANCE METHODS FOR SETTING DICTIONARY OPTIONS ===

        /// <summary>
        /// Configures this instance with LZMA dictionary settings.
        /// </summary>
        /// <param name="dictionarySize">Dictionary size in bytes (default: 16MB).</param>
        /// <param name="fastBytes">Fast bytes lookahead (default: 32).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithLzmaDictionary(long? dictionarySize = null, int? fastBytes = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                DictionarySize = dictionarySize ?? (1L << 24), // 16MB
                FastBytes = fastBytes ?? 32,
                LiteralContextBits = 3,
                LiteralPositionBits = 0,
                PositionBits = 2,
                Algorithm = 1, // Normal algorithm
                BinaryTreeMode = 1, // Binary tree mode
                HashBytes = 4
            };
            return this;
        }

        /// <summary>
        /// Configures this instance with maximum LZMA dictionary settings for best compression.
        /// </summary>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithLzmaMaxDictionary()
        {
            Dictionary = new CompressionDictionaryOptions
            {
                DictionarySize = 1L << 26, // 64MB
                FastBytes = 273, // Maximum
                LiteralContextBits = 8, // Maximum for text
                LiteralPositionBits = 4, // Maximum
                PositionBits = 4, // Maximum
                Algorithm = 1,
                BinaryTreeMode = 1,
                HashBytes = 4,
                MatchCycles = 256
            };
            return this;
        }

        /// <summary>
        /// Configures this instance with fast LZMA dictionary settings for speed.
        /// </summary>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithLzmaFastDictionary()
        {
            Dictionary = new CompressionDictionaryOptions
            {
                DictionarySize = 1L << 20, // 1MB
                FastBytes = 16, // Lower for speed
                LiteralContextBits = 3,
                LiteralPositionBits = 0,
                PositionBits = 2,
                Algorithm = 0, // Fast algorithm
                BinaryTreeMode = 0, // Hash chain for speed
                HashBytes = 2 // Fewer hash bytes for speed
            };
            return this;
        }

        /// <summary>
        /// Configures this instance with LZMA2 dictionary settings.
        /// Tailored for LZMA2 usage (sensible dictionary, fast-bytes and block behaviour).
        /// Sets BlockSize to -1 (solid) by default to match typical 7-Zip settings; caller can override ThreadCount/BlockSize afterwards.
        /// </summary>
        /// <param name="dictionarySize">Dictionary size in bytes (default: 16MB).</param>
        /// <param name="fastBytes">Fast bytes lookahead (default: 64).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithLzma2Dictionary(long? dictionarySize = null, int? fastBytes = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                DictionarySize = dictionarySize ?? (1L << 24), // 16MB
                FastBytes = fastBytes ?? 64, // Higher for better compression in MT or LZMA2
                LiteralContextBits = 3,
                LiteralPositionBits = 0,
                PositionBits = 2,
                Algorithm = 1,
                BinaryTreeMode = 1,
                HashBytes = 4
            };

            // LZMA2 tends to use solid (blockSize = -1) for best compression by default.
            // If the caller wants multi-threaded block-mode, they can set ThreadCount and BlockSize after calling this helper.
            this.BlockSize = -1;

            return this;
        }

        /// <summary>
        /// Configures this instance with maximum LZMA2 dictionary settings for best compression.
        /// Sets BlockSize to -1 (solid) so the encoder operates in solid mode by default.
        /// </summary>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithLzma2MaxDictionary()
        {
            Dictionary = new CompressionDictionaryOptions
            {
                DictionarySize = 1L << 26, // 64MB
                FastBytes = 273,
                LiteralContextBits = 8,
                LiteralPositionBits = 4,
                PositionBits = 4,
                Algorithm = 1,
                BinaryTreeMode = 1,
                HashBytes = 4,
                MatchCycles = 512
            };

            // Default to solid blocks for maximum compression; callers can set BlockSize/ThreadCount to enable MT block mode.
            this.BlockSize = -1;

            return this;
        }

        /// <summary>
        /// Configures this instance with ZStd window settings.
        /// </summary>
        /// <param name="windowBits">Window size as power of 2 (default: 23 = 8MB).</param>
        /// <param name="strategy">Compression strategy (default: 7 = btopt).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithZStdWindow(int? windowBits = null, int? strategy = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                WindowBits = windowBits ?? 23, // 8MB window
                Strategy = strategy ?? 7, // btopt strategy
                HashLog = 12, // Reasonable hash table size
                ChainLog = 15, // Reasonable chain table size
                SearchLog = 8 // Moderate search depth
            };
            return this;
        }

        /// <summary>
        /// Configures this instance with fast ZStd window settings for speed.
        /// </summary>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithZStdFastWindow()
        {
            Dictionary = new CompressionDictionaryOptions
            {
                WindowBits = 20, // 1MB window for speed
                Strategy = 1, // Fast strategy
                HashLog = 10, // Smaller hash table
                ChainLog = 12, // Smaller chain table
                SearchLog = 6 // Shorter search
            };
            return this;
        }

        /// <summary>
        /// Configures this instance with Brotli window settings.
        /// </summary>
        /// <param name="windowBits">Window size as power of 2, 10-24 (default: 22 = 4MB).</param>
        /// <param name="quality">Brotli quality level, 0-11 (default: 6).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithBrotliWindow(int? windowBits = null, int? quality = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                WindowBits = windowBits ?? 22, // 4MB window
                Quality = quality ?? 6,
                Strategy = quality ?? 6 // Map to Brotli quality
            };
            return this;
        }

        /// <summary>
        /// Configures this instance with maximum Brotli window settings for best compression.
        /// </summary>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithBrotliMaxWindow()
        {
            Dictionary = new CompressionDictionaryOptions
            {
                WindowBits = 24, // Maximum 16MB window
                Quality = 11, // Maximum quality
                Strategy = 11
            };
            return this;
        }

        /// <summary>
        /// Configure ZLib (zlib header) mode with optional memory level and strategy.
        /// Streams/blocks choose the window bits for zlib header form.
        /// </summary>
        /// <param name="memoryLevel">Memory level 1..9 (defaults to zlib default).</param>
        /// <param name="strategy">Compression strategy (defaults to 0 = DefaultStrategy).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithZLib(int? memoryLevel = null, int? strategy = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                // WindowBits intentionally NOT set here; streams/blocks choose correct window form (zlib header).
                MemoryLevel = memoryLevel ?? Interop.ZLib.Deflate_DefaultMemLevel,
                Strategy = strategy ?? 0
            };
            return this;
        }

        /// <summary>
        /// Configure ZLib with settings optimized for best compression (higher mem level).
        /// </summary>
        public CompressionOptions WithZLibMax(int? memoryLevel = null, int? strategy = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                // WindowBits intentionally NOT set here; streams/blocks choose correct window form (zlib header).
                MemoryLevel = memoryLevel ?? 9, // prefer maximum memory for best ratio
                Strategy = strategy ?? 0
            };
            return this;
        }

        /// <summary>
        /// Configure raw Deflate (no zlib/gzip header) mode with optional memory level and strategy.
        /// Streams/blocks choose the window bits for raw deflate form.
        /// </summary>
        /// <param name="memoryLevel">Memory level 1..9 (defaults to zlib default).</param>
        /// <param name="strategy">Compression strategy (defaults to 0 = DefaultStrategy).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithDeflate(int? memoryLevel = null, int? strategy = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                // WindowBits intentionally NOT set here; streams/blocks choose correct window form (raw deflate).
                MemoryLevel = memoryLevel ?? Interop.ZLib.Deflate_DefaultMemLevel,
                Strategy = strategy ?? 0
            };
            return this;
        }

        /// <summary>
        /// Configure raw Deflate with settings optimized for best compression (higher mem level).
        /// </summary>
        public CompressionOptions WithDeflateMax(int? memoryLevel = null, int? strategy = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                // WindowBits intentionally NOT set here; streams/blocks choose correct window form (raw deflate).
                MemoryLevel = memoryLevel ?? 9, // prefer maximum memory for best ratio
                Strategy = strategy ?? 0
            };
            return this;
        }

        /// <summary>
        /// Configure GZip (gzip header) mode with optional memory level and strategy.
        /// Streams/blocks choose the window bits for gzip header form.
        /// </summary>
        /// <param name="memoryLevel">Memory level 1..9 (defaults to zlib default).</param>
        /// <param name="strategy">Compression strategy (defaults to 0 = DefaultStrategy).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithGZip(int? memoryLevel = null, int? strategy = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                // WindowBits intentionally NOT set here; streams/blocks choose correct window form (gzip header).
                MemoryLevel = memoryLevel ?? Interop.ZLib.Deflate_DefaultMemLevel,
                Strategy = strategy ?? 0
            };
            return this;
        }

        /// <summary>
        /// Configure GZip with settings optimized for best compression (higher mem level).
        /// </summary>
        public CompressionOptions WithGZipMax(int? memoryLevel = null, int? strategy = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                // WindowBits intentionally NOT set here; streams/blocks choose correct window form (gzip header).
                MemoryLevel = memoryLevel ?? 9, // prefer maximum memory for best ratio
                Strategy = strategy ?? 0
            };
            return this;
        }

        /// <summary>
        /// Configures this instance with Fast-LZMA2 dictionary settings.
        /// </summary>
        /// <param name="dictionarySize">Dictionary size in bytes (default: 64MB).</param>
        /// <param name="strategy">Strategy: 1=fast, 2=optimized, 3=ultra (default: 3).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithFastLzma2Dictionary(long? dictionarySize = null, int? strategy = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                DictionarySize = dictionarySize ?? (64L * 1024 * 1024), // 64MB
                Strategy = strategy ?? 3, // Ultra strategy
                LiteralContextBits = 3,
                LiteralPositionBits = 0,
                PositionBits = 2,
                FastBytes = 48, // FastLength equivalent
                SearchDepth = 42 // Default search depth
            };

            // Default to pseudo-solid/full solid behaviour for best ratio, caller can override BlockSize/ThreadCount.
            this.BlockSize = -1;

            return this;
        }

        /// <summary>
        /// Configures this instance with LZ4 dictionary settings.
        /// </summary>
        /// <param name="dictionarySize">Dictionary size for dictionary mode (default: 1MB).</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithLz4Dictionary(long? dictionarySize = null)
        {
            Dictionary = new CompressionDictionaryOptions
            {
                DictionarySize = dictionarySize ?? (1024L * 1024), // 1MB
                Strategy = (int)Type // Map compression level to LZ4 strategy
            };
            return this;
        }

        /// <summary>
        /// Configures this instance with custom dictionary size (algorithm-agnostic).
        /// </summary>
        /// <param name="dictionarySize">Dictionary size in bytes.</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithDictionarySize(long dictionarySize)
        {
            Dictionary ??= new CompressionDictionaryOptions();
            Dictionary.DictionarySize = dictionarySize;
            return this;
        }

        /// <summary>
        /// Configures this instance with custom window bits (algorithm-agnostic).
        /// </summary>
        /// <param name="windowBits">Window size as power of 2.</param>
        /// <returns>This instance for method chaining.</returns>
        public CompressionOptions WithWindowBits(int windowBits)
        {
            Dictionary ??= new CompressionDictionaryOptions();
            Dictionary.WindowBits = windowBits;
            return this;
        }


    }

    /// <summary>
    /// Represents dictionary and advanced compression options for algorithms that support fine-tuning.
    /// These options provide control over memory usage, compression ratio, and performance characteristics.
    /// </summary>
    public class CompressionDictionaryOptions
    {
        // === TIER 1: Universal Dictionary/Window Options ===

        private long? _dictionarySize;

        /// <summary>
        /// Gets or sets the dictionary size in bytes for algorithms that support it.
        /// This is the most impactful setting for compression ratio vs memory usage.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> Range: (1 &lt;&lt; 12) to (1 &lt;&lt; 27) = 4KB to 128MB (32-bit), up to (3 &lt;&lt; 29) = 1.5GB (64-bit). Default: 16MB</para>
        /// <para>• <strong>Fast-LZMA2:</strong> Explicit dictionary size in bytes. Default: 64MB</para>
        /// <para>• <strong>LZ4:</strong> Used when dictionary mode is enabled for compression/decompression</para>
        /// <para>• <strong>ZStd:</strong> Implicitly controlled via WindowBits (2^WindowBits = dictionary size)</para>
        /// <para><strong>Impact:</strong> Larger values provide better compression but use more memory and may be slower.</para>
        /// </summary>
        public long? DictionarySize
        {
            get => _dictionarySize;
            set
            {
                if (value.HasValue)
                {
                    if (value.Value < 0 || value.Value > uint.MaxValue)
                        throw new ArgumentOutOfRangeException(nameof(DictionarySize), $"DictionarySize must be between 0 and {uint.MaxValue} (fits in uint).");
                }
                _dictionarySize = value;
            }
        }

        /// <summary>
        /// Gets or sets the window size as a power of 2 (log2 value) for algorithms that use logarithmic sizing.
        /// Alternative to DictionarySize for algorithms that prefer logarithmic parameters.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>ZStd:</strong> WindowLog range: 10-31. Default: 23 (8MB). Dictionary size = 2^WindowBits</para>
        /// <para>• <strong>Brotli:</strong> Window bits range: 10-24. Default: 22 (4MB). Larger = better compression</para>
        /// <para>• <strong>ZLib/Deflate/GZip:</strong> Range: 8-15 (256B-32KB), or negative (-8 to -15) for raw deflate without headers</para>
        /// <para><strong>Impact:</strong> Higher values increase memory usage but improve compression for large files.</para>
        /// </summary>
        public int? WindowBits { get; set; }

        // === TIER 2: LZMA Family Fine-Tuning (Highest ROI) ===

        /// <summary>
        /// Gets or sets the number of literal context bits for LZMA family algorithms.
        /// Controls how many bits of context are used when encoding literal (unmatched) bytes.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> Range: 0-8, default: 3. Higher values improve text compression</para>
        /// <para>• <strong>Fast-LZMA2:</strong> Range: 0-8, default: 3. Used in LZMA2 encoder settings</para>
        /// <para><strong>Impact:</strong> Higher values significantly improve compression of text/source code but increase memory usage exponentially.</para>
        /// <para><strong>Memory:</strong> Memory usage increases by 2^(lc+lp) factor.</para>
        /// </summary>
        public int? LiteralContextBits { get; set; }

        /// <summary>
        /// Gets or sets the number of literal position bits for LZMA family algorithms.
        /// Controls position-dependent context for literal byte encoding.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> Range: 0-4, default: 0. Helps with position-sensitive data patterns</para>
        /// <para>• <strong>Fast-LZMA2:</strong> Range: 0-4, default: 0. Used in LZMA2 encoder settings</para>
        /// <para><strong>Impact:</strong> Useful for data with alignment patterns or structured layouts (e.g., executable files).</para>
        /// <para><strong>Memory:</strong> Memory usage increases by 2^(lc+lp) factor.</para>
        /// </summary>
        public int? LiteralPositionBits { get; set; }

        /// <summary>
        /// Gets or sets the number of position bits for LZMA family algorithms.
        /// Controls position-dependent context for match distance encoding.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> Range: 0-4, default: 2. Optimizes for data alignment patterns</para>
        /// <para>• <strong>Fast-LZMA2:</strong> Range: 0-4, default: 2. Used in LZMA2 encoder settings</para>
        /// <para><strong>Impact:</strong> Higher values help with binary data that has specific alignment requirements.</para>
        /// <para><strong>Memory:</strong> Memory usage increases by 2^pb factor for some internal structures.</para>
        /// </summary>
        public int? PositionBits { get; set; }

        /// <summary>
        /// Gets or sets the number of fast bytes (lookahead) for LZMA family algorithms.
        /// Controls how far ahead the encoder looks for better matches.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> Range: 5-273, default: 32. This is often the second most impactful setting after dictionary size</para>
        /// <para>• <strong>Fast-LZMA2:</strong> Similar concept as FastLength parameter (default: 48)</para>
        /// <para><strong>Impact:</strong> Higher values provide better compression but significantly slower encoding. Diminishing returns above 64-128.</para>
        /// <para><strong>Performance:</strong> Values above 100 can dramatically slow compression with minimal ratio improvement.</para>
        /// </summary>
        public int? FastBytes { get; set; }

        // === TIER 3: Cross-Algorithm Strategy & Advanced Options ===

        /// <summary>
        /// Gets or sets the compression strategy/algorithm variant for different algorithms.
        /// Each algorithm interprets this value differently to optimize for specific data patterns.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> 0=fast algorithm, 1=normal algorithm (default). Normal provides better compression</para>
        /// <para>• <strong>ZStd:</strong> 1=fast, 2=dfast, 3=greedy, 4=lazy, 5=lazy2, 6=btlazy2, 7=btopt, 8=btultra, 9=btultra2</para>
        /// <para>• <strong>ZLib/Deflate:</strong> 0=default, 1=filtered, 2=huffman_only, 3=rle, 4=fixed</para>
        /// <para>• <strong>Brotli:</strong> Maps to quality level (0-11)</para>
        /// <para>• <strong>Fast-LZMA2:</strong> 1=fast, 2=optimized, 3=ultra (hybrid mode)</para>
        /// <para><strong>Impact:</strong> Algorithm-specific optimizations for different data types and speed/ratio trade-offs.</para>
        /// </summary>
        public int? Strategy { get; set; }

        /// <summary>
        /// Gets or sets the algorithm/mode selection for LZMA family algorithms.
        /// Controls the fundamental compression algorithm used.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> 0=fast algorithm, 1=normal algorithm (default)</para>
        /// <para><strong>Impact:</strong> Fast algorithm trades compression ratio for speed. Normal algorithm provides better compression.</para>
        /// </summary>
        public int? Algorithm { get; set; }

        /// <summary>
        /// Gets or sets the binary tree mode for LZMA family match finding.
        /// Controls the data structure used for finding matches during compression.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> 0=hash chain mode, 1=binary tree mode (default)</para>
        /// <para><strong>Impact:</strong> Binary tree mode generally provides better compression ratios but uses more CPU and memory.</para>
        /// </summary>
        public int? BinaryTreeMode { get; set; }

        /// <summary>
        /// Gets or sets hash table configuration for various algorithms.
        /// Controls the size or type of hash tables used for match finding.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> Hash bytes (2, 3, or 4, default: 4). Number of bytes used for hash calculation</para>
        /// <para>• <strong>ZStd:</strong> HashLog (6-26), controls hash table size as 2^HashLog entries</para>
        /// <para><strong>Impact:</strong> Higher values improve compression but increase memory usage and may affect speed.</para>
        /// </summary>
        public int? HashBytes { get; set; }

        /// <summary>
        /// Gets or sets the match search depth/thoroughness for compression algorithms.
        /// Controls how extensively the algorithm searches for optimal matches.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> Match cycles (1 to 1&lt;&lt;30, default: 32). Maximum search iterations for matches</para>
        /// <para>• <strong>ZStd:</strong> SearchLog (1-26), controls search length as 2^SearchLog</para>
        /// <para>• <strong>Fast-LZMA2:</strong> SearchDepth (default: 42), maximum match finder search depth</para>
        /// <para><strong>Impact:</strong> Higher values can improve compression ratio but significantly slow encoding. Diminishing returns.</para>
        /// </summary>
        public int? SearchDepth { get; set; }

        /// <summary>
        /// Gets or sets the match cycles for LZMA family algorithms.
        /// Controls the maximum number of search iterations when looking for matches.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>LZMA/LZMA2:</strong> Range: 1 to (1&lt;&lt;30), default: 32</para>
        /// <para><strong>Impact:</strong> Higher values can improve compression but may significantly slow encoding with diminishing returns.</para>
        /// </summary>
        public int? MatchCycles { get; set; }

        // === TIER 4: Algorithm-Specific Advanced Options ===

        /// <summary>
        /// Gets or sets the memory usage level for ZLib family algorithms.
        /// Controls the trade-off between memory usage and compression performance.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>ZLib/Deflate/GZip:</strong> Range: 1-9, default: 8. Higher = more memory, better compression</para>
        /// <para><strong>Impact:</strong> Level 1 uses minimum memory but is slow and reduces compression ratio.</para>
        /// <para>Level 9 uses maximum memory for optimal speed. Recommended: 7 for no compression, 8 for compression.</para>
        /// </summary>
        public int? MemoryLevel { get; set; }

        /// <summary>
        /// Gets or sets the hash log for ZStd compression.
        /// Controls the size of the hash table used for match finding.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>ZStd:</strong> Range: 6-26, hash table size = 2^HashLog entries</para>
        /// <para><strong>Impact:</strong> Larger hash tables can improve compression ratio but use more memory.</para>
        /// </summary>
        public int? HashLog { get; set; }

        /// <summary>
        /// Gets or sets the chain log for ZStd compression.
        /// Controls the size of the match chain table.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>ZStd:</strong> Range: 6-30, chain table size = 2^ChainLog entries</para>
        /// <para><strong>Impact:</strong> Larger chain tables can improve compression ratio but use more memory and CPU.</para>
        /// </summary>
        public int? ChainLog { get; set; }

        /// <summary>
        /// Gets or sets the search log for ZStd compression.
        /// Controls the search length for match finding.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>ZStd:</strong> Range: 1-26, search length = 2^SearchLog</para>
        /// <para><strong>Impact:</strong> Longer searches can improve compression ratio but slow encoding.</para>
        /// </summary>
        public int? SearchLog { get; set; }

        /// <summary>
        /// Gets or sets the minimum match length for ZStd compression.
        /// Controls the shortest allowable match length.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>ZStd:</strong> Range: 3-7, default varies by level</para>
        /// <para><strong>Impact:</strong> Shorter minimum matches can improve compression of repetitive data.</para>
        /// </summary>
        public int? MinMatch { get; set; }

        /// <summary>
        /// Gets or sets the target match length for ZStd compression.
        /// Controls the desired match length that the algorithm aims for.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>ZStd:</strong> Range: 0-999, default varies by level</para>
        /// <para><strong>Impact:</strong> Longer target lengths can improve compression ratio but may slow encoding.</para>
        /// </summary>
        public int? TargetLength { get; set; }

        /// <summary>
        /// Gets or sets the quality level for Brotli compression.
        /// Alternative interface to Brotli's compression level.
        /// <para><strong>Algorithms:</strong></para>
        /// <para>• <strong>Brotli:</strong> Range: 0-11, default: 6. Higher = better compression, slower speed</para>
        /// <para><strong>Impact:</strong> Level 0 provides minimal compression but fastest speed.</para>
        /// <para>Level 11 provides maximum compression but slowest speed.</para>
        /// </summary>
        public int? Quality { get; set; }
    }
}

# GrindCore Quick Start

A concise guide to highlight the features and usage patterns for GrindCore.

---

## Overview

GrindCore is a high-performance C# compression and hashing library built on top of modern C implementations. It consists of two projects:
- **GrindCore**: The multiplatform C library.
- **GrindCore.net**: The .NET wrapper, providing a standardized API for .NET applications.

Published to NuGet simply as **GrindCore**.

**Related Projects:**
- **[GrindCore.SharpCompress](https://github.com/Nanook/GrindCore.SharpCompress)**: Enhanced fork of SharpCompress leveraging GrindCore's native streams for improved performance and additional features like LZMA/2 level support.

GrindCore is designed to follow the familiar `System.IO.Compression` patterns, making it easy to adopt and update with the latest algorithms.

Key features:
- **Platforms**: win-x64, win-x86, win-arm64, linux-x64, linux-arm64, linux-arm, osx-x64 and osx-arm64
- **Frameworks**: .NET 9/8/7/6/5, .NET Core 3.1, .NET Standard 2.1/2.0, and .NET Framework 4.x/3.5
- **Stream compression and hashing**
- **Block compression**
- **Synchronous and asynchronous patterns**

## Stream Compression Architecture

All stream classes in GrindCore follow a standardized pattern, making them easy to use and integrate.

### Key Features

- **Inheritance**: All compression streams inherit from `CompressionStream`.
- **Unified Constructor**: All compression streams can be constructed with a `Stream` and a `CompressionOptions` object. Not all CompressionOptions are supported for all algorithms. See the property documentation for information.
- **Flexible Creation**: Streams can be created directly (`new ZStdStream(stream, options)`) or via factory (`CompressionStreamFactory.Create(algo)`).
- **Read/Write Modes**:
  - **Read**: Decompress from a compressed stream.
  - **Write**: Compress data to a target stream.
- **Position Tracking**:
  - `Position`: Bytes read or written to the base (compressed) stream.
  - `PositionFullSize`: Bytes read or written in uncompressed form.
- **CompressionOptions**:
  - `Algorithm`: - Copy, Brotli, Deflate, DeflateNg, FastLzma2, GZip, GZipNg, Lz4, Lzma, Lzma2, ZLib, ZLibNg and ZStd
  - `Type`: Set to a compression level for compression, or `Decompress` for decompression.
  - `Version`: Pin to a specific algorithm version for byte-perfect output.
  - `LeaveOpen`: Leave the base/source stream open on dispose.
  - `PositionLimit` / `PositionFullSizeLimit`: Built-in limits to prevent overreading/overwriting without additional wrapper streams.
  - Algorithm-specific options (e.g., `BlockSize` for LZMA2).
  - Compression tuning: many encoders expose fine-grained tuning via `CompressionOptions.Dictionary` (see note below).
- **Synchronous and Asynchronous Methods**: Both blocking and async APIs are provided.
- Static factory classes support stream creation, returning as CompressionStream and exposing all the above functionality

> **Technical Note**: LZMA and LZMA2 C implementations have been carefully modified with minimal changes to support .NET's streaming pattern of multiple read/write calls, rather than requiring single-buffer or dual-stream approaches.

### CompressionOptions.Dictionary (tuning)

For algorithms that support detailed tuning (notably the LZMA family: LZMA, LZMA2 and Fast-LZMA2), use `CompressionOptions.Dictionary` to provide encoder-specific settings. The Dictionary object is intentionally algorithm-agnostic; encoders map the relevant fields to native parameters.

> **Note**: Fast-LZMA2 has sophisticated native multi-threading support and uses its own internal parameter system. Dictionary options are automatically mapped to appropriate native Fast-LZMA2 settings.

Common useful fields for the LZMA family:
- `DictionarySize` — dictionary size in bytes (most impactful for ratio/memory).
- `FastBytes` — encoder lookahead / fast-match length.
- `LiteralContextBits`, `LiteralPositionBits`, `PositionBits` — map to LZMA's lc/lp/pb settings.
- `Algorithm`, `BinaryTreeMode`, `HashBytes`, `MatchCycles`, `SearchDepth` — various match-finder and algorithm-mode knobs.
- `Strategy` — used by some wrappers to indicate compression level/variant for hybrid encoders (Fast?LZMA2 maps Strategy ? compression level).
- `ThreadCount` (on `CompressionOptions`) — used where the native encoder supports multithreading (e.g., Fast-LZMA2, block-based LZMA2). Note: LZMA uses single-threading for stability; LZMA2 and Fast-LZMA2 support true multithreading.

Best practice:
- Set tuning via `CompressionOptions.WithLzmaDictionary(...)`, `WithLzma2Dictionary(...)` or `WithFastLzma2Dictionary(...)` helpers where provided.
- Dictionary Size: Only set explicit `DictionarySize` when needed; native normalization often chooses optimal values based on compression level.
- After compression, persist `CompressionStream.Properties` (encoder runtime properties like LZMA property byte) alongside compressed data. When decompressing, set `CompressionOptions.InitProperties = savedProperties` so the decoder is initialized identically.
- Encoders clone dictionary options before mutating and validate/clamp ranges; follow the same pattern in your code if you mutate the input object.


Short example:

```csharp
// Compress with LZMA2, tuned dictionary and threads
var opts = CompressionOptions.DefaultCompressOptimal()
    .WithLzma2Dictionary(dictionarySize: 64 * 1024 * 1024, fastBytes: 64);
opts.ThreadCount = 4; // enable multithreaded encoder when supported

using (var s = new Lzma2Stream(output, opts))
{
    // ... write data ...
    s.Complete();
    var props = s.Properties; // store with archive metadata
}

// Decompress later
var dOpts = CompressionOptions.DefaultDecompress();
dOpts.InitProperties = props; // ensure decoder uses exact encoder settings
using var ds = new Lzma2Stream(input, dOpts);
```

### Stream Creation

Streams can be created using either approach based on your requirements:

#### Factory Approach

```csharp
// Factory approach - algorithm-agnostic
CompressionStream stream = CompressionStreamFactory.Create(
  CompressionAlgorithm.ZStd, outputStream, CompressionType.Fastest);

// With full options
var stream = CompressionStreamFactory.Create(CompressionAlgorithm.Lzma2, outputStream,
  new CompressionOptions { Type = CompressionType.Optimal, BlockSize = 0x200000 });
```

#### Direct Instantiation
```csharp
// Direct instantiation - type-safe, algorithm-specific
using var zstdStream = new ZStdStream(outputStream, 
  new CompressionOptions { Type = CompressionType.SmallestSize });

using var brotliStream = new BrotliStream(inputStream, 
  CompressionOptions.DefaultDecompress());
```

## Block Compression Architecture 

GrindCore provides high-performance block compression classes for one-shot compression and decompression of buffers. This is ideal for custom formats, filesystems, parallel block processing, and scenarios where streaming is not required.

### Key Features

- **Unified API**: All block compressors inherit from `CompressionBlock` and use `CompressionOptions`.
- **Stateless and Thread-Safe**: Block classes are stateless and can be used concurrently across threads.
- **Flexible Creation**: Blocks can be instantiated directly (`new Lz4Block(options)`) or via factory (`CompressionBlockFactory.Create()`).
- **Supported Algorithms**: Copy, Brotli, Deflate, FastLzma2, GZip, Lz4, Lzma, Lzma2, ZLib, ZStd.
- **CompressionOptions**:
  - `Type`: Set to a compression level (never `Decompress`) for compression. Use `Decompress()` for decompression.
  - `BlockSize`: Required for most block compressors; determines the input size per operation.
  - `ThreadCount`: For multi-threaded block algorithms (e.g., LZMA2, FastLzma2).
  - `Version`: Pin to a specific algorithm version for reproducible output.
- **Methods**:
  - `Compress(...)`: Compresses a buffer in one call. Returns a `CompressionResultCode` and updates the compressed size via a `ref int` parameter.
  - `Decompress(...)`: Decompresses a buffer in one call. Returns a `CompressionResultCode` and updates the decompressed size via a `ref int` parameter.
  - Async variants: `CompressAsync(...)`, `DecompressAsync(...)` for high-performance scenarios.
  - `RequiredCompressOutputSize`: Get the recommended output buffer size for compression.

> **Note:** With block compression, the sizes of blocks must be known and allocated by the consumer. Always allocate more space than the input when compressing, as compression can increase the size of the data.

### CompressionResultCode

All block compression and decompression methods return a `CompressionResultCode` value, which can be one of:

- `Success`: The operation completed successfully.
- `Error`: The operation failed due to an unknown or generic error.
- `InsufficientBuffer`: The destination buffer is too small.
- `InvalidData`: The source data is corrupted or invalid.
- `InvalidParameter`: Invalid parameters were provided.
- `NotSupported`: The operation is not supported.

Check the return value to determine the result of the operation, and use the updated size from the `ref int` parameter.

### Example: Block Compression

```csharp
// Direct instantiation using
var lz4 = new Lz4Block(new CompressionOptions
{
  Type = CompressionType.Optimal, BlockSize = 65536
});

// Factory approach using
var compressor = CompressionBlockFactory.Create(CompressionAlgorithm.ZStd, CompressionType.Fastest, blockSize: 65536);

// Consistent API regardless of creation method
byte[] input = GetData();
byte[] output = new byte[compressor.RequiredCompressOutputSize];
int compressedSize = output.Length;

// Compress: returns CompressionResultCode, updates compressedSize via ref CompressionResultCode
compressResult = compressor.Compress( input, 0, input.Length, output, 0, ref compressedSize);
if (compressResult != CompressionResultCode.Success)
  throw new InvalidOperationException($"Compression failed: {compressResult}");

// Decompress: returns CompressionResultCode, updates decompressedSize via ref
byte[] decompressed = new byte[input.Length];
int decompressedSize = decompressed.Length;
CompressionResultCode decompressResult = compressor.Decompress(output, 0, compressedSize, decompressed, 0, ref decompressedSize);
if (decompressResult != CompressionResultCode.Success)
  throw new InvalidOperationException($"Decompression failed: {decompressResult}");

// Validate round-trip
Debug.Assert(decompressedSize == input.Length);
Debug.Assert(input.AsSpan(0, input.Length).SequenceEqual(decompressed.AsSpan(0, decompressedSize)));
```

### Example: Handling Return Codes

```csharp
int compressedSize = output.Length;
var result = compressor.Compress(input, 0, input.Length, output, 0, ref compressedSize);

switch (result)
{
  case CompressionResultCode.Success: // Use compressedSize
    break;
  case CompressionResultCode.InsufficientBuffer: // Increase output buffer and retry
    break;
  case CompressionResultCode.InvalidData: // Handle invalid input
    break;
  default: // Handle other errors
    break;
}
```

## Hashing Architecture

GrindCore provides a set of high-performance, .NET-compliant hashing algorithms, designed for easy integration with .NET's cryptographic infrastructure.

### Key Features

- **HashAlgorithm Integration**: All hash classes inherit from `HashAlgorithm`, allowing seamless use with .NET APIs such as `CryptoStream`.
- **Supported Algorithms** (via `HashFactory`):
  - Blake3, Blake2sp
  - XXHash64, XXHash32
  - MD5, MD4, MD2
  - SHA1
  - SHA2-512, SHA2-384, SHA2-256
  - SHA3-512, SHA3-384, SHA3-256, SHA3-224
- **Flexible Output**: Hashes are returned as byte arrays via the standard `Hash` property.
- **Streaming Support**: Hashes can be computed incrementally using streams, supporting large data sets efficiently.
- **Disposal and Reuse**: Hash objects are disposable and can be reused for multiple computations.
- **Factory Usage**: The `HashFactory` class provides a unified API for creating and computing hashes.
- **Hash .HashAsType<T>** Hashes support generic value access for supported types, e.g. `.HashAsType<ulong>()` for XXHash64. This allows you to retrieve the hash result directly as a native type (such as `uint`, `ulong`) without byte conversion, also supporting `byte[]`.

### Example: Hashing

```csharp
// One-shot hashing
byte[] data = GetData();
byte[] hash = HashFactory.Compute(HashType.Blake3, data);

// Factory for streaming
using var hasher = HashFactory.Create(HashType.XXHash64);
using var fileStream = File.OpenRead("data.bin");
using var cryptoStream = new CryptoStream(Stream.Null, hasher, CryptoStreamMode.Write);
fileStream.CopyTo(cryptoStream);
cryptoStream.FlushFinalBlock();
byte[] result = hasher.Hash;

// Direct instantiation
using var blake3 = Blake3.Create();
byte[] directHash = blake3.ComputeHash(data);
```

### Example: Hashing a Byte Array with Offset in oneshot

```csharp
byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World! This is a longer string for demonstration purposes.");
int offset = 7;
int length = 5; // Hash "World"

byte[] hashBytes = HashFactory.Compute(HashType.SHA2_256, data, offset, length);

// Compute XXHash64 as a ulong using the generic value accessor
using var xxh64 = HashFactory.Create(HashType.XXHash64);
xxh64.TransformBlock(data, offset, length, data, offset); // Hash "World"
xxh64.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
ulong hashValue = xxh64.HashAsType<ulong>();
```



## Future Features

As the library name reflects, this library implements computationaly intensive algorithms that are primarily written in C. It may have other algorithms added if required. E.g. audio compression etc.
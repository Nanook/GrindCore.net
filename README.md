# GrindCore  

An AOT-compatible compression and hashing library built in the **System.IO.Compression** style—providing a managed .NET wrapper around the **[GrindCore](https://github.com/Nanook/GrindCore) Native** library.  

Published on **NuGet** as [GrindCore](https://www.nuget.org/packages/GrindCore) with support for multiple platforms.  

## Table of Contents

- [Quick Start](#quick-start)
- [Overview](#overview)
- [Core Objectives](#core-objectives)
- [Key Features](#key-features)
- [Compression](#compression)
- [Hashing](#hashing)
- [Continuous Integration (CI) Status](#continuous-integration-ci-status)
- [Key Project Integrations](#key-project-integrations)
- [Addressing Current Issues](#addressing-current-issues)
- [To Do](#to-do)
- [Conclusion](#conclusion)

## Quick Start

For usage examples and API patterns, see the **[Getting Started Guide](GettingStarted.md)**.

For more in-depth information, see [![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/Nanook/GrindCore.net).

### NuGet

[![NuGet](https://img.shields.io/nuget/v/GrindCore.svg)](https://www.nuget.org/packages/GrindCore)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

```
dotnet add package GrindCore
```

## Overview

GrindCore is an innovative library designed to streamline and enhance compression processes in dotnet applications. It aims to tackle prevalent issues such as performance degradation and outdated implementations when native code updates frequently. By unifying multiple C forks into a single, multiplatform library, GrindCore achieves a cohesive and efficient solution.

## Core Objectives

The primary goal of GrindCore is to deliver a maintainable compression and hashing solution for dotnet applications. Leveraging the exact method used to build C in the dotnet runtime ensures a robust approach. By preserving precise compression algorithms for key versions, GrindCore guarantees compatibility and reliability for projects requiring byte-perfect output.

## Key Features

- **Stream Position Correction**: Advanced buffer management for precise stream rewinding when overreading occurs
- **Multi-Framework Support**: Compatible with .NET Framework 2.0 through .NET 10
  `net10.0;net9.0;net8.0;net7.0;net6.0;net5.0;netcoreapp3.1;netstandard2.1;netstandard2.0;net48;net47;net46;net45;net40;net35;net20`
- **AOT Compatible**: Fully supports Ahead-of-Time compilation
- **Native Performance**: Leverages native C libraries for optimal performance
- **Seekable Compression**: ZStd seekable format for random access into compressed archives

### Compression  

GrindCore implements compression in two forms: **Stream-based** and **Block-based**.  

- **Stream-based compression** follows the standard .NET approach, enabling seamless integration with existing workflows.  
- **Block-based compression** is designed for one-shot buffer compression, providing efficient, high-performance processing for specific use cases.  

All **compression streams** inherit from the `CompressionStream` class, ensuring consistent behavior and shared functionality across implementations.  
Similarly, all **block-based compression** implementations inherit from `CompressionBlock`, maintaining structured handling of compression operations.  

Additionally, GrindCore provides **seekable compression** via `ZStdSeekableStream`, enabling random access decompression of ZStd archives. Data is organized into independently decompressible frames with a seek table, allowing efficient seeking to any byte offset without scanning the entire archive. See the [Getting Started Guide](GettingStarted.md#zstd-seekable-stream) for details.

To simplify instance creation, GrindCore provides:  
- `CompressionStreamFactory`, allowing easy instantiation of stream-based compression classes.  
- `CompressionBlockFactory`, offering a straightforward mechanism for initializing block-based compression instances.  

---

#### Supported Compression Algorithms  

All native compression algorithms are directly built into the [GrindCore Native](https://github.com/Nanook/GrindCore) project—**no third-party binaries are used or required**. The following algorithms are compiled from source as part of the native library, ensuring full integration, security, and maintainability:

- **Brotli** v1.1.0 (from [.NET 9.0](https://github.com/dotnet/runtime/tree/release/9.0/src/native/external/brotli))
- **BZip2** v1.0.8 (from [bzip2](https://sourceware.org/git/?p=bzip2.git)) — supports multi-stream (concatenated) decompression
- **Copy** (no compression - direct stream copy)
- **LZ4** v1.10.0 (from [LZ4](https://github.com/lz4/lz4/tree/dev/lib))
- **LZMA** v25.1.0 (from [7Zip](https://sourceforge.net/projects/sevenzip/files/7-Zip/25.01/) App)
- **LZMA2** v25.1.0 (from [7Zip](https://sourceforge.net/projects/sevenzip/files/7-Zip/25.01/) App)
- **Fast-LZMA2** v1.0.1 (from [7Zip-mcmilk](https://github.com/mcmilk/7-Zip-zstd/tree/master/C/fast-lzma2))
- **ZLib** v1.3.1 (GZip, ZLib, Deflate - from [.NET 8.0](https://github.com/dotnet/runtime/tree/release/8.0/src/native/external/zlib))
- **ZLib-NG** v2.2.1 (GZip, ZLib, Deflate - from [.NET 9.0](https://github.com/dotnet/runtime/tree/release/9.0/src/native/external/zlib-ng))
- **ZStd** v1.5.7 & v1.5.2 (from [Facebook](https://github.com/facebook/zstd/tree/dev/lib)) — includes seekable format, skippable frame support, multithreaded compression, and dictionary support

**Notes:**
- Multiple versions of some algorithms (e.g., ZStd, ZLib/ZLib-NG) are included to support applications that require pinned or frozen versions, most commonly for scenarios demanding byte-perfect, deterministic outputs.
- ZStd supports multithreaded compression via `ThreadCount` in `CompressionOptions`, enabling parallel compression using multiple worker threads for significantly higher throughput on multicore systems.
- ZStd supports pre-trained dictionaries via `InitProperties` in `CompressionOptions` for improved compression of small data.
- The set of supported algorithms will continue to expand.
- Both blocking and asynchronous methods are available, allowing flexible compression workflows.
- Compression streams expose `.Position` (compressed) and `.PositionFullSize` (uncompressed) properties for accurate progress tracking.

### Hashing

Hashes inherit from HashAlgorithm allowing them to be used with CryptoStream for standard .Net use.

- Blake3, Blake2sp
- MD5, MD4, MD2
- SHA1
- SHA2 [SHA256, SHA384, SHA512]
- SHA3 [SHA3-224, SHA3-256, SHA3-384, SHA3-512]
- XXHash [XXH32, XXH64]

## Continuous Integration (CI) Status

A comprehensive list of test statuses for various platforms is available below. For a quick summary: GrindCore is being actively tested across major platforms including Linux ARM64, Linux ARM, Linux x64, macOS x64, macOS ARM64, Windows x64, Windows x86, and Windows ARM64.

### Detailed Test Status

| Platform            | Unit Test Status                                                                                      |
|---------------------|-------------------------------------------------------------------------------------------------------|
| **Linux ARM64**     | ![Linux ARM64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?branch=main)   |
| **Linux ARM**       | ![Linux ARM Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?branch=main)       |
| **Linux x64**       | ![Linux x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?branch=main)       |
| **macOS x64**       | ![macOS x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?branch=main)         |
| **macOS ARM64**     | ![macOS ARM64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?branch=main)     |
| **Windows x86**     | ![Windows x86 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?branch=main)       |
| **Windows x64**     | ![Windows x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?branch=main)       |
| **Windows ARM64**   | ![Windows arm64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?branch=main)   |

## Key Project Integrations

GrindCore integrates robust solutions from several key projects:

- **[dotnet Runtime GitHub Repository](https://github.com/dotnet/runtime):**
  - Provides a foundation with multiplatform C compilation based on CMake and C, ensuring seamless integration across different platforms.
  - Supplies zlib/deflate and Brotli from the dotnet 8 code, combined with C# wrappers, to offer efficient and reliable compression algorithms.
- **[ZStd Facebook GitHub Repository](https://github.com/facebook/zstd):**
  - Multiplatform zstandard direct from the source.
- **[bzip2 Official Repository](https://sourceware.org/bzip2/):**
  - The official bzip2/libbzip2 1.0.8 source, providing block-sorting compression with streaming and one-shot block APIs.
- **[7zip mcmilk GitHub Repository](https://github.com/mcmilk/7-Zip-zstd):**
  - Contributes a comprehensive suite of hash functions, including SHA-1, SHA-2, SHA-3, MD2, MD4, MD5, and XXHash (32 and 64). More compression and hashing algorithms will be ported, benefiting from a uniform Make project structure that simplifies integration.
- **[GrindCore.SharpCompress](https://github.com/Nanook/GrindCore.SharpCompress):**
  - Enhanced fork of SharpCompress leveraging GrindCore's native streams for improved performance and additional features like LZMA/2 level support.

## Addressing Current Issues

GrindCore is designed to overcome several known complications in the dotnet ecosystem:

- **Performance:**
  - C# ports generally perform slower than native C, although the JIT offers powerful optimization capabilities.
- **Up-to-date Implementations:**
  - Leveraging well-maintained projects like dotnet Runtime and 7zip mcmilk ensures that the C algorithms can be updated easily.
- **Cross-Platform Compatibility:**
  - Through multiplatform C compilation via the dotnet CMake system, GrindCore ensures seamless functionality across different operating systems. The managed layer abstracts this, allowing it to be used as System.IO.Compression would be used.
- **Consistency:**
  - By preserving exact compression algorithms, the library is ideal for projects requiring checksummed output, ensuring consistent data results and reliability.
- **Addressing Missing Functionality:**
  - GrindCore aims to expose additional functionalities not available in other libraries, such as `compress2` from zlib/deflate, providing more options and flexibility for developers.

## To Do

Several enhancements and additional features could be introduced to further improve GrindCore. While these may be addressed over time, listing them here serves to communicate known gaps and encourage community contributions:
- Multi-language support.
- ~~Custom Dictionaries and training where supported.~~ ✅ ZStd dictionary support added.
- Progress updates raised from C library.
- Update native compression versions.
- Expanded compression algorithm capabilities.
  - If you identify missing features, feel free to raise issues or submit pull requests.
  - Any unimplemented methods from the C source can be exposed upon request.

## Conclusion

GrindCore is on a journey to create a more reliable and efficient compression solution for dotnet. The community's contributions and collaboration are welcomed.

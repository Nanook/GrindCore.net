# GrindCore  

An AOT-compatible compression and hashing library built in the **System.IO.Compression** style—providing a managed .NET wrapper around the native **[GrindCore](https://github.com/Nanook/GrindCore) Native** library.  

Published on **NuGet** as [GrindCore](https://www.nuget.org/packages/GrindCore) with support for multiple platforms.  

> **⚠️ Important Notice**: While GrindCore has reached its first stable release, it should still be used with caution in production environments. The library is actively being tested and refined. Please thoroughly test in your specific use cases and report any issues encountered.

## Quick Start

For usage examples and API patterns, see the **[Quick Start Guide](QuickStart.md)**.

For more in-depth information, see [![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/Nanook/GrindCore.net).

### Nuget

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
- **Multi-Framework Support**: Compatible with .NET Framework 3.5 through .NET 9
  `net9.0;net8.0;net7.0;net6.0;net5.0;netcoreapp3.1;netstandard2.1;netstandard2.0;net48;net47;net46;net45;net40;net35`
- **AOT Compatible**: Fully supports Ahead-of-Time compilation
- **Native Performance**: Leverages native C libraries for optimal performance

### Compression  

GrindCore implements compression in two forms: **Stream-based** and **Block-based**.  

- **Stream-based compression** follows the standard .NET approach, enabling seamless integration with existing workflows.  
- **Block-based compression** is designed for one-shot buffer compression, providing efficient, high-performance processing for specific use cases.  

All **compression streams** inherit from the `CompressionStream` class, ensuring consistent behavior and shared functionality across implementations.  
Similarly, all **block-based compression** implementations inherit from `CompressionBlock`, maintaining structured handling of compression operations.  

To simplify instance creation, GrindCore provides:  
- `CompressionStreamFactory`, allowing easy instantiation of stream-based compression classes.  
- `CompressionBlockFactory`, offering a straightforward mechanism for initializing block-based compression instances.  

#### Supported Compression Algorithms  

- **Brotli** v1.1.0 _(From .NET 9.0)_  
- **Copy** _(No compression - direct stream copy)_  
- **LZ4** v1.9.4 _(From 7Zip-mcmilk)_  
- **LZMA** v24.7.0 _(From 7Zip-mcmilk)_  
- **LZMA2** v24.7.0 _(From 7Zip-mcmilk)_  
- **Fast-LZMA2** v1.0.1 _(From 7Zip-mcmilk)_  
- **ZLib** v1.3.1 _(GZip, ZLib, Deflate - From .NET 8.0)_  
- **ZLib-NG** v2.2.1 _(GZip, ZLib, Deflate - From .NET 9.0)_  
- **ZStd** v1.5.6 & v1.5.2 _(From 7Zip-mcmilk)_  

Additionally, **blocking and asynchronous methods** are implemented, allowing flexible compression workflows.

Streams expose the `.Position` (compressed) and `.PositionFullSize` (uncompressed) properties, allowing consuming objects to track progress and status with accuracy.

### Hashing

Hashes inherit from HashAlgorithm allowing them to be used with CryptoStream for standard dotnet use.

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
| **Linux ARM64**     | ![Linux ARM64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_arm64)   |
| **Linux ARM**       | ![Linux ARM Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_arm)       |
| **Linux x64**       | ![Linux x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_x64)       |
| **macOS x64**       | ![macOS x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_osx_x64)         |
| **macOS ARM64**     | ![macOS ARM64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_osx_arm64)     |
| **Windows x86**     | ![Windows x86 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_win_x86)       |
| **Windows x64**     | ![Windows x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_win_x64)       |
| **Windows ARM64**   | ![Windows arm64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_win_arm64)   |

## Key Project Integrations

GrindCore integrates robust solutions from several key projects:

- **[dotnet Runtime GitHub Repository](https://github.com/dotnet/runtime):**
  - Provides a foundation with multiplatform C compilation based on CMake and C, ensuring seamless integration across different platforms.
  - Supplies zlib/deflate and Brotli from the dotnet 8 code, combined with C# wrappers, to offer efficient and reliable compression algorithms.
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
- Dictionary support.
- Progress updates raised from C library.
- Update native compression versions.
- Expanded compression algorithm capabilities.
  - If you identify missing features, feel free to raise issues or submit pull requests.
  - Any unimplemented methods from the C source can be exposed upon request.

## Conclusion

GrindCore is on a journey to create a more reliable and efficient compression solution for dotnet. The community's contributions and collaboration are welcomed.

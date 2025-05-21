# GrindCore

A compression and hashing library built the System.IO.Compression way... A managed dotnet wrapper around a Native library ([GrindCore](https://github.com/Nanook/GrindCore) Native).

Published to nuget as [GrindCore](https://www.nuget.org/packages/GrindCore) and supports multiple platforms.

This library is in the early stages of development. There may be many breaking changes over the following months. Error handling and bounds checking is minimal. 

## Overview

GrindCore is an innovative library designed to streamline and enhance compression processes in dotnet applications. It aims to tackle prevalent issues such as performance degradation and outdated implementations when native code updates frequently. By unifying multiple C forks into a single, multiplatform library, GrindCore achieves a cohesive and efficient solution.

## Core Objectives

The primary goal of GrindCore is to deliver a maintainable compression and hashing solution for dotnet applications. Leveraging the exact method used to build C in the dotnet runtime ensures a robust approach. By preserving precise compression algorithms for key versions, GrindCore guarantees compatibility and reliability for projects requiring byte-perfect output.

## Current Features

### Supported Frameworks

`net9.0;net8.0;net7.0;net6.0;net5.0;netcoreapp3.1;netstandard2.1;netstandard2.0;net48;net47;net46;net45;net40;net35`

### Compression Streams

All compression streams inherit from a CompressionStream class that provides common features and behaviour. 

- Brotli v1.1.0 (From DotNet 9.0)
- LZ4 v1.9.4 (From 7Zip-mcmilk)
- Lzma v24.7.0 (From 7Zip-mcmilk)
- Lzma2 v24.7.0 (From 7Zip-mcmilk)
- Fast-Lzma2 v1.0.1 (From 7Zip-mcmilk)
- ZLib v1.3.1 [GZip, ZLib, Deflate] (From DotNet 8.0)
- ZLib-NG v2.2.1 [GZip, ZLib, Deflate] (From DotNet 9.0)
- ZStd v1.5.6 (From 7Zip-mcmilk)

 *!! The Fast-Lzma2 dotnet interop requires more work, certain configs don't seem correct and streaming is not fully implemented*

### Hashing

Hashes inherit from HashingAlgorithm allowing them to be use with CryptoStream for standard dotnet use.

- Blake3, Blake2sp
- MD5, MD4, MD2
- SHA1
- SHA2 [SHA256, SHA384, SHA512]
- SHA3 [SHA3-224, SHA3-256, SHA3-384, SHA3-512]
- XXHash [XXH32, XXH64]

## Continuous Integration (CI) Status

A comprehensive list of test statuses for various platforms is available below. For a quick summary: GrindCore is being actively tested across major platforms including Linux ARM64, Linux ARM, Linux x64, macOS x64, macOS ARM64, Windows x64, and Windows x86. Windows ARM64 builds, but there is no test platform available.

### Detailed Test Status

| Platform            | Unit Test Status                                                                                      |
|---------------------|-------------------------------------------------------------------------------------------------------|
| **Linux ARM64**     | ![Linux ARM64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_arm64)   |
| **Linux ARM**       | ![Linux ARM Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_arm)       |
| **Linux x64**       | ![Linux x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_x64)       |
| **macOS x64**       | ![macOS x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_osx_x64)         |
| **macOS ARM64**     | ![macOS ARM64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_osx_arm64)     |
| **Windows x64**     | ![Windows x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_win_x64)       |
| **Windows x86**     | ![Windows x86 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_win_x86)       |
| **Windows ARM64**   | Builds, no test platform available |

## Key Project Integrations

GrindCore integrates robust solutions from several key projects:

- **[dotnet Runtime GitHub Repository](https://github.com/dotnet/runtime):**
  - Provides a foundation with multiplatform C compilation based on CMake and C, ensuring seamless integration across different platforms.
  - Supplies zlib/deflate and Brotli from the dotnet 8 code, combined with C# wrappers, to offer efficient and reliable compression algorithms.
- **[7zip mcmilk GitHub Repository](https://github.com/mcmilk/7-Zip-zstd):**
  - Contributes a comprehensive suite of hash functions, including SHA-1, SHA-2, SHA-3, MD2, MD4, MD5, and XXHash (32 and 64). More compression and hashing algorithms will be ported, benefiting from a uniform Make project structure that simplifies integration.

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

## Conclusion

GrindCore is on a journey to create a more reliable and efficient compression solution for dotnet. The community's contributions and collaboration are welcomed.

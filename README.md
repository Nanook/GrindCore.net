# GrindCore.net
Managed c# dotnet wrapper around Native GrindCore library - https://github.com/Nanook/GrindCore

This library is in the early stages of development. There may be many breaking changes over the following months.

## Current Features

### Compression

- ZLib, Deflate
- Brotli
- Zip

### Hashing

- Blake3, Blake2sp
- MD5, MD4, MD2
- SHA1
- SHA2 (SHA256, SHA384, SHA512)
- SHA3 (SHA3-224, SHA3-256, SHA3-384, SHA3-512)
- XXHash (XXH32, XXH64)

Lots more functionality to be added

## Continuous Integration (CI) Status

| Platform            | Unit Test Status                                                                                      |
|---------------------|-------------------------------------------------------------------------------------------------------|
| **Linux ARM64**     | ![Linux ARM64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_arm64)   |
| **Linux ARM**       | ![Linux ARM Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_arm)       |
| **Linux x64**       | ![Linux x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_linux_x64)       |
| **macOS x64**       | ![macOS x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_osx_x64)         |
| **macOS ARM64**     | ![macOS ARM64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_osx_arm64)     |
| **Windows x64**     | ![Windows x64 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_win_x64)       |
| **Windows x86**     | ![Windows x86 Status](https://github.com/Nanook/GrindCore.net/actions/workflows/test.yaml/badge.svg?event=push&job=test_win_x86)       |
| **Windows ARM64**   | No test available                                                                                     |


# GrindCore v0.10.0

## Bug Fixes

### LZMA / LZMA2 — decode truncated after short source reads
`LzmaStream` and `Lzma2Stream` stopped decoding prematurely when the underlying source returned fewer bytes than requested. This is normal behaviour for network and pipe streams, and is exactly what SharpCompress's `BufferedSubStream` does at every 128 KiB refill — meaning every 7z archive read through GrindCore.SharpCompress was affected. Output appeared to succeed but was silently truncated. Both streams now read until the codec signals completion, regardless of how the source delivers bytes.

### LZMA2 — `Flush()` discarded all subsequent data
In solid mode, calling `Lzma2Stream.Flush()` latched the encoder at end-of-input. All data written after the first flush was silently lost. `Flush()` is now a proper sync flush: the stream up to the flush point is independently decodable, the encoder dictionary is preserved, and subsequent writes continue from where they left off. `Complete()` behaviour is unchanged.

### ZStd — encoding errors were silently swallowed
`ZStdEncoder` (both v1.5.7 and v1.5.2) ignored the return values of `CompressStream`, `FlushStream`, and `EndStream`. On failure — most commonly out-of-memory at level 22 on 32-bit systems — the encoder produced empty or corrupt output and reported success. With v1.5.2, a race condition in the failed session could crash the process. Encoding errors now throw `"ZStd compression failed: <reason>"`, consistent with the seekable encoder.

### LZMA2 — out-of-memory caused a process crash on 32-bit
`Lzma2Encoder` did not check the return value of its native `Prepare` call, which allocates the match finder and dictionary (~370 MiB at level 5). On a 32-bit process short of address space, the subsequent encode call wrote through a bad pointer and crashed the host. Now throws `"Failed to prepare LZMA2 encoder <code>"` with a clean teardown.

---

## New Features

### LZMA2 multi-threaded encoding
`ThreadCount` on `CompressionOptions` now works for `Lzma2Stream` and `Lzma2Block`. Previously it was silently ignored.

- `ThreadCount > 1` with a `BlockSize` splits each block into `BlockSize / ThreadCount` 7-Zip blocks and compresses them in parallel. Measured throughput on win-x64, 64 MiB at level 5: **4.7 → 25.3 MiB/s** at 8 threads with a 32 MiB block size.
- `ThreadCount > 1` without a `BlockSize` uses automatic blocking — one dictionary per block.
- `BlockSize = -1` forces solid mode regardless of thread count.
- `LzmaStream` remains single-threaded.

Output is byte-identical to the previous release for the default `Lzma2Stream` and for `LzmaStream`. Streams with `BlockSize` set or `ThreadCount > 1` produce standard LZMA2 that any conforming decoder handles.

### ZStd block frame — `WriteContentSize` option
`CompressionDictionaryOptions.WriteContentSize` (bool?) controls whether the content size is embedded in the ZStd frame header for block compression:

| Value | Behaviour |
|-------|-----------|
| `null` (default) | Unchanged — uses the plain block API as before |
| `true` | Forces content size into the header (zstd's default) |
| `false` | Omits content size — frames between 256 B and 64 KiB are 1 byte smaller, matching the output of NDZ and python-zstandard |

Applies to `ZStdBlock` with both v1.5.7 and v1.5.2, with and without a dictionary. Stream compression is unaffected.

---

## Notes

- Requires the updated GrindCore native build (`audit/native-fixes`, GrindCore commit aff9802 or later). The new ZStd block exports (`CompressBlockAdvanced`, `CompressBlockWithDictAdvanced`) are not present in GrindCore 0.9.0 native libraries; the 10 new `ZStdBlockContentSizeTests` will throw `EntryPointNotFoundException` with older natives, and all other tests continue to pass.
- The LZMA2 threading and flush fixes are managed-only; no native change is required for those.

---

## Support

If you find GrindCore useful, you can support me on Ko-fi:
- **Sponsor Me**: [ko-fi.com/nanook_/tiers](http://ko-fi.com/nanook_/tiers)
- **Buy Me a Beer** 🍺: [ko-fi.com/nanook_](https://ko-fi.com/nanook_)

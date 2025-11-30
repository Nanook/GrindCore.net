Buffer size policy and rationale

Overview

This document explains the buffer-sizing behavior implemented in `src/CompressionStream.cs` and the related LZMA stream logic in `src/Lzma/LzmaStream.cs`.

Goal

- Avoid pathological buffer sizes that trigger native encoder/decoder bugs (observed in LZMA/LZMA2/FastLzma2).
- Keep predictable, reasonable buffer allocations without dramatically changing per-algorithm compression quality.
- Provide a stable base so tests run reliably and maintainers can reason about changes.

What changed (summary)

- A conservative list of "allowed" buffer sizes is defined and used when we "snap" sizes.
- Implicit (no `CompressionOptions.BufferSize` provided): the requested size is rounded up (ceiling) to the next allowed size.
- Explicit `BufferSize` provided by caller:
  - For LZMA-family (Lzma, Lzma2, FastLzma2): when the provided value is very close to an allowed size (<= 1 KiB difference) we snap to the nearest allowed value (prefer lower on tie). This addresses off-by-one values such as `0x10001` while avoiding wider changes that harm compression.
  - For other algorithms: explicit sizes are preserved as-is (to avoid degrading compression quality).
- After snapping we ensure:
  - `bufferCapacity >= MinimumBufferThreshold` (algorithm minimums, e.g., LZMA 64 KiB).
  - The `BufferThreshold` (when the stream decides to push data to encoder) is clamped so it never exceeds the actual `bufferCapacity`.

Allowed sizes

The list (see `src/CompressionStream.cs`) is:

- 64 KiB
- 128 KiB
- 256 KiB
- 512 KiB
- 1 MiB
- 2 MiB
- 4 MiB
- 8 MiB
- 16 MiB
- 32 MiB
- 64 MiB
- 128 MiB
- 256 MiB

Rationale: these are conservative, sensible allocations that avoid rare native encoder edge-cases while supporting a broad range of block-based encoders.

Why LZMA-family needs special handling

- LZMA/LZMA2/Fast-LZMA2 are block-based and sensitive to dictionary size, blockSize and exact input partitioning. Small changes in the buffer (or where the encoder is asked to flush) change block boundaries and therefore compressed output.
- The original failure was when `0x10001` (65537) was supplied: the encoder produced different block partitioning and the decompressed output/hash didn't line up.
- Fixes implemented:
  - `LzmaStream` now uses the actual allocated internal buffer capacity (the `CompressionStream` buffer) when selecting encoder dictionary/block sizes so encoder and stream align.
  - Small off-by-one explicit sizes for LZMA-family are snapped to nearest allowed size (<= 1 KiB delta) to protect against pathological native behavior.

Why some tests can still change compressed size

- Block-based encoders (LZMA2/Fast-LZMA2) emit per-block headers and partition input differently depending on the block/dictionary size. Even rounding up a buffer can increase or decrease compression slightly.
- Fast-LZMA2 (multithreaded) also partitions work across threads — changing block size affects partitioning and ordering.

Recommendations for deterministic compressed output

If you need byte-for-byte identical compressed blobs across configurations:

- Set explicit options that control the encoder behavior directly when creating streams for LZMA-family:
  - `CompressionOptions.Dictionary.DictionarySize`
  - `CompressionOptions.BlockSize` (where applicable)

Set those to canonical values that the tests expect (for example, the stream's snapped buffer capacity). Alternatively add `CompressionOptions.DisableBufferSnapping = true` to force exact preservation of user-provided sizes (not implemented by default in this branch but recommended as a future option).

Testing guidance

- Prefer asserting decompressed data equality (hashes) rather than compressed size, unless the encoder configuration is explicitly controlled and part of the test contract.
- For tests that assert compressed size, ensure they also set explicit dictionary/block settings so they are deterministic.

Where to change behavior

- `src/CompressionStream.cs` — central snap/threshold/allocation logic. To change the allowed sizes edit the `allowedSizes` array.
- `src/Lzma/LzmaStream.cs` — the LZMA stream uses the allocated internal buffer for dict size. If you want to apply similar alignment for LZMA2/FastLzma2, do it in their stream/encoder classes.

Trade-offs considered

- Snapping down (nearest) reduces allocation but can hurt compression. Snapping up (ceiling) preserves or improves compression but increases memory.
- Current compromise: implicit sizes round up (ceiling) to avoid odd implicit allocations; explicit sizes are preserved for non-LZMA algorithms; LZMA gets small-delta snapping to handle pathological cases without broadly degrading compression.

Future ideas

- Add `CompressionOptions.DisableBufferSnapping` so callers/tests can opt out.
- Add `CompressionOptions.ForceBufferAlignment` with explicit alignment policy for encoders that care (e.g., `AlignTo=64KiB` or `UseBufferAsDictionary=true`).
- Expose the `allowedSizes` list via configuration to avoid code changes for future experiments.


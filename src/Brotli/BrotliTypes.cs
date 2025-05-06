


using System;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>
    /// Mode - BrotliEncoderMode enumerates all available values.
    /// Quality - The main compression speed-density lever. The higher the quality, the slower the compression. Range is from ::BROTLI_MIN_QUALITY to::BROTLI_MAX_QUALITY.
    /// LGWin - Recommended sliding LZ77 window size. Encoder may reduce this value, e.g. if input is much smaller than window size. Range is from BROTLI_MIN_WINDOW_BITS to BROTLI_MAX_WINDOW_BITS.
    /// LGBlock - Recommended input block size. Encoder may reduce this value, e.g. if input is much smaller than window size. Range is from BROTLI_MIN_INPUT_BLOCK_BITS to BROTLI_MAX_INPUT_BLOCK_BITS. Bigger input block size allows better compression, but consumes more memory.
    /// LCModeling-  Flag that affects usage of "literal context modeling" format feature. This flag is a "decoding-speed vs compression ratio" trade-off.
    /// SizeHint - Estimated total input size for all BrotliEncoderCompressStream calls. The default value is 0, which means that the total input size is unknown.
    /// </summary>
    internal enum BrotliEncoderParameter
    {
        Mode,
        Quality,
        LGWin,
        LGBlock,
        LCModeling,
        SizeHint
    }
    
    /// <summary>
    /// Process - Process input. Encoder may postpone producing output, until it has processed enough input.
    /// Flush - Produce output for all processed input.  Actual flush is performed when input stream is depleted and there is enough space in output stream.
    /// Finish - Finalize the stream. Adding more input data to finalized stream is impossible.
    /// EmitMetadata - Emit metadata block to stream. Stream is soft-flushed before metadata block is emitted. Metadata bloc MUST be no longer than 16MiB.
    /// </summary>
    internal enum BrotliEncoderOperation
    {
        Process,
        Flush,
        Finish,
        EmitMetadata
    }

    //
    // Summary:
    //     Defines the values that can be returned from span-based operations that support
    //     processing of input contained in multiple discontiguous buffers.
    internal enum OperationStatus
    {
        //
        // Summary:
        //     The entire input buffer has been processed and the operation is complete.
        Done = 0,
        //
        // Summary:
        //     The input is partially processed, up to what could fit into the destination buffer.
        //     The caller can enlarge the destination buffer, slice the buffers appropriately,
        //     and retry.
        DestinationTooSmall = 1,
        //
        // Summary:
        //     The input is partially processed, up to the last valid chunk of the input that
        //     could be consumed. The caller can stitch the remaining unprocessed input with
        //     more data, slice the buffers appropriately, and retry.
        NeedMoreData = 2,
        //
        // Summary:
        //     The input contained invalid bytes which could not be processed. If the input
        //     is partially processed, the destination contains the partial result. This guarantees
        //     that no additional data appended to the input will make the invalid sequence
        //     valid.
        InvalidData = 3
    }

    internal sealed class SafeBrotliEncoderHandle : SafeHandle
    {
        public SafeBrotliEncoderHandle() : base(IntPtr.Zero, true) { }

        protected override bool ReleaseHandle()
        {
            if (Version.Index == 0)
                Interop.Brotli.DN9_BRT_v1_1_0_EncoderDestroyInstance(handle);
            return true;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public CompressionVersion Version { get; set; }
    }

    internal sealed class SafeBrotliDecoderHandle : SafeHandle
    {
        public SafeBrotliDecoderHandle() : base(IntPtr.Zero, true) { }

        protected override bool ReleaseHandle()
        {
            if (Version.Index == 0)
                Interop.Brotli.DN9_BRT_v1_1_0_DecoderDestroyInstance(handle);
            return true;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public CompressionVersion Version { get; set; }
    }
}

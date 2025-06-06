using System;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>
    /// Specifies Brotli encoder parameters.
    /// <list type="bullet">
    /// <item><b>Mode</b> - BrotliEncoderMode enumerates all available values.</item>
    /// <item><b>Quality</b> - The main compression speed-density lever. The higher the quality, the slower the compression. Range is from <c>BROTLI_MIN_QUALITY</c> to <c>BROTLI_MAX_QUALITY</c>.</item>
    /// <item><b>LGWin</b> - Recommended sliding LZ77 window size. Encoder may reduce this value, e.g. if input is much smaller than window size. Range is from <c>BROTLI_MIN_WINDOW_BITS</c> to <c>BROTLI_MAX_WINDOW_BITS</c>.</item>
    /// <item><b>LGBlock</b> - Recommended input block size. Encoder may reduce this value, e.g. if input is much smaller than window size. Range is from <c>BROTLI_MIN_INPUT_BLOCK_BITS</c> to <c>BROTLI_MAX_INPUT_BLOCK_BITS</c>. Bigger input block size allows better compression, but consumes more memory.</item>
    /// <item><b>LCModeling</b> - Flag that affects usage of "literal context modeling" format feature. This flag is a "decoding-speed vs compression ratio" trade-off.</item>
    /// <item><b>SizeHint</b> - Estimated total input size for all BrotliEncoderCompressStream calls. The default value is 0, which means that the total input size is unknown.</item>
    /// </list>
    /// </summary>
    internal enum BrotliEncoderParameter
    {
        /// <summary>Specifies the encoder mode.</summary>
        Mode,
        /// <summary>Specifies the compression quality (speed-density lever).</summary>
        Quality,
        /// <summary>Specifies the sliding LZ77 window size.</summary>
        LGWin,
        /// <summary>Specifies the input block size.</summary>
        LGBlock,
        /// <summary>Specifies the literal context modeling flag.</summary>
        LCModeling,
        /// <summary>Specifies the estimated total input size.</summary>
        SizeHint
    }

    /// <summary>
    /// Specifies the operation to perform with the Brotli encoder.
    /// <list type="bullet">
    /// <item><b>Process</b> - Process input. Encoder may postpone producing output, until it has processed enough input.</item>
    /// <item><b>Flush</b> - Produce output for all processed input. Actual flush is performed when input stream is depleted and there is enough space in output stream.</item>
    /// <item><b>Finish</b> - Finalize the stream. Adding more input data to finalized stream is impossible.</item>
    /// <item><b>EmitMetadata</b> - Emit metadata block to stream. Stream is soft-flushed before metadata block is emitted. Metadata block MUST be no longer than 16MiB.</item>
    /// </list>
    /// </summary>
    internal enum BrotliEncoderOperation
    {
        /// <summary>Process input. Encoder may postpone producing output until it has processed enough input.</summary>
        Process,
        /// <summary>Produce output for all processed input. Actual flush is performed when input stream is depleted and there is enough space in output stream.</summary>
        Flush,
        /// <summary>Finalize the stream. Adding more input data to finalized stream is impossible.</summary>
        Finish,
        /// <summary>Emit metadata block to stream. Stream is soft-flushed before metadata block is emitted. Metadata block MUST be no longer than 16MiB.</summary>
        EmitMetadata
    }

    /// <summary>
    /// Defines the values that can be returned from span-based operations that support processing of input contained in multiple discontiguous buffers.
    /// </summary>
    internal enum OperationStatus
    {
        /// <summary>
        /// The entire input buffer has been processed and the operation is complete.
        /// </summary>
        Done = 0,
        /// <summary>
        /// The input is partially processed, up to what could fit into the destination buffer.
        /// The caller can enlarge the destination buffer, slice the buffers appropriately, and retry.
        /// </summary>
        DestinationTooSmall = 1,
        /// <summary>
        /// The input is partially processed, up to the last valid chunk of the input that could be consumed.
        /// The caller can stitch the remaining unprocessed input with more data, slice the buffers appropriately, and retry.
        /// </summary>
        NeedMoreData = 2,
        /// <summary>
        /// The input contained invalid bytes which could not be processed. If the input is partially processed, the destination contains the partial result.
        /// This guarantees that no additional data appended to the input will make the invalid sequence valid.
        /// </summary>
        InvalidData = 3
    }

    /// <summary>
    /// Represents a safe handle for a native Brotli encoder instance.
    /// </summary>
    internal sealed class SafeBrotliEncoderHandle : SafeHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeBrotliEncoderHandle"/> class.
        /// </summary>
        public SafeBrotliEncoderHandle() : base(IntPtr.Zero, true) { }

        /// <summary>
        /// Releases the native Brotli encoder instance.
        /// </summary>
        /// <returns><c>true</c> if the handle was released successfully; otherwise, <c>false</c>.</returns>
        protected override bool ReleaseHandle()
        {
            if (Version.Index == 0)
                Interop.Brotli.DN9_BRT_v1_1_0_EncoderDestroyInstance(handle);
            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the handle is invalid.
        /// </summary>
        public override bool IsInvalid => handle == IntPtr.Zero;

        /// <summary>
        /// Gets or sets the compression version associated with this handle.
        /// </summary>
        public CompressionVersion Version { get; set; }
    }

    /// <summary>
    /// Represents a safe handle for a native Brotli decoder instance.
    /// </summary>
    internal sealed class SafeBrotliDecoderHandle : SafeHandle
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SafeBrotliDecoderHandle"/> class.
        /// </summary>
        public SafeBrotliDecoderHandle() : base(IntPtr.Zero, true) { }

        /// <summary>
        /// Releases the native Brotli decoder instance.
        /// </summary>
        /// <returns><c>true</c> if the handle was released successfully; otherwise, <c>false</c>.</returns>
        protected override bool ReleaseHandle()
        {
            if (Version.Index == 0)
                Interop.Brotli.DN9_BRT_v1_1_0_DecoderDestroyInstance(handle);
            return true;
        }

        /// <summary>
        /// Gets a value indicating whether the handle is invalid.
        /// </summary>
        public override bool IsInvalid => handle == IntPtr.Zero;

        /// <summary>
        /// Gets or sets the compression version associated with this handle.
        /// </summary>
        public CompressionVersion Version { get; set; }
    }
}

namespace Nanook.GrindCore.Brotli
{
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
}

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
}

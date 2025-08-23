namespace Nanook.GrindCore
{
    /// <summary>
    /// Defines the result codes for compression and decompression operations.
    /// </summary>
    public enum CompressionResultCode
    {
        /// <summary>
        /// The operation completed successfully.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The operation failed due to an unknown error.
        /// </summary>
        Error = -1,

        /// <summary>
        /// The destination buffer is too small.
        /// </summary>
        InsufficientBuffer = -2,

        /// <summary>
        /// The source data is corrupted or invalid.
        /// </summary>
        InvalidData = -3,

        /// <summary>
        /// Invalid parameters were provided.
        /// </summary>
        InvalidParameter = -4,

        /// <summary>
        /// The operation is not supported.
        /// </summary>
        NotSupported = -5
    }
}
namespace Nanook.GrindCore.FastLzma2
{
    /// <summary>
    /// Represents an input buffer for Fast-LZMA2 operations.
    /// </summary>
    internal struct FL2InBuffer
    {
        /// <summary>
        /// Pointer to the start of the input buffer.
        /// </summary>
        public nint src;

        /// <summary>
        /// Size of the input buffer in bytes.
        /// </summary>
        public nuint size;

        /// <summary>
        /// Position where reading stopped. Will be updated. Necessarily 0 &lt;= pos &lt;= size.
        /// </summary>
        public nuint pos;
    }

    /// <summary>
    /// Represents an output buffer for Fast-LZMA2 operations.
    /// </summary>
    internal struct FL2OutBuffer
    {
        /// <summary>
        /// Pointer to the start of the output buffer.
        /// </summary>
        public nint dst;

        /// <summary>
        /// Size of the output buffer in bytes.
        /// </summary>
        public nuint size;

        /// <summary>
        /// Position where writing stopped. Will be updated. Necessarily 0 &lt;= pos &lt;= size.
        /// </summary>
        public nuint pos;
    }

    /// <summary>
    /// Represents a dictionary buffer for Fast-LZMA2 operations.
    /// </summary>
    internal struct FL2DictBuffer
    {
        /// <summary>
        /// Pointer to the start of the available dictionary buffer.
        /// </summary>
        public nint dst;

        /// <summary>
        /// Size of the remaining dictionary in bytes.
        /// </summary>
        public nuint size;
    }

    /// <summary>
    /// Represents a compressed data buffer for Fast-LZMA2 operations.
    /// </summary>
    internal struct FL2cBuffer
    {
        /// <summary>
        /// Pointer to the start of the compressed data.
        /// </summary>
        public nint src;

        /// <summary>
        /// Size of the compressed data in bytes.
        /// </summary>
        public nuint size;
    }
}

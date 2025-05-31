namespace Nanook.GrindCore.FastLzma2
{
    internal struct FL2InBuffer
    {
        /// <summary>
        /// start of input _outBuffer
        /// </summary>
        public nint src;

        /// <summary>
        /// size of input _outBuffer
        /// </summary>
        public nuint size;

        /// <summary>
        /// position where reading stopped. Will be updated. Necessarily 0 <= pos <= size
        /// </summary>
        public nuint pos;
    }

    internal struct FL2OutBuffer
    {
        /// <summary>
        /// start of output _outBuffer
        /// </summary>
        public nint dst;

        /// <summary>
        /// size of output _outBuffer
        /// </summary>
        public nuint size;

        /// <summary>
        /// position where writing stopped. Will be updated. Necessarily 0 <= pos <= size
        /// </summary>
        public nuint pos;
    }

    internal struct FL2DictBuffer
    {
        /// <summary>
        /// start of available dict _outBuffer
        /// </summary>
        public nint dst;

        /// <summary>
        /// size of dict remaining
        /// </summary>
        public nuint size;
    }

    internal struct FL2cBuffer
    {
        /// <summary>
        /// start of compressed data
        /// </summary>
        public nint src;

        /// <summary>
        /// size of compressed data
        /// </summary>
        public nuint size;
    }
}
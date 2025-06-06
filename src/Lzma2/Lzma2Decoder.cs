using System;
using static Nanook.GrindCore.Interop.Lzma;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Represents information about a single LZMA2 sub-block.
    /// </summary>
    internal struct Lzma2BlockInfo
    {
        /// <summary>
        /// Gets a value indicating whether this block is a terminator.
        /// </summary>
        public bool IsTerminator;
        /// <summary>
        /// Gets a value indicating whether this block is a control block.
        /// </summary>
        public bool IsControlBlock;
        /// <summary>
        /// Gets a value indicating whether this block initializes the decoder state.
        /// </summary>
        public bool InitState;
        /// <summary>
        /// Gets a value indicating whether this block initializes the decoder properties.
        /// </summary>
        public bool InitProp;
        /// <summary>
        /// Gets the property byte for this block, if present.
        /// </summary>
        public byte Prop;
        /// <summary>
        /// Gets the uncompressed size of this block.
        /// </summary>
        public int UncompressedSize;
        /// <summary>
        /// Gets the compressed size of this block.
        /// </summary>
        public int CompressedSize;
        /// <summary>
        /// Gets the size of the compressed header for this block.
        /// </summary>
        public int CompressedHeaderSize;
        /// <summary>
        /// Gets the total block size (compressed size plus header size).
        /// </summary>
        public int BlockSize => CompressedSize + CompressedHeaderSize;
        /// <summary>
        /// Gets a value indicating whether this is a new block with control, state, and property initialization.
        /// </summary>
        public bool NewBlock => IsControlBlock && InitState && InitProp;
    }

    /// <summary>
    /// Provides a decoder for LZMA2-compressed data, supporting block-based decompression.
    /// </summary>
    internal unsafe class Lzma2Decoder : IDisposable
    {
        private CLzma2Dec _decCtx;

        /// <summary>
        /// Gets the LZMA2 property byte used for decoding.
        /// </summary>
        public byte Properties { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lzma2Decoder"/> class with the specified LZMA2 property byte.
        /// </summary>
        /// <param name="properties">The LZMA2 property byte required for decoding.</param>
        /// <exception cref="Exception">Thrown if the decoder context cannot be allocated.</exception>
        public Lzma2Decoder(byte properties)
        {
            Properties = properties;
            createDecoder(properties);
        }

        /// <summary>
        /// Allocates and initializes the decoder context with the specified property byte.
        /// </summary>
        /// <param name="props">The LZMA2 property byte.</param>
        /// <exception cref="Exception">Thrown if the decoder context cannot be allocated.</exception>
        private void createDecoder(byte props)
        {
            _decCtx = new CLzma2Dec() { decoder = new CLzmaDec() };
            int res = SZ_Lzma2_v24_07_Dec_Allocate(ref _decCtx, props);

            if (res != 0)
                throw new Exception($"Allocate Error {res}");
        }

        /// <summary>
        /// Reads information about a sub-block from the input data at the specified offset.
        /// </summary>
        /// <param name="inData">The input data array.</param>
        /// <param name="inOffset">The offset within the input data array.</param>
        /// <returns>A <see cref="Lzma2BlockInfo"/> structure describing the sub-block.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="inOffset"/> is outside the bounds of the data array.</exception>
        public Lzma2BlockInfo ReadSubBlockInfo(byte[] inData, ulong inOffset)
        {
            if (inOffset >= (ulong)inData.LongLength)
                throw new ArgumentOutOfRangeException(nameof(inOffset), "Position is outside the bounds of the data array.");

            byte b = inData[inOffset];
            bool isControlBlock = (b & 0b10000000) != 0;
            bool initProp = (b & 0b01000000) != 0 && isControlBlock;
            bool initState = (b & 0b00100000) != 0 && isControlBlock;

            return new Lzma2BlockInfo()
            {
                IsTerminator = b == 0,
                IsControlBlock = isControlBlock,
                InitProp = initProp,
                InitState = initState,
                Prop = initProp ? inData[inOffset + 5] : (byte)0,
                UncompressedSize = isControlBlock || b == 1 ? ((b & 0x1F) << 16) + (inData[inOffset + 1] << 8) + inData[inOffset + 2] + 1 : 0,
                CompressedSize = isControlBlock ? (inData[inOffset + 3] << 8) + inData[inOffset + 4] + 1 : 0,
                CompressedHeaderSize = isControlBlock ? (initProp ? 6 : 5) : 0
            };
        }

        /// <summary>
        /// Sets the LZMA2 property byte for the decoder, reinitializing the decoder context.
        /// </summary>
        /// <param name="props">The LZMA2 property byte.</param>
        /// <exception cref="Exception">Thrown if the decoder context cannot be allocated.</exception>
        public void SetProps(byte props)
        {
            SZ_Lzma2_v24_07_Dec_Free(ref _decCtx);
            createDecoder(props);
        }

        /// <summary>
        /// Initializes the decoder state.
        /// </summary>
        public void SetState()
        {
            SZ_Lzma2_v24_07_Dec_Init(ref _decCtx);
        }

        /// <summary>
        /// Decodes LZMA2-compressed data from the input buffer into the output buffer.
        /// </summary>
        /// <param name="inData">The input buffer containing compressed data.</param>
        /// <param name="inSize">Reference to the number of bytes available to read from the input buffer. Updated with the number of bytes consumed.</param>
        /// <param name="outData">The output buffer to write decompressed data to.</param>
        /// <param name="outSize">The number of bytes available to write to the output buffer.</param>
        /// <param name="status">Outputs the status of the decompression operation.</param>
        /// <returns>The number of bytes written to the output buffer.</returns>
        /// <exception cref="Exception">Thrown if decompression fails.</exception>
        public int DecodeData(CompressionBuffer inData, ref int inSize, CompressionBuffer outData, int outSize, out int status)
        {
            ulong inSz = (ulong)inSize;
            ulong outSz = (ulong)outSize;
            fixed (byte* outPtr = outData.Data)
            fixed (byte* inPtr = inData.Data)
            fixed (int* statusPtr = &status)
            {
                *&outPtr += outData.Size;
                *&inPtr += inData.Pos;
                int res = SZ_Lzma2_v24_07_Dec_DecodeToBuf(ref _decCtx, outPtr, &outSz, inPtr, &inSz, 0, statusPtr);
                if (res != 0)
                    throw new Exception($"Decode Error {res}");

                outData.Write((int)outSz);
                inData.Read((int)inSz);
                inSize = (int)inSz;
                return (int)outSz;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="Lzma2Decoder"/>.
        /// </summary>
        public void Dispose()
        {
            SZ_Lzma2_v24_07_Dec_Free(ref _decCtx);
        }
    }
}


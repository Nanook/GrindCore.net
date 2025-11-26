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
        /// <summary>Indicates whether this block is a terminator.</summary>
        public bool IsTerminator;
        /// <summary>Indicates whether this block contains compressed data.</summary>
        public bool IsCompressed;
        /// <summary>Indicates whether this block initializes the decoder state.</summary>
        public bool InitState;
        /// <summary>Indicates whether this block initializes the decoder properties.</summary>
        public bool InitProp;
        /// <summary>The property byte for this block, if present.</summary>
        public byte Prop;
        /// <summary>The uncompressed size of this block.</summary>
        public int UncompressedSize;
        /// <summary>The compressed payload size of this block.</summary>
        public int CompressedSize;
        /// <summary>The size of the header (including dictionary) for this block.</summary>
        public int HeaderSize;
        /// <summary>The total block size (header size plus compressed payload size).</summary>
        public int BlockSize => CompressedSize + HeaderSize;
        /// <summary>Indicates whether this is a new block with control, state, and property initialization.</summary>
        public bool NewBlock => IsCompressed && InitState && InitProp;
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
            int res = SZ_Lzma2_v25_01_Dec_Allocate(ref _decCtx, props);

            if (res != 0)
                throw new Exception($"Allocate Error {res}");
        }

        /// <summary>
        /// Determines the number of header bytes to read for an LZMA2 block, based on the first control byte.
        /// </summary>
        /// <param name="control">The first byte of the LZMA2 block header.</param>
        /// <returns>The minimal number of header bytes to read before the payload.</returns>
        public int GetHeaderSize(byte control)
        {
            if (control == 0)
                return 1; // terminator marker only

            if ((control & 0x80) == 0)
                return 3; // uncompressed block: 1 control + 2 size bytes

            // compressed block
            // base header is 5 bytes: 1 control + 2 uncompressed size + 2 compressed size
            // if properties are reset (bit 0x40), at least one extra property byte follows -> 6 bytes minimal
            // Do NOT assume the 4-byte dictionary size exists here (varies by container). Caller will read more if needed.
            return (control & 0x40) != 0 ? 6 : 5;
        }

        /// <summary>
        /// Reads information about a sub-block from the input data at the specified offset.
        /// This method is tolerant of headers that are present only partially in the buffer:
        /// it uses the provided <paramref name="size"/> (number of header bytes currently available).
        /// </summary>
        /// <param name="inData">The input data array.</param>
        /// <param name="inOffset">The offset within the input data array.</param>
        /// <param name="size">The number of header bytes currently available at the offset.</param>
        /// <returns>A <see cref="Lzma2BlockInfo"/> structure describing the sub-block.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="inOffset"/> is outside the bounds of the data array or if the provided header bytes are insufficient to parse mandatory fields.</exception>
        public Lzma2BlockInfo ReadSubBlockInfo(byte[] inData, ulong inOffset, int size)
        {
            if (inOffset >= (ulong)inData.LongLength)
                throw new ArgumentOutOfRangeException(nameof(inOffset));

            // Need at least 1 byte for control
            if (size < 1)
                throw new ArgumentOutOfRangeException(nameof(size), "Not enough data to read control byte");

            byte b = inData[inOffset];
            bool hasComp = (b & 0x80) != 0;
            bool initProp = (b & 0x40) != 0;
            bool initState = (b & 0x20) != 0;

            // For size fields we need at least bytes at offsets +1 and +2 (uncompressed size)
            if (size < 3)
                throw new ArgumentOutOfRangeException(nameof(size), "Incomplete header: need at least 3 bytes to read size fields");

            int uSize = ((b & 0x1F) << 16 | (inData[inOffset + 1] << 8) | inData[inOffset + 2]) + 1;

            int cSize;
            if (!hasComp)
            {
                // uncompressed block: compressed payload size equals uncompressed size
                cSize = uSize;
            }
            else
            {
                // For compressed blocks we need compressed-size bytes at offsets +3 and +4
                if (size < 5)
                    throw new ArgumentOutOfRangeException(nameof(size), "Incomplete header: need at least 5 bytes for compressed-size");
                cSize = ((inData[inOffset + 3] << 8) | inData[inOffset + 4]) + 1;
            }

            // Determine the header size we actually have / should expect.
            // Minimal header length:
            int minimalHeader = hasComp ? 5 : 3;
            if (hasComp && initProp)
                minimalHeader = 6; // base 5 + prop byte

            // If the buffer actually contains the 4-byte dict size (10 total) treat that as full header.
            int fullHeader = hasComp && initProp ? 10 : minimalHeader;

            // Decide reported HeaderSize:
            // - If caller provided fewer header bytes than minimal required, throw earlier.
            // - If caller provided at least fullHeader bytes (i.e. 10), report fullHeader.
            // - Otherwise report the minimal header (6 or 5 or 3).
            int reportedHeaderSize = minimalHeader;
            if (size >= fullHeader)
                reportedHeaderSize = fullHeader;

            // Read property byte only if present in the provided header bytes (or present in full header)
            byte prop = 0;
            if (initProp && size >= 6)
                prop = inData[inOffset + 5];
            else if (initProp && size < 6)
                prop = 0; // property not yet available; caller should read more

            return new Lzma2BlockInfo
            {
                IsTerminator = b == 0,
                IsCompressed = hasComp,
                InitProp = initProp,
                InitState = initState,
                Prop = prop,
                UncompressedSize = uSize,
                CompressedSize = cSize,
                HeaderSize = reportedHeaderSize
            };
        }

        /// <summary>
        /// Sets the LZMA2 property byte for the decoder, reinitializing the decoder context.
        /// </summary>
        /// <param name="props">The LZMA2 property byte.</param>
        /// <exception cref="Exception">Thrown if the decoder context cannot be allocated.</exception>
        public void SetProps(byte props)
        {
            SZ_Lzma2_v25_01_Dec_Free(ref _decCtx);
            createDecoder(props);
        }

        /// <summary>
        /// Initializes the decoder state.
        /// </summary>
        public void SetState()
        {
            SZ_Lzma2_v25_01_Dec_Init(ref _decCtx);
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
            outData.Tidy(); //ensure all the space is at the end making _buffer.AvailableWrite safe for interop

            ulong inSz = (ulong)inSize;
            ulong outSz = (ulong)outSize;
            status = 0; // defensive init
            fixed (byte* outPtr = outData.Data)
            fixed (byte* inPtr = inData.Data)
            fixed (int* statusPtr = &status)
            {
                byte* pOut = outPtr + outData.Size; // Size is writing Pos
                byte* pIn = inPtr + inData.Pos;
                int res = SZ_Lzma2_v25_01_Dec_DecodeToBuf(ref _decCtx, pOut, &outSz, pIn, &inSz, 0, statusPtr);
                if (res != 0)
                    throw new Exception($"Decode Error {res}");

                outData.Write((int)outSz);
                inData.Read((int)inSz);
                inSize = (int)inSz;
                return (int)outSz;
            }
        }

        /// <summary>Releases all resources used by the <see cref="Lzma2Decoder"/>.</summary>
        public void Dispose()
        {
            SZ_Lzma2_v25_01_Dec_Free(ref _decCtx);
        }
    }
}


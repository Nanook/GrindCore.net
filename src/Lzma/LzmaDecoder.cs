using System;
using static Nanook.GrindCore.Interop.Lzma;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Provides a decoder for LZMA-compressed data, supporting block-based decompression.
    /// </summary>
    internal unsafe class LzmaDecoder : IDisposable
    {
        private CLzmaDec _decCtx;

        /// <summary>
        /// Gets the LZMA properties used for decoding.
        /// </summary>
        public byte[] Properties { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LzmaDecoder"/> class with the specified LZMA properties.
        /// </summary>
        /// <param name="properties">The LZMA properties required for decoding.</param>
        /// <exception cref="Exception">Thrown if the decoder context cannot be allocated.</exception>
        public LzmaDecoder(byte[] properties)
        {
            Properties = properties;

            int res;

            _decCtx = new CLzmaDec();
            fixed (byte* outPtr = properties)
            {
                res = SZ_Lzma_v24_07_Dec_Allocate(ref _decCtx, outPtr, (uint)properties.Length);
                SZ_Lzma_v24_07_Dec_Init(ref _decCtx);
            }
            if (res != 0)
                throw new Exception($"Allocate Error {res}");
        }

        /// <summary>
        /// Decodes LZMA-compressed data from the input buffer into the output buffer.
        /// </summary>
        /// <param name="inData">The input buffer containing compressed data.</param>
        /// <param name="readSz">Outputs the number of bytes read from the input buffer.</param>
        /// <param name="outData">The output buffer to write decompressed data to.</param>
        /// <param name="outSize">The number of bytes available to write to the output buffer.</param>
        /// <param name="status">Outputs the status of the decompression operation.</param>
        /// <returns>The number of bytes written to the output buffer.</returns>
        /// <exception cref="Exception">Thrown if decompression fails.</exception>
        public int DecodeData(CompressionBuffer inData, out int readSz, CompressionBuffer outData, int outSize, out int status)
        {
            outData.Tidy(); //ensure all the space is at the end making _buffer.AvailableWrite safe for interop

            // Get properties from DataBlock
            ulong outSz = (ulong)outSize;
            ulong inSz = (ulong)inData.AvailableRead;

            fixed (byte* outPtr = outData.Data) // Pin memory for the output inData
            fixed (byte* inPtr = inData.Data) // Pin memory for the input inData
            fixed (int* statusPtr = &status) // Pin memory for the status
            {
                *&outPtr += outData.Size; //Size is writing Pos
                *&inPtr += inData.Pos;
                // Call the C interop function
                int res = SZ_Lzma_v24_07_Dec_DecodeToBuf(ref _decCtx, outPtr, &outSz, inPtr, &inSz, 0, statusPtr);
                if (res != 0)
                    throw new Exception($"Decode Error {res}");

                readSz = (int)inSz; // Update inSize with the size consumed
                inData.Read(readSz);
                outData.Write((int)outSz);
                return (int)outSz; // Return the size of the output
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="LzmaDecoder"/>.
        /// </summary>
        public void Dispose()
        {
            SZ_Lzma_v24_07_Dec_Free(ref _decCtx);
        }
    }
}


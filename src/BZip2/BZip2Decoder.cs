using System;
using System.IO;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.BZip2;

namespace Nanook.GrindCore.BZip2
{
    /// <summary>
    /// Provides a wrapper around the BZip2 streaming decompression API, supporting transparent
    /// decompression of concatenated ("multi-stream") .bz2 data - bzip2 has documented this
    /// concatenation convention since 0.9.0 (the same way multiple gzip members can be
    /// concatenated and decompress as one logical stream; see <c>DeflateDecoder</c>'s
    /// <c>resetStreamForLeftoverInput</c> for the equivalent gzip-side handling).
    /// </summary>
    internal unsafe sealed class BZip2Decoder : IDisposable
    {
        private static readonly byte[] _bzip2MagicHeader = { (byte)'B', (byte)'Z', (byte)'h' };

        // Must live at a single, never-moving address for its entire lifetime - see
        // Interop.SZ_BZip2_v1_0_8_CompressionContext's doc comment (libbzip2 stores and validates
        // the bz_stream* pointer across calls) and BZip2Encoder's matching field.
        private IntPtr _ctx;
        private readonly int _small;
        private readonly bool _tolerateErrors;
        private bool _finished;
        private bool _nonEmptyInput;
        private bool _isDisposed;

        /// <summary>
        /// Returns true if the end of the (possibly multi-stream) input has been reached.
        /// </summary>
        public bool Finished => _finished;

        /// <summary>
        /// Returns true if any non-empty input has ever been provided.
        /// </summary>
        public bool NonEmptyInput => _nonEmptyInput;

        /// <summary>
        /// Initializes a new instance of the <see cref="BZip2Decoder"/> class.
        /// </summary>
        /// <param name="smallDecompress">
        /// When true, selects bzip2's reduced-memory decompression algorithm (~2.5x less memory, some speed cost).
        /// </param>
        /// <param name="tolerateErrors">
        /// When true, BZ_DATA_ERROR after valid output is treated as end-of-stream rather than
        /// throwing. Used when bzip2 data is embedded in archives where buffer overread may feed
        /// non-bzip2 bytes to the decoder after the real stream end.
        /// </param>
        /// <exception cref="Exception">Thrown if the native decompression context cannot be created.</exception>
        public BZip2Decoder(bool smallDecompress, bool tolerateErrors = false)
        {
            _small = smallDecompress ? 1 : 0;
            _tolerateErrors = tolerateErrors;
            _ctx = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SZ_BZip2_v1_0_8_DecompressionContext)));
            init();
        }

        private void init()
        {
            int result = SZ_BZip2_v1_0_8_CreateDecompressionContext(_ctx, _small);
            if (result != BZ_OK)
                throw new Exception($"Failed to create BZip2 decompression context (error {result})");
        }

        /// <summary>
        /// Finalizer to ensure resources are released.
        /// </summary>
        ~BZip2Decoder()
        {
            dispose(false);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="BZip2Decoder"/>.
        /// </summary>
        public void Dispose()
        {
            dispose(true);
            GC.SuppressFinalize(this);
        }

        private void dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                SZ_BZip2_v1_0_8_FreeDecompressionContext(_ctx);
                Marshal.FreeHGlobal(_ctx);
                _isDisposed = true;
            }
        }

        /// <summary>
        /// Returns true if there is no available input to feed the decoder.
        /// </summary>
        public bool NeedsInput(CompressionBuffer inData) => inData.AvailableRead == 0;

        /// <summary>
        /// Decompresses available input from <paramref name="inData"/> into <paramref name="outData"/>.
        /// If bzip2 reports the end of a stream and further input immediately looks like another
        /// bzip2 header, the decoder transparently reinitializes and continues (multi-stream
        /// concatenation); otherwise <see cref="Finished"/> becomes true.
        /// </summary>
        /// <param name="inData">The input buffer containing compressed data.</param>
        /// <param name="outData">The output buffer to write decompressed data to.</param>
        /// <param name="length">The maximum number of bytes to write. If 0, fills the available output space.</param>
        /// <returns>The number of bytes written to <paramref name="outData"/>.</returns>
        /// <exception cref="InvalidDataException">Thrown if the input data is corrupted or invalid.</exception>
        public int DecodeData(CompressionBuffer inData, CompressionBuffer outData, int length)
        {
            outData.Tidy();

            if (length == 0 || length > outData.AvailableWrite)
                length = outData.AvailableWrite;

            if (_finished || length == 0 || inData.AvailableRead == 0)
                return 0;

            _nonEmptyInput = true;

            fixed (byte* inBase = inData.Data)
            fixed (byte* outBase = outData.Data)
            {
                byte* srcPtr = inBase + inData.Pos;
                byte* dstPtr = outBase + outData.Size;

                int result = SZ_BZip2_v1_0_8_DecompressStream(
                    _ctx, dstPtr, (UIntPtr)length,
                    srcPtr, (UIntPtr)inData.AvailableRead,
                    out long inSize, out long outSize);

                if (result < 0)
                {
                    // BZ_DATA_ERROR or BZ_DATA_ERROR_MAGIC can occur when the internal
                    // buffer overreads past the real bzip2 stream end into non-bzip2 data
                    // (e.g., next archive entry in a Zip file). If tolerance mode is enabled
                    // and we've previously produced valid output, treat as implicit end-of-stream.
                    // The caller's rewind mechanism will correct the base stream position.
                    if (_tolerateErrors && _nonEmptyInput && (result == BZ_DATA_ERROR || result == BZ_DATA_ERROR_MAGIC || result == BZ_SEQUENCE_ERROR))
                    {
                        _finished = true;
                        if (inSize > 0)
                            inData.Read((int)inSize);
                        if (outSize > 0)
                            outData.Write((int)outSize);
                        return (int)outSize;
                    }

                    throw new InvalidDataException(SR.GenericInvalidData);
                }

                if (inSize > 0)
                    inData.Read((int)inSize);
                if (outSize > 0)
                    outData.Write((int)outSize);

                if (result == BZ_STREAM_END)
                {
                    if (inData.AvailableRead >= _bzip2MagicHeader.Length && looksLikeBzip2Header(inBase + inData.Pos))
                    {
                        SZ_BZip2_v1_0_8_FreeDecompressionContext(_ctx);
                        init();
                    }
                    else
                    {
                        _finished = true;
                    }
                }

                return (int)outSize;
            }
        }

        private static bool looksLikeBzip2Header(byte* p)
        {
            for (int i = 0; i < _bzip2MagicHeader.Length; i++)
                if (p[i] != _bzip2MagicHeader[i])
                    return false;
            return true;
        }
    }
}

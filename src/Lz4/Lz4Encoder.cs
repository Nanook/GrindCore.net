using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;
using static Nanook.GrindCore.Interop.Lz4;

namespace Nanook.GrindCore.Lz4
{
    /// <summary>
    /// Provides an encoder for LZ4 frame-compressed data, supporting block-based compression.
    /// </summary>
    internal unsafe class Lz4Encoder : IDisposable
    {
        private SZ_Lz4F_v1_10_0_CompressionContext _context;
        private byte[] _buffer;
        private GCHandle _bufferPinned;
        private IntPtr _bufferPtr;
        private LZ4F_preferences_t _preferences;
        private bool _headerWritten;

        /// <summary>
        /// Gets the block size used for compression.
        /// </summary>
        public int BlockSize { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Lz4Encoder"/> class with the specified block size and compression level.
        /// </summary>
        /// <param name="blockSize">The block size to use for compression.</param>
        /// <param name="compressionLevel">The compression level to use.</param>
        /// <exception cref="Exception">Thrown if the compression context or buffer cannot be allocated.</exception>
        public Lz4Encoder(int blockSize, int compressionLevel)
        {
            BlockSize = blockSize;

            _context = new SZ_Lz4F_v1_10_0_CompressionContext();

            fixed (SZ_Lz4F_v1_10_0_CompressionContext* ctxPtr = &_context)
            {
                if (SZ_Lz4F_v1_10_0_CreateCompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create LZ4 Frame compression context");
            }

            // level 1 is lz4 - LZ4F_blockLinked favour decompression
            // level 2 is lz4 - LZ4F_blockLinked
            // level 3 to 12 is lz4 - LZ4F_blockIndependent

            _preferences = new LZ4F_preferences_t
            {
                frameInfo = new LZ4F_frameInfo_t
                {
                    // blockSizeID = LZ4F_blockSizeID_t.LZ4F_max4MB,
                    blockMode = compressionLevel < 3 ? LZ4F_blockMode_t.LZ4F_blockLinked : LZ4F_blockMode_t.LZ4F_blockIndependent,
                    contentChecksumFlag = LZ4F_contentChecksum_t.LZ4F_noContentChecksum
                },
                favorDecSpeed = compressionLevel == 1 ? 1u : 0u,
                compressionLevel = compressionLevel
            };

            {
                UIntPtr bound = SZ_Lz4F_v1_10_0_CompressFrameBound((UIntPtr)BlockSize, IntPtr.Zero);
                _buffer = BufferPool.Rent((int)bound.ToUInt64());
            }
            if (_buffer == null)
                throw new Exception("Failed to allocate buffer for LZ4 compression");

            _bufferPinned = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _bufferPtr = _bufferPinned.AddrOfPinnedObject();

            _headerWritten = false;
        }

        /// <summary>
        /// Writes the LZ4 frame header to the output buffer if it has not already been written.
        /// </summary>
        /// <param name="outData">The output buffer to write the header to.</param>
        /// <param name="totalCompressed">The current total number of compressed bytes.</param>
        /// <returns>The updated total number of compressed bytes.</returns>
        /// <exception cref="Exception">Thrown if the header cannot be written.</exception>
        private int writeHeader(CompressionBuffer outData, int totalCompressed)
        {
            if (!_headerWritten)
            {
                UIntPtr headerSizeU;
                fixed (LZ4F_preferences_t* prefsPtr = &_preferences)
                fixed (SZ_Lz4F_v1_10_0_CompressionContext* ctxPtr = &_context)
                {
                    headerSizeU = SZ_Lz4F_v1_10_0_CompressBegin(
                        ctxPtr, _bufferPtr, (UIntPtr)_buffer.Length, prefsPtr);
                }

                long headerSize = (long)headerSizeU.ToUInt64();
                if (headerSize < 0)
                    throw new Exception("Failed to write LZ4 frame header");
                if (headerSize > int.MaxValue)
                    throw new Exception($"LZ4 header size too large: {headerSize}");

                totalCompressed += (int)headerSize;
                outData.Write(_buffer, 0, (int)headerSize);
                _headerWritten = true;
            }

            return totalCompressed;
        }

        /// <summary>
        /// Encodes data from the input buffer into the output buffer using LZ4 frame compression.
        /// </summary>
        /// <param name="inData">The input buffer containing data to compress.</param>
        /// <param name="outData">The output buffer to write compressed data to.</param>
        /// <param name="final">Indicates if this is the final block of data.</param>
        /// <param name="cancel">A cancellable task for cooperative cancellation.</param>
        /// <returns>The total number of bytes written to the output buffer.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="inData"/> or <paramref name="outData"/> is not at the correct position.</exception>
        /// <exception cref="Exception">Thrown if compression fails.</exception>
        public long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            // Write frame header if not already written
            int totalCompressed = writeHeader(outData, 0);

            while (inData.AvailableRead > 0 || final)
            {
                cancel.ThrowIfCancellationRequested();

                int inputSize = Math.Min(inData.AvailableRead, BlockSize);

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_Lz4F_v1_10_0_CompressionContext* ctxPtr = &_context)
                {
                    *&inputPtr += inData.Pos;
                    UIntPtr compressedSizeU = SZ_Lz4F_v1_10_0_CompressUpdate(
                        ctxPtr, _bufferPtr, (UIntPtr)_buffer.Length,
                        inputPtr, (UIntPtr)inputSize, IntPtr.Zero);

                    long compressedSize = (long)compressedSizeU.ToUInt64();
                    if (compressedSize < 0)
                        throw new Exception($"LZ4 Frame compression failed with error code {compressedSizeU.ToUInt64()}");
                    if (compressedSize > int.MaxValue)
                        throw new Exception($"LZ4 Frame compression returned size too large: {compressedSize}");

                    inData.Read(inputSize);
                    outData.Write(_buffer, 0, (int)compressedSize);
                    totalCompressed += (int)compressedSize;
                    final = false;
                }
            }

            return totalCompressed;
        }

        /// <summary>
        /// Flushes any remaining compressed data to the output buffer and finalizes the LZ4 frame if requested.
        /// </summary>
        /// <param name="outData">The output buffer to write flushed or finalized data to.</param>
        /// <param name="flush">Indicates if a flush operation should be performed.</param>
        /// <param name="complete">Indicates if the frame should be finalized.</param>
        /// <returns>The number of bytes written to the output buffer during the flush/finalize operation.</returns>
        /// <exception cref="Exception">Thrown if flush or finalize fails.</exception>
        public long Flush(CompressionBuffer outData, bool flush, bool complete)
        {
            fixed (SZ_Lz4F_v1_10_0_CompressionContext* ctxPtr = &_context)
            {
                UIntPtr flushedSize = UIntPtr.Zero;
                UIntPtr endSize = UIntPtr.Zero;

                // Write frame header if not already written
                int headerBytes = writeHeader(outData, 0);

                if (flush)
                {
                    UIntPtr flushedSizeU = SZ_Lz4F_v1_10_0_Flush(ctxPtr, _bufferPtr, (UIntPtr)_buffer.Length, IntPtr.Zero);
                    long flushedSizeL = (long)flushedSizeU.ToUInt64();
                    if (flushedSizeL < 0)
                        throw new Exception("LZ4 Frame flush failed");
                    if (flushedSizeL > int.MaxValue)
                        throw new Exception($"LZ4 Frame flush returned size too large: {flushedSizeL}");
                    outData.Write(_buffer, 0, (int)flushedSizeL);
                    flushedSize = (UIntPtr)flushedSizeL;
                }

                if (complete)
                {
                    UIntPtr endSizeU = SZ_Lz4F_v1_10_0_CompressEnd(ctxPtr, _bufferPtr, (UIntPtr)_buffer.Length, IntPtr.Zero);
                    long endSizeL = (long)endSizeU.ToUInt64();
                    if (endSizeL <= 0)
                        throw new Exception("Failed to finalize LZ4 Frame compression");
                    if (endSizeL > int.MaxValue)
                        throw new Exception($"LZ4 Frame end returned size too large: {endSizeL}");

                    outData.Write(_buffer, 0, (int)endSizeL);
                    endSize = (UIntPtr)endSizeL;
                }

                return (long)((ulong)headerBytes + flushedSize.ToUInt64() + endSize.ToUInt64());
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="Lz4Encoder"/>.
        /// </summary>
        public void Dispose()
        {
            fixed (SZ_Lz4F_v1_10_0_CompressionContext* ctxPtr = &_context)
            {
                SZ_Lz4F_v1_10_0_FreeCompressionContext(ctxPtr);
            }

            if (_bufferPinned.IsAllocated)
                _bufferPinned.Free();

            BufferPool.Return(_buffer);
        }
    }
}


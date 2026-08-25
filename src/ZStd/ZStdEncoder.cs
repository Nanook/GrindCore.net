using System;
using System.Runtime.InteropServices;
using static Nanook.GrindCore.Interop;

namespace Nanook.GrindCore.ZStd
{
    /// <summary>
    /// Provides an encoder for Zstandard (ZStd) compressed data, supporting streaming compression.
    /// This class implements the latest ZStd version (1.5.7) directly.
    /// For older versions (e.g., 1.5.2), use <see cref="ZStdEncoderV1_5_2"/>, which inherits from this class and overrides only the version-specific logic.
    /// </summary>
    internal unsafe class ZStdEncoder : IDisposable
    {
        protected SZ_ZStd_v1_5_7_CompressionContext _ctx;
        protected SZ_ZStd_v1_5_7_CompressionDict? _cdict;
        protected byte[] _outputBuffer;
        protected GCHandle _outputPinned;
        protected IntPtr _outputPtr;
        protected int _compressionLevel;

        /// <summary>
        /// Gets the recommended input buffer size for ZStd compression.
        /// </summary>
        public int InputBufferSize { get; protected set; }

        /// <summary>
        /// Gets the recommended output buffer size for ZStd compression.
        /// </summary>
        public int OutputBufferSize { get; protected set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZStdEncoder"/> class with the specified block size and compression level.
        /// </summary>
        /// <param name="blockSize">The block size to use for compression.</param>
        /// <param name="compressionLevel">The compression level to use (default is 3).</param>
        /// <param name="nbWorkers">Number of worker threads for multithreaded compression (0 = single-threaded).</param>
        /// <param name="jobSize">Size of each compression job when using MT (0 = auto).</param>
        /// <param name="dictionary">Optional pre-trained dictionary data for improved compression.</param>
        /// <param name="windowLog">Optional advanced windowLog override for the dictionary's CDict (0 = zstd's implicit level-based sizing, which caps out at 8MB for dictionaries &gt;256KB - see <see cref="CompressionDictionaryOptions.WindowBits"/>).</param>
        public ZStdEncoder(int blockSize, int compressionLevel = 3, int nbWorkers = 0, int jobSize = 0, byte[]? dictionary = null, int windowLog = 0)
        {
            _compressionLevel = compressionLevel;
            _ctx = new SZ_ZStd_v1_5_7_CompressionContext();

            fixed (SZ_ZStd_v1_5_7_CompressionContext* ctxPtr = &_ctx)
            {
                if (Interop.ZStd.SZ_ZStd_v1_5_7_CreateCompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create Zstd v1.5.7 compression context");

                Interop.ZStd.SZ_ZStd_v1_5_7_SetCompressionLevel(ctxPtr, _compressionLevel);

                if (nbWorkers > 0)
                    Interop.ZStd.SZ_ZStd_v1_5_7_SetNbWorkers(ctxPtr, nbWorkers);
                if (jobSize > 0)
                    Interop.ZStd.SZ_ZStd_v1_5_7_SetJobSize(ctxPtr, (nuint)jobSize);

                if (dictionary != null && dictionary.Length > 0)
                {
                    fixed (byte* dictPtr = dictionary)
                    {
                        SZ_ZStd_v1_5_7_CompressionDict dict = new SZ_ZStd_v1_5_7_CompressionDict();
                        if (Interop.ZStd.SZ_ZStd_v1_5_7_CreateCompressionDict(&dict, (IntPtr)dictPtr, (UIntPtr)dictionary.Length, _compressionLevel, windowLog) == 0)
                        {
                            _cdict = dict;
                            Interop.ZStd.SZ_ZStd_v1_5_7_SetCompressionDict(ctxPtr, &dict);
                        }
                    }
                }
            }

            InputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_7_CStreamInSize();
            OutputBufferSize = (int)Interop.ZStd.SZ_ZStd_v1_5_7_CStreamOutSize();

            _outputBuffer = BufferPool.Rent(OutputBufferSize);
            _outputPinned = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
            _outputPtr = _outputPinned.AddrOfPinnedObject();
        }

        /// <summary>
        /// Encodes data from the input buffer into the output buffer using ZStd compression.
        /// </summary>
        public virtual long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            int totalCompressed = 0;

            while (inData.AvailableRead > 0 || final)
            {
                cancel.ThrowIfCancellationRequested();

                int srcCapacity = Math.Min(inData.AvailableRead, InputBufferSize);
                long inSize;
                long outSize;

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_ZStd_v1_5_7_CompressionContext* ctxPtr = &_ctx)
                {
                    byte* srcPtr = inputPtr + inData.Pos;

                    Interop.ZStd.SZ_ZStd_v1_5_7_CompressStream(
                        ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize,
                        srcPtr, (UIntPtr)srcCapacity,
                        out inSize, out outSize);

                    // Non-zero return is normal for MT (indicates internal buffering) — not an error.
                    // Buffered data will be drained during Flush/End operations.

                    inData.Read((int)inSize);
                    if (outSize > 0)
                    {
                        outData.Write(_outputBuffer, 0, (int)outSize);
                        totalCompressed += (int)outSize;
                    }
                    final = false;
                }
            }

            return totalCompressed;
        }

        /// <summary>
        /// Flushes any remaining compressed data to the output buffer and finalizes the compression stream.
        /// </summary>
        public virtual long Flush(CompressionBuffer outData)
        {
            long totalFlushed = 0;
            byte[] buff = new byte[1];
            long inSize;
            long outSize;

            fixed (byte* inputPtr = buff)
            fixed (SZ_ZStd_v1_5_7_CompressionContext* ctxPtr = &_ctx)
            {
                // Flush: drain all internally buffered data
                UIntPtr res;
                do
                {
                    res = Interop.ZStd.SZ_ZStd_v1_5_7_FlushStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out outSize);
                    if (outSize > 0)
                    {
                        outData.Write(_outputBuffer, 0, (int)outSize);
                        totalFlushed += outSize;
                    }
                } while (res != UIntPtr.Zero);

                // End: finalize the frame, draining any remaining output
                do
                {
                    res = Interop.ZStd.SZ_ZStd_v1_5_7_EndStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out outSize);
                    if (outSize > 0)
                    {
                        outData.Write(_outputBuffer, 0, (int)outSize);
                        totalFlushed += outSize;
                    }
                } while (res != UIntPtr.Zero);

                return totalFlushed;
            }
        }

        /// <summary>
        /// Releases all resources used by the <see cref="ZStdEncoder"/>.
        /// </summary>
        public virtual void Dispose()
        {
            fixed (SZ_ZStd_v1_5_7_CompressionContext* ctxPtr = &_ctx)
            {
                Interop.ZStd.SZ_ZStd_v1_5_7_FreeCompressionContext(ctxPtr);
            }

            if (_cdict.HasValue)
            {
                var cdict = _cdict.Value;
                Interop.ZStd.SZ_ZStd_v1_5_7_FreeCompressionDict(&cdict);
                _cdict = null;
            }

            if (_outputPinned.IsAllocated)
                try { _outputPinned.Free(); } catch { }

            BufferPool.Return(_outputBuffer);
        }
    }

    /// <summary>
    /// Provides an encoder for Zstandard (ZStd) compressed data using version 1.5.2.
    /// Inherits from <see cref="ZStdEncoder"/> and overrides only the version-specific logic.
    /// </summary>
    internal unsafe class ZStdEncoderV1_5_2 : ZStdEncoder
    {
        private SZ_ZStd_v1_5_2_CompressionContext _ctx152;
        private SZ_ZStd_v1_5_2_CompressionDict? _cdict152;

        public ZStdEncoderV1_5_2(int blockSize, int compressionLevel = 3, int nbWorkers = 0, int jobSize = 0, byte[]? dictionary = null, int windowLog = 0)
            : base(0, compressionLevel) // base will not be used, but must be called
        {
            _compressionLevel = compressionLevel;
            _ctx152 = new SZ_ZStd_v1_5_2_CompressionContext();

            fixed (SZ_ZStd_v1_5_2_CompressionContext* ctxPtr = &_ctx152)
            {
                if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateCompressionContext(ctxPtr) < 0)
                    throw new Exception("Failed to create Zstd v1.5.2 compression context");

                Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_SetCompressionLevel(ctxPtr, _compressionLevel);

                if (nbWorkers > 0)
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_SetNbWorkers(ctxPtr, nbWorkers);
                if (jobSize > 0)
                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_SetJobSize(ctxPtr, (nuint)jobSize);

                if (dictionary != null && dictionary.Length > 0)
                {
                    fixed (byte* dictPtr = dictionary)
                    {
                        SZ_ZStd_v1_5_2_CompressionDict dict = new SZ_ZStd_v1_5_2_CompressionDict();
                        if (Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CreateCompressionDict(&dict, (IntPtr)dictPtr, (UIntPtr)dictionary.Length, _compressionLevel, windowLog) == 0)
                        {
                            _cdict152 = dict;
                            Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_SetCompressionDict(ctxPtr, &dict);
                        }
                    }
                }
            }

            InputBufferSize = (int)Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CStreamInSize();
            OutputBufferSize = (int)Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CStreamOutSize();

            _outputBuffer = BufferPool.Rent(OutputBufferSize);
            _outputPinned = GCHandle.Alloc(_outputBuffer, GCHandleType.Pinned);
            _outputPtr = _outputPinned.AddrOfPinnedObject();
        }

        public override long EncodeData(CompressionBuffer inData, CompressionBuffer outData, bool final, CancellableTask cancel)
        {
            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            int totalCompressed = 0;

            while (inData.AvailableRead > 0 || final)
            {
                cancel.ThrowIfCancellationRequested();

                int srcCapacity = Math.Min(inData.AvailableRead, InputBufferSize);
                long inSize;
                long outSize;

                fixed (byte* inputPtr = inData.Data)
                fixed (SZ_ZStd_v1_5_2_CompressionContext* ctxPtr = &_ctx152)
                {
                    byte* srcPtr = inputPtr + inData.Pos;

                    Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_CompressStream(
                        ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize,
                        srcPtr, (UIntPtr)srcCapacity,
                        out inSize, out outSize);

                    // Non-zero return is normal for MT — not an error.
                    // Buffered data will be drained during Flush/End operations.

                    inData.Read((int)inSize);
                    if (outSize > 0)
                    {
                        outData.Write(_outputBuffer, 0, (int)outSize);
                        totalCompressed += (int)outSize;
                    }
                    final = false;
                }
            }

            return totalCompressed;
        }

        public override long Flush(CompressionBuffer outData)
        {
            long totalFlushed = 0;
            byte[] buff = new byte[1];
            long inSize;
            long outSize;

            fixed (byte* inputPtr = buff)
            fixed (SZ_ZStd_v1_5_2_CompressionContext* ctxPtr = &_ctx152)
            {
                // Flush: drain all internally buffered data
                UIntPtr res;
                do
                {
                    res = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FlushStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out outSize);
                    if (outSize > 0)
                    {
                        outData.Write(_outputBuffer, 0, (int)outSize);
                        totalFlushed += outSize;
                    }
                } while (res != UIntPtr.Zero);

                // End: finalize the frame, draining any remaining output
                do
                {
                    res = Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_EndStream(ctxPtr, _outputPtr, (UIntPtr)OutputBufferSize, inputPtr, (UIntPtr)0, out inSize, out outSize);
                    if (outSize > 0)
                    {
                        outData.Write(_outputBuffer, 0, (int)outSize);
                        totalFlushed += outSize;
                    }
                } while (res != UIntPtr.Zero);

                return totalFlushed;
            }
        }

        public override void Dispose()
        {
            fixed (SZ_ZStd_v1_5_2_CompressionContext* ctxPtr = &_ctx152)
            {
                Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeCompressionContext(ctxPtr);
            }

            if (_cdict152.HasValue)
            {
                var cdict152 = _cdict152.Value;
                Interop.ZStd_v1_5_2.SZ_ZStd_v1_5_2_FreeCompressionDict(&cdict152);
                _cdict152 = null;
            }

            if (_outputPinned.IsAllocated)
                try { _outputPinned.Free(); } catch { }

            BufferPool.Return(_outputBuffer);
        }
    }
}
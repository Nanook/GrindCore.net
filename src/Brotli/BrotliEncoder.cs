using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System;


namespace Nanook.GrindCore.Brotli
{
    /// <summary>Provides methods and static methods to encode and decode data in a streamless, non-allocating, and performant manner using the Brotli data format specification.</summary>
    internal partial struct BrotliEncoder : IDisposable
    {
        private const int WindowBits_Min = 10;
        private const int WindowBits_Default = 22;
        private const int WindowBits_Max = 24;
        private const int MaxInputSize = int.MaxValue - 515; // 515 is the max compressed extra bytes

        internal SafeBrotliEncoderHandle? _state;
        private bool _disposed;

        /// <summary>Initializes a new instance of the <see cref="System.IO.Compression.BrotliEncoder" /> structure using the specified quality and window.</summary>
        /// <param name="quality">A number representing quality of the Brotli compression. 0 is the minimum (no compression), 11 is the maximum.</param>
        /// <param name="window">A number representing the encoder window bits. The minimum value is 10, and the maximum value is 24.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="quality" /> is not between the minimum value of 0 and the maximum value of 11.
        /// -or-
        /// <paramref name="window" /> is not between the minimum value of 10 and the maximum value of 24.</exception>
        /// <exception cref="IOException">Failed to create the <see cref="System.IO.Compression.BrotliEncoder" /> instance.</exception>
        public BrotliEncoder(CompressionType level, int window, CompressionVersion? version = null)
        {
            _disposed = false;

            if (version == null)
                version = CompressionVersion.BrotliLatest();
            if (version.Index == 0)
                _state = Interop.Brotli.DN9_BRT_v1_1_0_EncoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            else
                throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
            _state!.Version = version;
            if (_state.IsInvalid)
                throw new IOException(SR.BrotliEncoder_Create);
            SetQuality(level);
            SetWindow(window);
        }

        /// <summary>
        /// Performs a lazy initialization of the native encoder using the default Quality and Window values:
        /// BROTLI_DEFAULT_WINDOW 22
        /// BROTLI_DEFAULT_QUALITY 11
        /// </summary>
        internal void InitializeEncoder(CompressionVersion? version = null)
        {
            EnsureNotDisposed();
            if (version == null)
                version = CompressionVersion.BrotliLatest();
            if (version.Index == 0)
                _state = Interop.Brotli.DN9_BRT_v1_1_0_EncoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            else
                throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
            _state!.Version = version;
            if (_state.IsInvalid)
                throw new IOException(SR.BrotliEncoder_Create);
        }

        internal void EnsureInitialized()
        {
            EnsureNotDisposed();
            if (_state == null)
                InitializeEncoder();
        }

        /// <summary>Frees and disposes unmanaged resources.</summary>
        public void Dispose()
        {
            _disposed = true;
            _state?.Dispose();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrotliEncoder), SR.BrotliEncoder_Disposed);
        }

        internal void SetQuality(CompressionType level)
        {
            EnsureNotDisposed();
            if (_state == null || _state.IsInvalid || _state.IsClosed)
            {
                InitializeEncoder();
                Debug.Assert(_state != null && !_state.IsInvalid && !_state.IsClosed);
            }

            if (_state.Version.Index == 0)
            {
                if (Interop.Brotli.DN9_BRT_v1_1_0_EncoderSetParameter(_state, BrotliEncoderParameter.Quality, (uint)level) == Interop.BOOL.FALSE)
                    throw new InvalidOperationException(string.Format(SR.BrotliEncoder_InvalidSetParameter, "Quality"));
            }
            else
                throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");
        }

        internal void SetWindow()
        {
            SetWindow(WindowBits_Default);
        }

        internal void SetWindow(int window)
        {
            EnsureNotDisposed();
            if (_state == null || _state.IsInvalid || _state.IsClosed)
            {
                InitializeEncoder();
                Debug.Assert(_state != null && !_state.IsInvalid && !_state.IsClosed);
            }

            if (window < WindowBits_Min || window > WindowBits_Max)
                throw new ArgumentOutOfRangeException(nameof(window), string.Format(SR.BrotliEncoder_Window, window, WindowBits_Min, WindowBits_Max));

            if (_state.Version.Index == 0)
            {
                if (Interop.Brotli.DN9_BRT_v1_1_0_EncoderSetParameter(_state, BrotliEncoderParameter.LGWin, (uint)window) == Interop.BOOL.FALSE)
                    throw new InvalidOperationException(string.Format(SR.BrotliEncoder_InvalidSetParameter, "Window"));
            }
            else
                throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");
        }

        /// <summary>Gets the maximum expected compressed length for the provided input size.</summary>
        /// <param name="inputSize">The input size to get the maximum expected compressed length from. Must be greater or equal than 0 and less or equal than <see cref="int.MaxValue" /> - 515.</param>
        /// <returns>A number representing the maximum compressed length for the provided input size.</returns>
        /// <remarks>Returns 1 if <paramref name="inputSize" /> is 0.</remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="inputSize" /> is less than 0, the minimum allowed input size, or greater than <see cref="int.MaxValue" /> - 515, the maximum allowed input size.</exception>
        public static int GetMaxCompressedLength(int inputSize)
        {
            if (inputSize < 0)
                throw new ArgumentOutOfRangeException(nameof(inputSize));
            if (inputSize > MaxInputSize)
                throw new ArgumentOutOfRangeException(nameof(inputSize));

            if (inputSize == 0)
                return 1;

            int numLargeBlocks = inputSize >> 24;
            int tail = inputSize & 0xFFFFFF;
            int tailOverhead = tail > 1 << 20 ? 4 : 3;
            int overhead = 2 + 4 * numLargeBlocks + tailOverhead + 1;
            int result = inputSize + overhead;
            return result;
        }

        //internal OperationStatus Flush(DataBlock destination, out int bytesWritten) => Flush(destination.Span, out bytesWritten);

        /// <summary>Compresses an empty read-only span of bytes into its destination, which ensures that output is produced for all the processed input. An actual flush is performed when the source is depleted and there is enough space in the destination for the remaining data.</summary>
        /// <param name="destination">When this method returns, a span of bytes where the compressed data will be stored.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="destination" />.</param>
        /// <returns>One of the enumeration values that describes the status with which the operation finished.</returns>
        public OperationStatus Flush(CompressionBuffer destination, out int bytesWritten) => Compress(new CompressionBuffer(0), destination, out _, out bytesWritten, BrotliEncoderOperation.Flush);

        //internal OperationStatus Process(DataBlock source, DataBlock destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock) => Process(source, destination, out bytesConsumed, out bytesWritten, isFinalBlock);

        /// <summary>Compresses a read-only byte span into a destination span.</summary>
        /// <param name="source">A read-only span of bytes containing the source data to compress.</param>
        /// <param name="destination">When this method returns, a byte span where the compressed is stored.</param>
        /// <param name="bytesConsumed">When this method returns, the total number of bytes that were read from <paramref name="source" />.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="destination" />.</param>
        /// <param name="isFinalBlock"><see langword="true" /> to finalize the internal stream, which prevents adding more input data when this method returns; <see langword="false" /> to allow the encoder to postpone the production of output until it has processed enough input.</param>
        /// <returns>One of the enumeration values that describes the status with which the span-based operation finished.</returns>
        public OperationStatus Compress(CompressionBuffer source, CompressionBuffer destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock) => Compress(source, destination, out bytesConsumed, out bytesWritten, isFinalBlock ? BrotliEncoderOperation.Finish : BrotliEncoderOperation.Process);

        internal OperationStatus Compress(CompressionBuffer source, CompressionBuffer destination, out int bytesConsumed, out int bytesWritten, BrotliEncoderOperation operation)
        {
            EnsureInitialized();
            Debug.Assert(_state != null);
            bool skipFirstFlush = operation == BrotliEncoderOperation.Flush && source.AvailableRead == 0;

            bytesWritten = 0;
            bytesConsumed = 0;
            nuint availableOutput = (nuint)destination.AvailableWrite;
            nuint availableInput = (nuint)source.AvailableRead;

            unsafe
            {
                // We can freely cast between int and nuint (.NET size_t equivalent) for two reasons:
                // 1. Interop Brotli functions will always return an availableInput/Output value lower or equal to the one passed to the function
                // 2. Span's have a maximum length of the int boundary.
                while ((int)availableOutput > 0)
                {
                    fixed (byte* inBytes = source.Data)
                    fixed (byte* outBytes = destination.Data)
                    {
                        *&inBytes += source.Pos;
                        *&outBytes += destination.Size; //writePos is Size

                        if (!skipFirstFlush) //don't flush if there's no data as it can add 2 bytes (that check is after)
                        {
                            if (_state.Version.Index == 0)
                            {
                                if (Interop.Brotli.DN9_BRT_v1_1_0_EncoderCompressStream(_state!, operation, ref availableInput, &inBytes, ref availableOutput, &outBytes, out _) == Interop.BOOL.FALSE)
                                    return OperationStatus.InvalidData;
                            }
                            else
                                throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");
                        }

                        bytesConsumed += source.AvailableRead - (int)availableInput;
                        bytesWritten += destination.AvailableWrite - (int)availableOutput;

                        source.Read(bytesConsumed); //update
                        destination.Write(bytesWritten); //update

                        if (_state.Version.Index == 0)
                        {
                            // no bytes written, no remaining input to give to the encoder, and no output in need of retrieving means we are Done
                            if ((int)availableOutput == destination.AvailableWrite && Interop.Brotli.DN9_BRT_v1_1_0_EncoderHasMoreOutput(_state) == Interop.BOOL.FALSE && availableInput == 0)
                                return OperationStatus.Done;
                            skipFirstFlush = false; //will loop if there's data to be flushed - prevent extra 2 bytes being written when there's extra data
                        }
                        else
                            throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");
                    }
                }

                return OperationStatus.DestinationTooSmall;
            }
        }
    }
}

using System.Diagnostics;
using System.IO;
using System;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>
    /// Provides methods and static methods to encode and decode data in a streamless, non-allocating, and performant manner using the Brotli data format specification.
    /// </summary>
    internal struct BrotliEncoder : IDisposable
    {
        private const int WindowBits_Min = 10;
        private const int WindowBits_Default = 22;
        private const int WindowBits_Max = 24;
        private const int MaxInputSize = int.MaxValue - 515; // 515 is the max compressed extra bytes

        internal SafeBrotliEncoderHandle? _state;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="BrotliEncoder"/> structure using the specified quality and window.
        /// </summary>
        /// <param name="level">A value representing the Brotli compression level. 0 is the minimum (no compression), 11 is the maximum.</param>
        /// <param name="window">A value representing the encoder window bits. The minimum value is 10, and the maximum value is 24.</param>
        /// <param name="version">The Brotli version to use, or <c>null</c> for the latest.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="level"/> or <paramref name="window"/> is out of range.</exception>
        /// <exception cref="IOException">Thrown if the encoder could not be created.</exception>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
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
        /// Performs a lazy initialization of the native encoder using the default Quality and Window values.
        /// </summary>
        /// <param name="version">The Brotli version to use, or <c>null</c> for the latest.</param>
        /// <exception cref="IOException">Thrown if the encoder could not be created.</exception>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
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

        /// <summary>
        /// Ensures the encoder is initialized, initializing it if necessary.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the encoder has been disposed.</exception>
        internal void EnsureInitialized()
        {
            EnsureNotDisposed();
            if (_state == null)
                InitializeEncoder();
        }

        /// <summary>
        /// Releases all resources used by the current Brotli encoder instance.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            _state?.Dispose();
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the encoder has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the encoder has been disposed.</exception>
        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrotliEncoder), SR.BrotliEncoder_Disposed);
        }

        /// <summary>
        /// Sets the Brotli compression quality (level).
        /// </summary>
        /// <param name="level">The compression level to set.</param>
        /// <exception cref="InvalidOperationException">Thrown if the encoder is not valid or the parameter cannot be set.</exception>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
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

        /// <summary>
        /// Sets the Brotli window size to the default value.
        /// </summary>
        internal void SetWindow()
        {
            SetWindow(WindowBits_Default);
        }

        /// <summary>
        /// Sets the Brotli window size.
        /// </summary>
        /// <param name="window">The window size to set.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="window"/> is out of range.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the encoder is not valid or the parameter cannot be set.</exception>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
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

        /// <summary>
        /// Gets the maximum expected compressed length for the provided input size.
        /// </summary>
        /// <param name="inputSize">The input size to get the maximum expected compressed length from. Must be greater or equal than 0 and less or equal than <see cref="int.MaxValue" /> - 515.</param>
        /// <returns>A number representing the maximum compressed length for the provided input size.</returns>
        /// <remarks>Returns 1 if <paramref name="inputSize" /> is 0.</remarks>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="inputSize"/> is less than 0 or greater than the maximum allowed input size.</exception>
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

        /// <summary>
        /// Compresses an empty buffer into <paramref name="outData"/>, ensuring that output is produced for all the processed input.
        /// An actual flush is performed when the input is depleted and there is enough space in the output for the remaining data.
        /// </summary>
        /// <param name="outData">When this method returns, a buffer where the compressed data will be stored.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="outData"/>.</param>
        /// <returns>One of the enumeration values that describes the status with which the operation finished.</returns>
        public OperationStatus Flush(CompressionBuffer outData, out int bytesWritten) => EncodeData(new CompressionBuffer(0), outData, out _, out bytesWritten, BrotliEncoderOperation.Flush);

        /// <summary>
        /// Compresses a buffer into an output buffer.
        /// </summary>
        /// <param name="inData">A buffer containing the input data to compress.</param>
        /// <param name="outData">When this method returns, a buffer where the compressed data is stored.</param>
        /// <param name="bytesConsumed">When this method returns, the total number of bytes that were read from <paramref name="inData"/>.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="outData"/>.</param>
        /// <param name="isFinalBlock"><see langword="true"/> to finalize the internal stream, which prevents adding more input data when this method returns; <see langword="false"/> to allow the encoder to postpone the production of output until it has processed enough input.</param>
        /// <returns>One of the enumeration values that describes the status with which the operation finished.</returns>
        public OperationStatus EncodeData(CompressionBuffer inData, CompressionBuffer outData, out int bytesConsumed, out int bytesWritten, bool isFinalBlock) => EncodeData(inData, outData, out bytesConsumed, out bytesWritten, isFinalBlock ? BrotliEncoderOperation.Finish : BrotliEncoderOperation.Process);

        /// <summary>
        /// Compresses a buffer into an output buffer using the specified encoder operation.
        /// </summary>
        /// <param name="inData">A buffer containing the input data to compress.</param>
        /// <param name="outData">When this method returns, a buffer where the compressed data is stored.</param>
        /// <param name="bytesConsumed">When this method returns, the total number of bytes that were read from <paramref name="inData"/>.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="outData"/>.</param>
        /// <param name="operation">The encoder operation to perform.</param>
        /// <returns>One of the enumeration values that describes the status with which the operation finished.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="inData"/> or <paramref name="outData"/> is not at the correct position.</exception>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        internal OperationStatus EncodeData(CompressionBuffer inData, CompressionBuffer outData, out int bytesConsumed, out int bytesWritten, BrotliEncoderOperation operation)
        {
            inData.Tidy();
            outData.Tidy();

            if (inData.Pos != 0)
                throw new ArgumentException($"inData should have a Pos of 0");
            if (outData.Size != 0)
                throw new ArgumentException($"outData should have a Size of 0");

            EnsureInitialized();
            Debug.Assert(_state != null);
            bool skipFirstFlush = operation == BrotliEncoderOperation.Flush && inData.AvailableRead == 0;

            bytesWritten = 0;
            bytesConsumed = 0;
            nuint availableOutput = (nuint)outData.AvailableWrite;
            nuint availableInput = (nuint)inData.AvailableRead;

            unsafe
            {
                // We can freely cast between int and nuint (.NET size_t equivalent) for two reasons:
                // 1. Interop Brotli functions will always return an availableInput/Output value lower or equal to the one passed to the function
                // 2. Span's have a maximum length of the int boundary.
                while ((int)availableOutput > 0)
                {
                    fixed (byte* inBytes = inData.Data)
                    fixed (byte* outBytes = outData.Data)
                    {
                        *&inBytes += inData.Pos;
                        *&outBytes += outData.Size; //writePos is Size

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

                        bytesConsumed += inData.AvailableRead - (int)availableInput;
                        bytesWritten += outData.AvailableWrite - (int)availableOutput;

                        inData.Read(bytesConsumed); //update
                        outData.Write(bytesWritten); //update

                        if (_state.Version.Index == 0)
                        {
                            // no bytes written, no remaining input to give to the encoder, and no output in need of retrieving means we are Done
                            if ((int)availableOutput == outData.AvailableWrite && Interop.Brotli.DN9_BRT_v1_1_0_EncoderHasMoreOutput(_state) == Interop.BOOL.FALSE && availableInput == 0)
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

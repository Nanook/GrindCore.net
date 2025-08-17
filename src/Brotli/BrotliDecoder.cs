using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>
    /// Provides non-allocating, performant Brotli decompression methods. The methods decompress in a single pass without using a BrotliStream /> instance.
    /// </summary>
    internal struct BrotliDecoder : IDisposable
    {
        private SafeBrotliDecoderHandle? _state;
        private bool _disposed;

        /// <summary>
        /// Initializes the Brotli decoder with the specified version, or the latest Brotli version if not specified.
        /// </summary>
        /// <param name="version">The Brotli version to use, or <c>null</c> for the latest.</param>
        /// <exception cref="IOException">Thrown if the decoder could not be created.</exception>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        internal void InitializeDecoder(CompressionVersion? version = null)
        {
            if (version == null)
                version = CompressionVersion.BrotliLatest();
            if (version.Index == 0)
                _state = Interop.Brotli.DN9_BRT_v1_1_0_BrotliDecoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            else
                throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
            _state.Version = version;
            if (_state.IsInvalid)
                throw new IOException(SR.BrotliDecoder_Create);
        }

        /// <summary>
        /// Ensures the decoder is initialized, initializing it if necessary.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the decoder has been disposed.</exception>
        internal void EnsureInitialized()
        {
            EnsureNotDisposed();
            if (_state == null)
                InitializeDecoder();
        }

        /// <summary>
        /// Releases all resources used by the current Brotli decoder instance.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
            _state?.Dispose();
        }

        /// <summary>
        /// Throws an <see cref="ObjectDisposedException"/> if the decoder has been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the decoder has been disposed.</exception>
        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrotliDecoder), SR.BrotliDecoder_Disposed);
        }

        /// <summary>
        /// Decompresses data that was compressed using the Brotli algorithm.
        /// </summary>
        /// <param name="inData">A buffer containing the compressed data.</param>
        /// <param name="outData">When this method returns, a buffer containing the decompressed data.</param>
        /// <param name="bytesConsumed">The total number of bytes that were read from <paramref name="inData" />.</param>
        /// <param name="bytesWritten">The total number of bytes that were written in the <paramref name="outData" />.</param>
        /// <returns>OperationStatus</returns>
        /// <remarks>
        /// The return value can be as follows:
        /// - <see cref="OperationStatus.Done"/>: <paramref name="inData"/> was successfully and completely decompressed into <paramref name="outData"/>.
        /// - <see cref="OperationStatus.DestinationTooSmall"/>: There is not enough space in <paramref name="outData"/> to decompress <paramref name="inData"/>.
        /// - <see cref="OperationStatus.NeedMoreData"/>: The decompression action is partially done; at least one more byte is required to complete the decompression task. This method should be called again with more input to decompress.
        /// - <see cref="OperationStatus.InvalidData"/>: The data in <paramref name="inData"/> is invalid and could not be decompressed.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the decoder has been disposed.</exception>
        /// <exception cref="IOException">Thrown if the decoder could not be created.</exception>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        public OperationStatus DecodeData(CompressionBuffer inData, CompressionBuffer outData, out int bytesConsumed, out int bytesWritten)
        {
            outData.Tidy();

            EnsureInitialized();
            Debug.Assert(_state != null);

            bytesConsumed = 0;
            bytesWritten = 0;
            if (_state.Version.Index == 0)
            {
                if (Interop.Brotli.DN9_BRT_v1_1_0_BrotliDecoderIsFinished(_state!) != Interop.BOOL.FALSE)
                    return OperationStatus.Done;
            }
            else
                throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");

            UIntPtr availableOutput = (UIntPtr)outData.AvailableWrite;
            UIntPtr availableInput = (UIntPtr)inData.AvailableRead;
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
                        *&outBytes += outData.Size; //Size is writing Pos

                        int brotliResult;
                        if (_state.Version.Index == 0)
                            brotliResult = Interop.Brotli.DN9_BRT_v1_1_0_BrotliDecoderDecompressStream(_state, ref availableInput, &inBytes, ref availableOutput, &outBytes, out _);
                        else
                            throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");

                        if (brotliResult == 0) // Error
                            return OperationStatus.InvalidData;

                        bytesConsumed += inData.AvailableRead - (int)availableInput;
                        bytesWritten += outData.AvailableWrite - (int)availableOutput;

                        inData.Read(bytesConsumed);
                        outData.Write(bytesWritten); //update dest

                        switch (brotliResult)
                        {
                            case 1: // Success
                                return OperationStatus.Done;
                            case 3: // NeedsMoreOutput
                                return OperationStatus.DestinationTooSmall;
                            case 2: // NeedsMoreInput
                            default:
                                // If more input is needed and no input is available, signal NeedMoreData.
                                if (brotliResult == 2 && inData.AvailableRead == 0)
                                    return OperationStatus.NeedMoreData;
                                break;
                        }
                    }
                }
                return OperationStatus.DestinationTooSmall;
            }
        }
    }
}

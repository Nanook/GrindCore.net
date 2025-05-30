using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System;

namespace Nanook.GrindCore.Brotli
{
    /// <summary>Provides non-allocating, performant Brotli decompression methods. The methods decompress in a single pass without using a <see cref="Nanook.GrindCore.BrotliStream" /> instance.</summary>
    internal struct BrotliDecoder : IDisposable
    {
        private SafeBrotliDecoderHandle? _state;
        private bool _disposed;

        internal void InitializeDecoder(CompressionVersion? version = null)
        {
            if (version == null)
                version = CompressionVersion.BrotliLatest();
            if (version.Index == 0)
                _state = Interop.Brotli.DN9_BRT_v1_1_0_DecoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            else
                throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
            _state.Version = version;
            if (_state.IsInvalid)
                throw new IOException(SR.BrotliDecoder_Create);
        }

        internal void EnsureInitialized()
        {
            EnsureNotDisposed();
            if (_state == null)
                InitializeDecoder();
        }

        /// <summary>Releases all resources used by the current Brotli decoder instance.</summary>
        public void Dispose()
        {
            _disposed = true;
            _state?.Dispose();
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BrotliDecoder), SR.BrotliDecoder_Disposed);
        }

        /// <summary>Decompresses data that was compressed using the Brotli algorithm.</summary>
        /// <param name="source">A buffer containing the compressed data.</param>
        /// <param name="destination">When this method returns, a byte span containing the decompressed data.</param>
        /// <param name="bytesConsumed">The total number of bytes that were read from <paramref name="source" />.</param>
        /// <param name="bytesWritten">The total number of bytes that were written in the <paramref name="destination" />.</param>
        /// <returns>One of the enumeration values that indicates the status of the decompression operation.</returns>
        /// <remarks>The return value can be as follows:
        /// - <see cref="OperationStatus.Done" />: <paramref name="source" /> was successfully and completely decompressed into <paramref name="destination" />.
        /// - <see cref="OperationStatus.DestinationTooSmall" />: There is not enough space in <paramref name="destination" /> to decompress <paramref name="source" />.
        /// - <see cref="OperationStatus.NeedMoreData" />: The decompression action is partially done at least one more byte is required to complete the decompression task. This method should be called again with more input to decompress.
        /// - <see cref="OperationStatus.InvalidData" />: The data in <paramref name="source" /> is invalid and could not be decompressed.</remarks>
        public OperationStatus Decompress(CompressionBuffer source, CompressionBuffer destination, out int bytesConsumed, out int bytesWritten)
        {
            EnsureInitialized();
            Debug.Assert(_state != null);

            bytesConsumed = 0;
            bytesWritten = 0;
            if (_state.Version.Index == 0)
            {
                if (Interop.Brotli.DN9_BRT_v1_1_0_DecoderIsFinished(_state!) != Interop.BOOL.FALSE)
                    return OperationStatus.Done;
            }
            else
                throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");

            UIntPtr availableOutput = (UIntPtr)destination.AvailableWrite;
            UIntPtr availableInput = (UIntPtr)source.AvailableRead;
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

                        int brotliResult;
                        if (_state.Version.Index == 0)
                            brotliResult = Interop.Brotli.DN9_BRT_v1_1_0_DecoderDecompressStream(_state, ref availableInput, &inBytes, ref availableOutput, &outBytes, out _);
                        else
                            throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");

                        if (brotliResult == 0) // Error
                            return OperationStatus.InvalidData;

                        bytesConsumed += source.AvailableRead - (int)availableInput;
                        bytesWritten += destination.AvailableWrite - (int)availableOutput;

                        source.Read(bytesConsumed);
                        destination.Write(bytesWritten); //update dest

                        switch (brotliResult)
                        {
                            case 1: // Success
                                return OperationStatus.Done;
                            case 3: // NeedsMoreOutput
                                return OperationStatus.DestinationTooSmall;
                            case 2: // NeedsMoreInput
                            default:
                                //source = new DataBlock(source.Data, source.AvailableRead, (int)availableInput);
                                if (brotliResult == 2 && source.AvailableRead == 0)
                                    return OperationStatus.NeedMoreData;
                                break;
                        }
                    }
                }
                return OperationStatus.DestinationTooSmall;
            }
        }

        /// <summary>Attempts to decompress data that was compressed with the Brotli algorithm.</summary>
        /// <param name="source">A buffer containing the compressed data.</param>
        /// <param name="destination">When this method returns, a byte span containing the decompressed data.</param>
        /// <param name="bytesWritten">The total number of bytes that were written in the <paramref name="destination" />.</param>
        /// <returns><see langword="true" /> on success; <see langword="false" /> otherwise.</returns>
        /// <remarks>If this method returns <see langword="false" />, <paramref name="destination" /> may be empty or contain partially decompressed data, with <paramref name="bytesWritten" /> being zero or greater than zero but less than the expected total.</remarks>
        public static unsafe bool TryDecompress(DataBlock source, DataBlock destination, out int bytesWritten, CompressionVersion? version = null)
        {
            fixed (byte* inBytes = source.Data)
            fixed (byte* outBytes = destination.Data)
            {
                *&inBytes += source.Offset;
                *&outBytes += destination.Offset;
                UIntPtr availableOutput = (UIntPtr)destination.Length;
                bool success;

                if (version == null)
                    version = CompressionVersion.BrotliLatest();

                if (version.Index == 0)
                    success = Interop.Brotli.DN9_BRT_v1_1_0_DecoderDecompress((UIntPtr)source.Length, inBytes, &availableOutput, outBytes) != Interop.BOOL.FALSE;
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");

                bytesWritten = (int)availableOutput;
                return success;
            }
        }
    }
}

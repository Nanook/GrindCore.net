


using System.Buffers;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System;

/* Unmerged change from project 'GrindCore.net (netstandard2.1)'
Added:
using Nanook;
using Nanook.GrindCore;
using Nanook.GrindCore.Brotli;
using Nanook.GrindCore.Brotli.enc;
using Nanook.GrindCore.Brotli;
*/

/* Unmerged change from project 'GrindCore.net (netstandard2.1)'
Added:
using Nanook;
using Nanook.GrindCore;
using Nanook.GrindCore.Brotli.enc;
*/

namespace Nanook.GrindCore.Brotli
{
    /// <summary>Provides methods and static methods to encode and decode data in a streamless, non-allocating, and performant manner using the Brotli data format specification.</summary>
    public partial struct BrotliEncoder : IDisposable
    {
        internal SafeBrotliEncoderHandle? _state;
        private bool _disposed;

        /// <summary>Initializes a new instance of the <see cref="System.IO.Compression.BrotliEncoder" /> structure using the specified quality and window.</summary>
        /// <param name="quality">A number representing quality of the Brotli compression. 0 is the minimum (no compression), 11 is the maximum.</param>
        /// <param name="window">A number representing the encoder window bits. The minimum value is 10, and the maximum value is 24.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="quality" /> is not between the minimum value of 0 and the maximum value of 11.
        /// -or-
        /// <paramref name="window" /> is not between the minimum value of 10 and the maximum value of 24.</exception>
        /// <exception cref="IOException">Failed to create the <see cref="System.IO.Compression.BrotliEncoder" /> instance.</exception>
        public BrotliEncoder(int quality, int window, CompressionVersion? version = null)
        {
            _disposed = false;
            if (version == null)
                version = CompressionVersion.BrotliLatest();
            if (version.Index == 0)
                _state = Interop.Brotli.DN8_Brotli_v1_0_9_EncoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            else
                throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
            _state!.Version = version;
            if (_state.IsInvalid)
                throw new IOException(SR.BrotliEncoder_Create);
            SetQuality(quality);
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
                _state = Interop.Brotli.DN8_Brotli_v1_0_9_EncoderCreateInstance(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
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
            {
                InitializeEncoder();
            }
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

        internal void SetQuality(int quality)
        {
            EnsureNotDisposed();
            if (_state == null || _state.IsInvalid || _state.IsClosed)
            {
                InitializeEncoder();
                Debug.Assert(_state != null && !_state.IsInvalid && !_state.IsClosed);
            }
            if (quality < BrotliUtils.Quality_Min || quality > BrotliUtils.Quality_Max)
            {
                throw new ArgumentOutOfRangeException(nameof(quality), SR.Format(SR.BrotliEncoder_Quality, quality, 0, BrotliUtils.Quality_Max));
            }
            if (_state.Version.Index == 0)
            {
                if (Interop.Brotli.DN8_Brotli_v1_0_9_EncoderSetParameter(_state, BrotliEncoderParameter.Quality, (uint)quality) == Interop.BOOL.FALSE)
                    throw new InvalidOperationException(SR.Format(SR.BrotliEncoder_InvalidSetParameter, "Quality"));
            }
            else
                throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");
        }

        internal void SetWindow(int window)
        {
            EnsureNotDisposed();
            if (_state == null || _state.IsInvalid || _state.IsClosed)
            {
                InitializeEncoder();
                Debug.Assert(_state != null && !_state.IsInvalid && !_state.IsClosed);
            }
            if (window < BrotliUtils.WindowBits_Min || window > BrotliUtils.WindowBits_Max)
            {
                throw new ArgumentOutOfRangeException(nameof(window), SR.Format(SR.BrotliEncoder_Window, window, BrotliUtils.WindowBits_Min, BrotliUtils.WindowBits_Max));
            }
            if (_state.Version.Index == 0)
            {
                if (Interop.Brotli.DN8_Brotli_v1_0_9_EncoderSetParameter(_state, BrotliEncoderParameter.LGWin, (uint)window) == Interop.BOOL.FALSE)
                    throw new InvalidOperationException(SR.Format(SR.BrotliEncoder_InvalidSetParameter, "Window"));
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
            if (inputSize > BrotliUtils.MaxInputSize)
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

        internal OperationStatus Flush(Memory<byte> destination, out int bytesWritten) => Flush(destination.Span, out bytesWritten);

        /// <summary>Compresses an empty read-only span of bytes into its destination, which ensures that output is produced for all the processed input. An actual flush is performed when the source is depleted and there is enough space in the destination for the remaining data.</summary>
        /// <param name="destination">When this method returns, a span of bytes where the compressed data will be stored.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="destination" />.</param>
        /// <returns>One of the enumeration values that describes the status with which the operation finished.</returns>
        public OperationStatus Flush(Span<byte> destination, out int bytesWritten) => Compress(ReadOnlySpan<byte>.Empty, destination, out _, out bytesWritten, BrotliEncoderOperation.Flush);

        internal OperationStatus Compress(ReadOnlyMemory<byte> source, Memory<byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock) => Compress(source.Span, destination.Span, out bytesConsumed, out bytesWritten, isFinalBlock);

        /// <summary>Compresses a read-only byte span into a destination span.</summary>
        /// <param name="source">A read-only span of bytes containing the source data to compress.</param>
        /// <param name="destination">When this method returns, a byte span where the compressed is stored.</param>
        /// <param name="bytesConsumed">When this method returns, the total number of bytes that were read from <paramref name="source" />.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="destination" />.</param>
        /// <param name="isFinalBlock"><see langword="true" /> to finalize the internal stream, which prevents adding more input data when this method returns; <see langword="false" /> to allow the encoder to postpone the production of output until it has processed enough input.</param>
        /// <returns>One of the enumeration values that describes the status with which the span-based operation finished.</returns>
        public OperationStatus Compress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesConsumed, out int bytesWritten, bool isFinalBlock) => Compress(source, destination, out bytesConsumed, out bytesWritten, isFinalBlock ? BrotliEncoderOperation.Finish : BrotliEncoderOperation.Process);

        internal OperationStatus Compress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesConsumed, out int bytesWritten, BrotliEncoderOperation operation)
        {
            EnsureInitialized();
            Debug.Assert(_state != null);

            bytesWritten = 0;
            bytesConsumed = 0;
            nuint availableOutput = (nuint)destination.Length;
            nuint availableInput = (nuint)source.Length;
            unsafe
            {
                // We can freely cast between int and nuint (.NET size_t equivalent) for two reasons:
                // 1. Interop Brotli functions will always return an availableInput/Output value lower or equal to the one passed to the function
                // 2. Span's have a maximum length of the int boundary.
                while ((int)availableOutput > 0)
                {
                    fixed (byte* inBytes = &MemoryMarshal.GetReference(source))
                    fixed (byte* outBytes = &MemoryMarshal.GetReference(destination))
                    {
                        if (_state.Version.Index == 0)
                        {
                            if (Interop.Brotli.DN8_Brotli_v1_0_9_EncoderCompressStream(_state!, operation, ref availableInput, &inBytes, ref availableOutput, &outBytes, out _) == Interop.BOOL.FALSE)
                                return OperationStatus.InvalidData;
                        }
                        else
                            throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");

                        Debug.Assert(availableInput <= (nuint)source.Length);
                        Debug.Assert(availableOutput <= (nuint)destination.Length);

                        bytesConsumed += source.Length - (int)availableInput;
                        bytesWritten += destination.Length - (int)availableOutput;

                        if (_state.Version.Index == 0)
                        {
                            // no bytes written, no remaining input to give to the encoder, and no output in need of retrieving means we are Done
                            if ((int)availableOutput == destination.Length && Interop.Brotli.DN8_Brotli_v1_0_9_EncoderHasMoreOutput(_state) == Interop.BOOL.FALSE && availableInput == 0)
                                return OperationStatus.Done;
                        }
                        else
                            throw new Exception($"{_state.Version.Algorithm} version {_state.Version.Version} is not supported");

                        source = source.Slice(source.Length - (int)availableInput);
                        destination = destination.Slice(destination.Length - (int)availableOutput);
                    }
                }

                return OperationStatus.DestinationTooSmall;
            }
        }

        /// <summary>Tries to compress a source byte span into a destination span.</summary>
        /// <param name="source">A read-only span of bytes containing the source data to compress.</param>
        /// <param name="destination">When this method returns, a span of bytes where the compressed data is stored.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="destination" />.</param>
        /// <returns><see langword="true" /> if the compression operation was successful; <see langword="false" /> otherwise.</returns>
        public static bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten) => TryCompress(source, destination, out bytesWritten, BrotliUtils.Quality_Default, BrotliUtils.WindowBits_Default);

        /// <summary>Tries to compress a source byte span into a destination byte span, using the provided compression quality leven and encoder window bits.</summary>
        /// <param name="source">A read-only span of bytes containing the source data to compress.</param>
        /// <param name="destination">When this method returns, a span of bytes where the compressed data is stored.</param>
        /// <param name="bytesWritten">When this method returns, the total number of bytes that were written to <paramref name="destination" />.</param>
        /// <param name="quality">A number representing quality of the Brotli compression. 0 is the minimum (no compression), 11 is the maximum.</param>
        /// <param name="window">A number representing the encoder window bits. The minimum value is 10, and the maximum value is 24.</param>
        /// <returns><see langword="true" /> if the compression operation was successful; <see langword="false" /> otherwise.</returns>
        public static bool TryCompress(ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten, int quality, int window, CompressionVersion? version = null)
        {
            if (quality < 0 || quality > BrotliUtils.Quality_Max)
            {
                throw new ArgumentOutOfRangeException(nameof(quality), SR.Format(SR.BrotliEncoder_Quality, quality, 0, BrotliUtils.Quality_Max));
            }
            if (window < BrotliUtils.WindowBits_Min || window > BrotliUtils.WindowBits_Max)
            {
                throw new ArgumentOutOfRangeException(nameof(window), SR.Format(SR.BrotliEncoder_Window, window, BrotliUtils.WindowBits_Min, BrotliUtils.WindowBits_Max));
            }

            unsafe
            {
                fixed (byte* inBytes = &MemoryMarshal.GetReference(source))
                fixed (byte* outBytes = &MemoryMarshal.GetReference(destination))
                {
                    nuint availableOutput = (nuint)destination.Length;
                    if (version == null)
                        version = CompressionVersion.BrotliLatest();

                    bool success;
                    if (version.Index == 0)
                        success = Interop.Brotli.DN8_Brotli_v1_0_9_EncoderCompress(quality, window, /*BrotliEncoderMode*/ 0, (nuint)source.Length, inBytes, &availableOutput, outBytes) != Interop.BOOL.FALSE;
                    else
                        throw new Exception($"{version.Algorithm} version {version.Version} is not supported");

                    Debug.Assert(success ? availableOutput <= (nuint)destination.Length : availableOutput == 0);

                    bytesWritten = (int)availableOutput;
                    return success;
                }
            }
        }
    }
}

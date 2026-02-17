using System.Runtime.InteropServices;
using System.Security;
using System;
using static Nanook.GrindCore.Interop.ZLib;
using System.Reflection.Emit;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// Provides declarations for constants, P/Invokes, and basic tools for exposing the native Nanook.GrindCore.Native.dll (effectively, ZLib) library to managed code.
    /// </summary>
    internal static partial class ZLibNative
    {
        /// <summary>
        /// Represents a safe handle for a native ZLib stream, managing the lifetime and state of the underlying z_stream structure.
        /// </summary>
        public sealed class ZLibStreamHandle : SafeHandle
        {
            /// <summary>
            /// Represents the initialization state of the ZLib stream.
            /// </summary>
            public enum State { NotInitialized, InitializedForDeflate, InitializedForInflate, Disposed }

            private Interop.ZStream _zStream;
            private volatile State _initializationState;

            /// <summary>
            /// Gets the compression version used by this stream.
            /// </summary>
            public CompressionVersion Version { get; }

            /// <summary>
            /// Initializes a new instance of the <see cref="ZLibStreamHandle"/> class for the specified compression version.
            /// </summary>
            /// <param name="version">The compression version to use.</param>
            public ZLibStreamHandle(CompressionVersion version)
                : base(new IntPtr(-1), true)
            {
                this.Version = version;
                _initializationState = State.NotInitialized;
                SetHandle(IntPtr.Zero);
            }

            /// <inheritdoc/>
            public override bool IsInvalid => handle == new IntPtr(-1);

            /// <summary>
            /// Gets the current initialization state of the stream.
            /// </summary>
            public State InitializationState => _initializationState;

            /// <inheritdoc/>
            /// <remarks>
            /// Releases the native z_stream and updates the state accordingly.
            /// </remarks>
            protected override bool ReleaseHandle() =>
                InitializationState switch
                {
                    State.NotInitialized => true,
                    State.InitializedForDeflate => (DeflateEnd() == ErrorCode.Ok),
                    State.InitializedForInflate => (InflateEnd() == ErrorCode.Ok),
                    State.Disposed => true,
                    _ => false,
                };

            /// <summary>
            /// Gets or sets the pointer to the next input byte.
            /// </summary>
            public IntPtr NextIn
            {
                get => _zStream.nextIn;
                set => _zStream.nextIn = value;
            }

            /// <summary>
            /// Gets or sets the number of bytes available at <see cref="NextIn"/>.
            /// </summary>
            public uint AvailIn
            {
                get => _zStream.availIn;
                set => _zStream.availIn = value;
            }

            /// <summary>
            /// Gets or sets the pointer to the next output byte.
            /// </summary>
            public IntPtr NextOut
            {
                get => _zStream.nextOut;
                set => _zStream.nextOut = value;
            }

            /// <summary>
            /// Gets or sets the number of bytes available at <see cref="NextOut"/>.
            /// </summary>
            public uint AvailOut
            {
                get => _zStream.availOut;
                set => _zStream.availOut = value;
            }

            /// <summary>
            /// Throws if the stream has been disposed.
            /// </summary>
            /// <exception cref="ObjectDisposedException">Thrown if the stream is disposed.</exception>
            private void ensureNotDisposed()
            {
                if (InitializationState == State.Disposed)
                    throw new ObjectDisposedException(nameof(ZLibStreamHandle));
            }

            /// <summary>
            /// Throws if the stream is not in the required state.
            /// </summary>
            /// <param name="requiredState">The required state.</param>
            /// <exception cref="InvalidOperationException">Thrown if the stream is not in the required state.</exception>
            private void ensureState(State requiredState)
            {
                if (InitializationState != requiredState)
                    throw new InvalidOperationException("InitializationState != " + requiredState.ToString());
            }

            /// <summary>
            /// Initializes the stream for deflate (compression) with the specified parameters.
            /// </summary>
            /// <param name="level">The compression level.</param>
            /// <param name="windowBits">The window bits parameter.</param>
            /// <param name="memLevel">The memory level parameter.</param>
            /// <param name="strategy">The compression strategy.</param>
            /// <returns>The error code returned by the native call.</returns>
            /// <exception cref="Exception">Thrown if the version is not supported.</exception>
            public unsafe ErrorCode DeflateInit2_(Interop.ZLib.CompressionLevel level, int windowBits, int memLevel, CompressionStrategy strategy)
            {
                ensureNotDisposed();
                ensureState(State.NotInitialized);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    ErrorCode errC;
                    if (this.Version == null || this.Version.Index == 0)
                        errC = Interop.ZLib.DN9_ZLibNg_v2_2_1_DeflateInit2_(stream, level, Interop.ZLib.CompressionMethod.Deflated, windowBits, memLevel, strategy);
                    else if (this.Version.Index == 1)
                        errC = Interop.ZLib.DN8_ZLib_v1_3_1_DeflateInit2_(stream, level, Interop.ZLib.CompressionMethod.Deflated, windowBits, memLevel, strategy);
                    else
                        throw new Exception($"{Version.Algorithm} version {Version.Version} is not supported");

                    _initializationState = State.InitializedForDeflate;
                    return errC;
                }
            }

            /// <summary>
            /// Performs a deflate (compression) operation.
            /// </summary>
            /// <param name="flush">The flush code to use.</param>
            /// <returns>The error code returned by the native call.</returns>
            /// <exception cref="Exception">Thrown if the version is not supported.</exception>
            public unsafe ErrorCode Deflate(FlushCode flush)
            {
                ensureNotDisposed();
                ensureState(State.InitializedForDeflate);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    if (this.Version == null || this.Version.Index == 0)
                        return Interop.ZLib.DN9_ZLibNg_v2_2_1_Deflate(stream, flush);
                    else if (this.Version.Index == 1)
                        return Interop.ZLib.DN8_ZLib_v1_3_1_Deflate(stream, flush);
                    else
                        throw new Exception($"{Version.Algorithm} version {Version.Version} is not supported");
                }
            }

            /// <summary>
            /// Ends the deflate (compression) operation and releases resources.
            /// </summary>
            /// <returns>The error code returned by the native call.</returns>
            /// <exception cref="Exception">Thrown if the version is not supported.</exception>
            public unsafe ErrorCode DeflateEnd()
            {
                ensureNotDisposed();
                ensureState(State.InitializedForDeflate);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    ErrorCode errC;
                    if (this.Version == null || this.Version.Index == 0)
                        errC = Interop.ZLib.DN9_ZLibNg_v2_2_1_DeflateEnd(stream);
                    else if (this.Version.Index == 1)
                        errC = Interop.ZLib.DN8_ZLib_v1_3_1_DeflateEnd(stream);
                    else
                        throw new Exception($"{Version.Algorithm} version {Version.Version} is not supported");
                    _initializationState = State.Disposed;
                    return errC;
                }
            }

            /// <summary>
            /// Initializes the stream for inflate (decompression) with the specified window bits.
            /// </summary>
            /// <param name="windowBits">The window bits parameter.</param>
            /// <returns>The error code returned by the native call.</returns>
            /// <exception cref="Exception">Thrown if the version is not supported.</exception>
            public unsafe ErrorCode InflateInit2_(int windowBits)
            {
                ensureNotDisposed();
                ensureState(State.NotInitialized);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    ErrorCode errC;
                    if (this.Version == null || this.Version.Index == 0)
                        errC = Interop.ZLib.DN9_ZLibNg_v2_2_1_InflateInit2_(stream, windowBits);
                    else if (this.Version.Index == 1)
                        errC = Interop.ZLib.DN8_ZLib_v1_3_1_InflateInit2_(stream, windowBits);
                    else
                        throw new Exception($"{Version.Algorithm} version {Version.Version} is not supported");
                    _initializationState = State.InitializedForInflate;
                    return errC;
                }
            }

            /// <summary>
            /// Performs an inflate (decompression) operation.
            /// </summary>
            /// <param name="flush">The flush code to use.</param>
            /// <returns>The error code returned by the native call.</returns>
            /// <exception cref="Exception">Thrown if the version is not supported.</exception>
            public unsafe ErrorCode Inflate(FlushCode flush)
            {
                ensureNotDisposed();
                ensureState(State.InitializedForInflate);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    if (this.Version == null || this.Version.Index == 0)
                        return Interop.ZLib.DN9_ZLibNg_v2_2_1_Inflate(stream, flush);
                    else if (this.Version.Index == 1)
                        return Interop.ZLib.DN8_ZLib_v1_3_1_Inflate(stream, flush);
                    else
                        throw new Exception($"{Version.Algorithm} version {Version.Version} is not supported");
                }
            }

            /// <summary>
            /// Ends the inflate (decompression) operation and releases resources.
            /// </summary>
            /// <returns>The error code returned by the native call.</returns>
            /// <exception cref="Exception">Thrown if the version is not supported.</exception>
            public unsafe ErrorCode InflateEnd()
            {
                ensureNotDisposed();
                ensureState(State.InitializedForInflate);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    ErrorCode errC;
                    if (this.Version == null || this.Version.Index == 0)
                        errC = Interop.ZLib.DN9_ZLibNg_v2_2_1_InflateEnd(stream);
                    else if (this.Version.Index == 1)
                        errC = Interop.ZLib.DN8_ZLib_v1_3_1_InflateEnd(stream);
                    else
                        throw new Exception($"{Version.Algorithm} version {Version.Version} is not supported");
                    _initializationState = State.Disposed;
                    return errC;
                }
            }

            /// <summary>
            /// Gets the last error message from the native z_stream, if available.
            /// </summary>
            /// <returns>The error message string, or an empty string if none is available.</returns>
            public string GetErrorMessage() => _zStream.msg != ZNullPtr ?
#if NETCOREAPP || NETSTANDARD2_1_OR_GREATER
                Marshal.PtrToStringUTF8(_zStream.msg)!
#else
                Marshal.PtrToStringBSTR(_zStream.msg)!
#endif
                : string.Empty;
        }

        /// <summary>
        /// Creates a new ZLib stream for deflate (compression) with the specified parameters.
        /// </summary>
        /// <param name="zLibStreamHandle">The resulting ZLib stream handle.</param>
        /// <param name="level">The compression level.</param>
        /// <param name="windowBits">The window bits parameter.</param>
        /// <param name="memLevel">The memory level parameter.</param>
        /// <param name="strategy">The compression strategy.</param>
        /// <param name="version">The compression version to use.</param>
        /// <returns>The error code returned by the native call.</returns>
        public static ErrorCode CreateZLibStreamForDeflate(out ZLibStreamHandle zLibStreamHandle, Interop.ZLib.CompressionLevel level,
            int windowBits, int memLevel, CompressionStrategy strategy, CompressionVersion version)
        {
            zLibStreamHandle = new ZLibStreamHandle(version);
            return zLibStreamHandle.DeflateInit2_(level, windowBits, memLevel, strategy);
        }

        /// <summary>
        /// Creates a new ZLib stream for inflate (decompression) with the specified parameters.
        /// </summary>
        /// <param name="zLibStreamHandle">The resulting ZLib stream handle.</param>
        /// <param name="windowBits">The window bits parameter.</param>
        /// <param name="version">The compression version to use.</param>
        /// <returns>The error code returned by the native call.</returns>
        public static ErrorCode CreateZLibStreamForInflate(out ZLibStreamHandle zLibStreamHandle, int windowBits, CompressionVersion version)
        {
            zLibStreamHandle = new ZLibStreamHandle(version);
            return zLibStreamHandle.InflateInit2_(windowBits);
        }
    }
}

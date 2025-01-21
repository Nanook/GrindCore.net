


using System.Runtime.InteropServices;
using System.Security;
using System;
using static Nanook.GrindCore.Interop.ZLib;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// This class provides declaration for constants and PInvokes as well as some basic tools for exposing the
    /// native Nanook.GrindCore.Native.dll (effectively, ZLib) library to managed code.
    ///
    /// See also: How to choose a compression level (in comments to <code>CompressionLevel</code>.
    /// </summary>
    internal static partial class ZLibNgNative
    {


        /// <summary>
        /// The <code>ZLibStreamHandle</code> could be a <code>CriticalFinalizerObject</code> rather than a
        /// <code>SafeHandleMinusOneIsInvalid</code>. This would save an <code>IntPtr</code> field since
        /// <code>ZLibStreamHandle</code> does not actually use its <code>handle</code> field.
        /// Instead it uses a <code>private ZStream zStream</code> field which is the actual handle data
        /// structure requiring critical finalization.
        /// However, we would like to take advantage if the better debugability offered by the fact that a
        /// <em>releaseHandleFailed MDA</em> is raised if the <code>ReleaseHandle</code> method returns
        /// <code>false</code>, which can for instance happen if the underlying ZLib <code>XxxxEnd</code>
        /// routines return an failure error code.
        /// </summary>
        public sealed class ZLibNgStreamHandle : SafeHandle
        {
            public enum State { NotInitialized, InitializedForDeflate, InitializedForInflate, Disposed }

            private Interop.ZStream _zStream;

            private volatile State _initializationState;


            public ZLibNgStreamHandle()
                : base(new IntPtr(-1), true)
            {
                _initializationState = State.NotInitialized;
                SetHandle(IntPtr.Zero);
            }

            public override bool IsInvalid
            {
                get { return handle == new IntPtr(-1); }
            }

            public State InitializationState
            {
                get { return _initializationState; }
            }


            protected override bool ReleaseHandle() =>
                InitializationState switch
                {
                    State.NotInitialized => true,
                    State.InitializedForDeflate => (DeflateEnd() == ErrorCode.Ok),
                    State.InitializedForInflate => (InflateEnd() == ErrorCode.Ok),
                    State.Disposed => true,
                    _ => false,  // This should never happen. Did we forget one of the State enum values in the switch?
                };

            public IntPtr NextIn
            {
                get { return _zStream.nextIn; }
                set { _zStream.nextIn = value; }
            }

            public uint AvailIn
            {
                get { return _zStream.availIn; }
                set { _zStream.availIn = value; }
            }

            public IntPtr NextOut
            {
                get { return _zStream.nextOut; }
                set { _zStream.nextOut = value; }
            }

            public uint AvailOut
            {
                get { return _zStream.availOut; }
                set { _zStream.availOut = value; }
            }

            private void EnsureNotDisposed()
            {
                if (InitializationState == State.Disposed)
                    throw new ObjectDisposedException(nameof(ZLibNgStreamHandle));
            }

            private void EnsureState(State requiredState)
            {
                if (InitializationState != requiredState)
                    throw new InvalidOperationException("InitializationState != " + requiredState.ToString());
            }


            public unsafe ErrorCode DeflateInit2_(Interop.ZLib.CompressionLevel level, int windowBits, int memLevel, CompressionStrategy strategy)
            {
                EnsureNotDisposed();
                EnsureState(State.NotInitialized);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    ErrorCode errC = Interop.ZLib.DN9_ZLibNg_v2_2_1_DeflateInit2_(stream, level, Interop.ZLib.CompressionMethod.Deflated, windowBits, memLevel, strategy);
                    _initializationState = State.InitializedForDeflate;

                    return errC;
                }
            }


            public unsafe ErrorCode Deflate(FlushCode flush)
            {
                EnsureNotDisposed();
                EnsureState(State.InitializedForDeflate);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    return Interop.ZLib.DN9_ZLibNg_v2_2_1_Deflate(stream, flush);
                }
            }


            public unsafe ErrorCode DeflateEnd()
            {
                EnsureNotDisposed();
                EnsureState(State.InitializedForDeflate);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    ErrorCode errC = Interop.ZLib.DN9_ZLibNg_v2_2_1_DeflateEnd(stream);
                    _initializationState = State.Disposed;

                    return errC;
                }
            }


            public unsafe ErrorCode InflateInit2_(int windowBits)
            {
                EnsureNotDisposed();
                EnsureState(State.NotInitialized);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    ErrorCode errC = Interop.ZLib.DN9_ZLibNg_v2_2_1_InflateInit2_(stream, windowBits);
                    _initializationState = State.InitializedForInflate;

                    return errC;
                }
            }


            public unsafe ErrorCode Inflate(FlushCode flush)
            {
                EnsureNotDisposed();
                EnsureState(State.InitializedForInflate);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    return Interop.ZLib.DN9_ZLibNg_v2_2_1_Inflate(stream, flush);
                }
            }


            public unsafe ErrorCode InflateEnd()
            {
                EnsureNotDisposed();
                EnsureState(State.InitializedForInflate);

                fixed (Interop.ZStream* stream = &_zStream)
                {
                    ErrorCode errC = Interop.ZLib.DN9_ZLibNg_v2_2_1_InflateEnd(stream);
                    _initializationState = State.Disposed;

                    return errC;
                }
            }

            // This can work even after XxflateEnd().
            public string GetErrorMessage() => _zStream.msg != ZNullPtr ? Marshal.PtrToStringUTF8(_zStream.msg)! : string.Empty;
        }

        public static ErrorCode CreateZLibStreamForDeflate(out ZLibNgStreamHandle zLibStreamHandle, Interop.ZLib.CompressionLevel level,
            int windowBits, int memLevel, CompressionStrategy strategy)
        {
            zLibStreamHandle = new ZLibNgStreamHandle();
            return zLibStreamHandle.DeflateInit2_(level, windowBits, memLevel, strategy);
        }


        public static ErrorCode CreateZLibStreamForInflate(out ZLibNgStreamHandle zLibStreamHandle, int windowBits)
        {
            zLibStreamHandle = new ZLibNgStreamHandle();
            return zLibStreamHandle.InflateInit2_(windowBits);
        }

    }
}

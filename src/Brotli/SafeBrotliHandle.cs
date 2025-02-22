


using System;
using System.Runtime.InteropServices;

namespace Nanook.GrindCore.Brotli
{
    internal sealed class SafeBrotliEncoderHandle : SafeHandle
    {
        public SafeBrotliEncoderHandle() : base(IntPtr.Zero, true) { }

        protected override bool ReleaseHandle()
        {
            if (Version.Index == 0)
                Interop.Brotli.DN9_BRT_v1_1_0_EncoderDestroyInstance(handle);
            return true;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public CompressionVersion Version { get; set; }
    }

    internal sealed class SafeBrotliDecoderHandle : SafeHandle
    {
        public SafeBrotliDecoderHandle() : base(IntPtr.Zero, true) { }

        protected override bool ReleaseHandle()
        {
            if (Version.Index == 0)
                Interop.Brotli.DN9_BRT_v1_1_0_DecoderDestroyInstance(handle);
            return true;
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        public CompressionVersion Version { get; set; }
    }
}

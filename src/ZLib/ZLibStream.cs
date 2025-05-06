


using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.IO;
using System;
using Nanook.GrindCore.DeflateZLib;

namespace Nanook.GrindCore.ZLib
{
    public class ZLibStream : DeflateStream
    {
        public ZLibStream(Stream stream, CompressionOptions options) : base(stream, options, Interop.ZLib.ZLib_DefaultWindowBits)
        {
        }

    }
}

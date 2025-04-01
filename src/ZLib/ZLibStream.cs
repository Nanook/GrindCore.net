


using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.IO;
using System;
using Nanook.GrindCore.DeflateZLib;

namespace Nanook.GrindCore.ZLib
{
    public class ZLibStream : DeflateStream
    {
        public ZLibStream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, leaveOpen: false, version)
        {
        }

        public ZLibStream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null) : base(stream, type, leaveOpen, Interop.ZLib.ZLib_DefaultWindowBits, version)
        {
        }

    }
}

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;
using Nanook.GrindCore.DeflateZLib;

/* Unmerged change from project 'GrindCore.net (netstandard2.1)'
Added:
using Nanook;
using Nanook.GrindCore;
using Nanook.GrindCore.GZip;
*/

namespace Nanook.GrindCore.GZip
{
    public class GZipStream : DeflateStream
    {
        public GZipStream(Stream stream, CompressionType type, CompressionVersion? version = null) : this(stream, type, leaveOpen: false, version)
        {
        }

        public GZipStream(Stream stream, CompressionType type, bool leaveOpen, CompressionVersion? version = null) : base(stream, type, leaveOpen, Interop.ZLib.GZip_DefaultWindowBits, version)
        {
        }

    }
}

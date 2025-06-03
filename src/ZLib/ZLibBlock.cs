

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;
using System.Reflection.Emit;

namespace Nanook.GrindCore.DeflateZLib
{
    public class ZLibBlock : DeflateBlock
    {
        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.ZLib;

        public ZLibBlock(CompressionOptions options) : base(options, Interop.ZLib.ZLib_DefaultWindowBits)
        {
        }
    }
}

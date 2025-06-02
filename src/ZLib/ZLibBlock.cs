

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
        public ZLibBlock(CompressionAlgorithm algorithm, CompressionOptions options) : base(algorithm, options, Interop.ZLib.ZLib_DefaultWindowBits)
        {
        }
    }
}

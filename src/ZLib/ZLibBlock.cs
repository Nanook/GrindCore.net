using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;
using System.Reflection.Emit;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// Provides a block-based implementation of the ZLib compression algorithm using ZLibNg.
    /// </summary>
    public class ZLibBlock : DeflateBlock
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZLibBlock"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        public ZLibBlock(CompressionOptions options)
            : base(CompressionAlgorithm.ZLibNg, options, Interop.ZLib.ZLib_DefaultWindowBits)
        {
        }
    }
}


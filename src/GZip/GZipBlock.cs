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
    /// Provides a Deflate block implementation configured for GZip (GZipNg) compression and decompression.
    /// </summary>
    public class GZipBlock : DeflateBlock
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GZipBlock"/> class with the specified compression options.
        /// </summary>
        /// <param name="options">The compression options to use.</param>
        public GZipBlock(CompressionOptions options)
            : base(CompressionAlgorithm.GZipNg, options, Interop.ZLib.GZip_DefaultWindowBits)
        {
        }
    }
}

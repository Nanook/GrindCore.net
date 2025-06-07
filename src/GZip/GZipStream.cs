using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;
using Nanook.GrindCore.DeflateZLib;

namespace Nanook.GrindCore.GZip
{
    /// <summary>
    /// Provides a stream for GZip (GZipNg) compression and decompression, using DeflateStream with GZip-specific window bits.
    /// </summary>
    public class GZipStream : DeflateStream
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GZipStream"/> class with the specified stream and compression options.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        public GZipStream(Stream stream, CompressionOptions options) : base(stream, CompressionAlgorithm.GZipNg, options, Interop.ZLib.GZip_DefaultWindowBits)
        {
        }
    }
}


using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.IO;
using System;
using Nanook.GrindCore.DeflateZLib;

namespace Nanook.GrindCore.ZLib
{
    /// <summary>
    /// Provides a stream for ZLib compression and decompression, using the default ZLib window bits.
    /// Inherits from <see cref="DeflateStream"/>.
    /// </summary>
    public class ZLibStream : DeflateStream
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ZLibStream"/> class with the specified stream and compression options.
        /// </summary>
        /// <param name="stream">The underlying stream to read from or write to.</param>
        /// <param name="options">The compression options to use.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="stream"/> or <paramref name="options"/> is null.</exception>
        public ZLibStream(Stream stream, CompressionOptions options) : base(stream, CompressionAlgorithm.ZLibNg, options, Interop.ZLib.ZLib_DefaultWindowBits)
        {
        }
    }
}


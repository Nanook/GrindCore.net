using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore.ZLib
{
    /// <summary>
    /// Provides static methods for compressing and decompressing data using ZLib and ZLibNg.
    /// </summary>
    public class ZLib
    {
        /// <summary>
        /// Compresses data using ZLib or ZLibNg.
        /// </summary>
        /// <param name="dest">The destination buffer for compressed data.</param>
        /// <param name="destOffset">The offset in the destination buffer to start writing.</param>
        /// <param name="destLen">The maximum number of bytes to write to the destination buffer.</param>
        /// <param name="source">The source buffer containing data to compress.</param>
        /// <param name="sourceOffset">The offset in the source buffer to start reading.</param>
        /// <param name="sourceLen">The number of bytes to read from the source buffer.</param>
        /// <param name="version">The compression version to use (null for default).</param>
        /// <returns>The number of bytes written to the destination buffer.</returns>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        public static unsafe int Compress(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, CompressionVersion? version = null)
        {
            fixed (byte* d = dest, s = source)
            {
                uint destLen2 = (uint)destLen;
                int ret;
                if (version == null || version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen);
                else if (version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Compress(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen);
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
                return (int)destLen2;
            }
        }

        /// <summary>
        /// Compresses data using ZLib or ZLibNg with a specified compression level.
        /// </summary>
        /// <param name="dest">The destination buffer for compressed data.</param>
        /// <param name="destOffset">The offset in the destination buffer to start writing.</param>
        /// <param name="destLen">The maximum number of bytes to write to the destination buffer.</param>
        /// <param name="source">The source buffer containing data to compress.</param>
        /// <param name="sourceOffset">The offset in the source buffer to start reading.</param>
        /// <param name="sourceLen">The number of bytes to read from the source buffer.</param>
        /// <param name="level">The compression level to use.</param>
        /// <param name="version">The compression version to use (null for default).</param>
        /// <returns>The number of bytes written to the destination buffer.</returns>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        public static unsafe int Compress2(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, int level, CompressionVersion? version = null)
        {
            fixed (byte* d = dest, s = source)
            {
                uint destLen2 = (uint)destLen;
                int ret;
                if (version == null || version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress2(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen, level);
                else if (version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Compress2(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen, level);
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
                return (int)destLen2;
            }
        }

        /// <summary>
        /// Compresses data using ZLib or ZLibNg with advanced options.
        /// </summary>
        /// <param name="dest">The destination buffer for compressed data.</param>
        /// <param name="destOffset">The offset in the destination buffer to start writing.</param>
        /// <param name="destLen">The maximum number of bytes to write to the destination buffer.</param>
        /// <param name="source">The source buffer containing data to compress.</param>
        /// <param name="sourceOffset">The offset in the source buffer to start reading.</param>
        /// <param name="sourceLen">The number of bytes to read from the source buffer.</param>
        /// <param name="level">The compression level to use.</param>
        /// <param name="windowBits">The window bits parameter for the compression algorithm.</param>
        /// <param name="strategy">The compression strategy to use.</param>
        /// <param name="version">The compression version to use (null for default).</param>
        /// <returns>The number of bytes written to the destination buffer.</returns>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        public static unsafe int Compress3(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, int level, int windowBits, int strategy, CompressionVersion? version = null)
        {
            fixed (byte* d = dest, s = source)
            {
                uint destLen2 = (uint)destLen;
                int ret;
                if (version == null || version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress3(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen, level, windowBits, 9, strategy);
                else if (version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Compress3(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen, level, windowBits, 9, strategy);
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
                return (int)destLen2;
            }
        }

        /// <summary>
        /// Decompresses data using ZLib or ZLibNg.
        /// </summary>
        /// <param name="dest">The destination buffer for decompressed data.</param>
        /// <param name="destOffset">The offset in the destination buffer to start writing.</param>
        /// <param name="destLen">The maximum number of bytes to write to the destination buffer.</param>
        /// <param name="source">The source buffer containing data to decompress.</param>
        /// <param name="sourceOffset">The offset in the source buffer to start reading.</param>
        /// <param name="sourceLen">The number of bytes to read from the source buffer.</param>
        /// <param name="version">The compression version to use (null for default).</param>
        /// <returns>The number of bytes written to the destination buffer.</returns>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        public static unsafe int Uncompress(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, CompressionVersion? version = null)
        {
            fixed (byte* d = dest, s = source)
            {
                uint sourceLen2 = (uint)sourceLen;
                uint destLen2 = (uint)destLen;
                if (version == null || version.Index == 0)
                    Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress(d + destOffset, ref destLen2, s + sourceOffset, sourceLen2);
                else if (version.Index == 1)
                    Interop.ZLib.DN8_ZLib_v1_3_1_Uncompress(d + destOffset, ref destLen2, s + sourceOffset, sourceLen2);
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
                return (int)destLen2;
            }
        }

        /// <summary>
        /// Decompresses data using ZLib or ZLibNg, updating the source length.
        /// </summary>
        /// <param name="dest">The destination buffer for decompressed data.</param>
        /// <param name="destOffset">The offset in the destination buffer to start writing.</param>
        /// <param name="destLen">The maximum number of bytes to write to the destination buffer.</param>
        /// <param name="source">The source buffer containing data to decompress.</param>
        /// <param name="sourceOffset">The offset in the source buffer to start reading.</param>
        /// <param name="sourceLen">The number of bytes to read from the source buffer.</param>
        /// <param name="version">The compression version to use (null for default).</param>
        /// <returns>The number of bytes written to the destination buffer.</returns>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        public static unsafe int Uncompress2(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, CompressionVersion? version = null)
        {
            fixed (byte* d = dest, s = source)
            {
                uint sourceLen2 = (uint)sourceLen;
                uint destLen2 = (uint)destLen;
                if (version == null || version.Index == 0)
                    Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress2(d + destOffset, ref destLen2, s + sourceOffset, ref sourceLen2);
                else if (version.Index == 1)
                    Interop.ZLib.DN8_ZLib_v1_3_1_Uncompress2(d + destOffset, ref destLen2, s + sourceOffset, ref sourceLen2);
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
                return (int)destLen2;
            }
        }

        /// <summary>
        /// Decompresses data using ZLib or ZLibNg with a specified window bits parameter.
        /// </summary>
        /// <param name="dest">The destination buffer for decompressed data.</param>
        /// <param name="destOffset">The offset in the destination buffer to start writing.</param>
        /// <param name="destLen">The maximum number of bytes to write to the destination buffer.</param>
        /// <param name="source">The source buffer containing data to decompress.</param>
        /// <param name="sourceOffset">The offset in the source buffer to start reading.</param>
        /// <param name="sourceLen">The number of bytes to read from the source buffer.</param>
        /// <param name="windowBits">The window bits parameter for the decompression algorithm.</param>
        /// <param name="version">The compression version to use (null for default).</param>
        /// <returns>The number of bytes written to the destination buffer.</returns>
        /// <exception cref="Exception">Thrown if the specified version is not supported.</exception>
        public static unsafe int Uncompress3(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, int windowBits, CompressionVersion? version = null)
        {
            fixed (byte* d = dest, s = source)
            {
                uint sourceLen2 = (uint)sourceLen;
                uint destLen2 = (uint)destLen;
                if (version == null || version.Index == 0)
                    Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress3(d + destOffset, ref destLen2, s + sourceOffset, ref sourceLen2, windowBits);
                else if (version.Index == 1)
                    Interop.ZLib.DN8_ZLib_v1_3_1_Uncompress3(d + destOffset, ref destLen2, s + sourceOffset, ref sourceLen2, windowBits);
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
                return (int)destLen2;
            }
        }
    }
}

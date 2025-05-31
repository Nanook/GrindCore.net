

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System;
using System.Reflection.Emit;

namespace Nanook.GrindCore.DeflateZLib
{
    public class DeflateBlock : CompressionBlock
    {
        private readonly int _windowBits;

        public override int RequiredCompressOutputSize { get; }

        public DeflateBlock(CompressionAlgorithm algorithm, CompressionOptions options) : this(algorithm, options, Interop.ZLib.Deflate_DefaultWindowBits)
        {
        }

        internal DeflateBlock(CompressionAlgorithm algorithm, CompressionOptions options, int windowBits) : base(algorithm, options)
        {
            _windowBits = windowBits;
            int sourceLen = (int)options.BlockSize!;
            RequiredCompressOutputSize = sourceLen + (sourceLen >> 12) + (sourceLen >> 14) + (sourceLen >> 25) + 13;
        }

        internal unsafe override int OnCompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* s = srcData.Data)
            fixed (byte* d = dstData.Data)
            {
                *&s += srcData.Offset;
                *&d += dstData.Offset;

                uint dstLen = (uint)dstData.Length;
                int ret;
                if (base.Options.Version == null || base.Options.Version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress3(d, ref dstLen, s, (uint)srcData.Length, (int)Options.Type, _windowBits, 9, 0);
                else if (base.Options.Version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Compress3(d, ref dstLen, s, (uint)srcData.Length, (int)Options.Type, _windowBits, 9, 0);
                else
                    throw new Exception($"{base.Options.Version.Algorithm} version {base.Options.Version.Version} is not supported");
                return (int)dstLen;
            }
        }

        internal unsafe override int OnDecompress(DataBlock srcData, DataBlock dstData)
        {
            fixed (byte* s = srcData.Data)
            fixed (byte* d = dstData.Data)
            {
                *&s += srcData.Offset;
                *&d += dstData.Offset;

                uint srcLen = (uint)srcData.Length;
                uint dstLen = (uint)dstData.Length;
                int ret;
                if (base.Options.Version == null || base.Options.Version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress3(d, ref dstLen, s, ref srcLen);
                else if (base.Options.Version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Uncompress3(d, ref dstLen, s, ref srcLen);
                else
                    throw new Exception($"{base.Options.Version.Algorithm} version {base.Options.Version.Version} is not supported");
                return (int)dstLen;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore.ZLib
{
    public class ZLib
    {
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

        public static unsafe int Compress3(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, int level, int strategy, bool header, CompressionVersion? version = null)
        {
            fixed (byte* d = dest, s = source)
            {
                uint destLen2 = (uint)destLen;
                int ret;
                if (version == null || version.Index == 0)
                    ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress3(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen, level, header ? 15 : -15, 9, strategy);
                else if (version.Index == 1)
                    ret = Interop.ZLib.DN8_ZLib_v1_3_1_Compress3(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen, level, header ? 15 : -15, 9, strategy);
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
                return (int)destLen2;
            }
        }

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

        public static unsafe int Uncompress3(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, CompressionVersion? version = null)
        {
            fixed (byte* d = dest, s = source)
            {
                uint sourceLen2 = (uint)sourceLen;
                uint destLen2 = (uint)destLen;
                if (version == null || version.Index == 0)
                    Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress3(d + destOffset, ref destLen2, s + sourceOffset, ref sourceLen2);
                else if (version.Index == 1)
                    Interop.ZLib.DN8_ZLib_v1_3_1_Uncompress3(d + destOffset, ref destLen2, s + sourceOffset, ref sourceLen2);
                else
                    throw new Exception($"{version.Algorithm} version {version.Version} is not supported");
                return (int)destLen2;
            }
        }

    }
}

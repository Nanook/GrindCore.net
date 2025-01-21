using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nanook.GrindCore.ZLib
{
    public class ZLibNg
    {
        public static unsafe int Compress(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen)
        {
            fixed (byte* d = dest, s = source)
            {
                uint destLen2 = (uint)destLen;
                int ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen);
                return (int)destLen2;
            }
        }

        public static unsafe int Compress2(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, int level)
        {
            fixed (byte* d = dest, s = source)
            {
                uint destLen2 = (uint)destLen;
                int ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress2(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen, level);
                return (int)destLen2;
            }
        }

        public static unsafe int Compress3(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen, int level, int strategy, bool header)
        {
            fixed (byte* d = dest, s = source)
            {
                uint destLen2 = (uint)destLen;
                int ret = Interop.ZLib.DN9_ZLibNg_v2_2_1_Compress3(d + destOffset, ref destLen2, s + sourceOffset, (uint)sourceLen, level, header ? 15 : -15, 9, strategy);
                return (int)destLen2;
            }
        }

        public static unsafe int Uncompress(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen)
        {
            fixed (byte* d = dest, s = source)
            {
                uint sourceLen2 = (uint)sourceLen;
                uint destLen2 = (uint)destLen;
                Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress(d + destOffset, ref destLen2, s + sourceOffset, sourceLen2);
                return (int)destLen2;
            }
        }

        public static unsafe int Uncompress2(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen)
        {
            fixed (byte* d = dest, s = source)
            {
                uint sourceLen2 = (uint)sourceLen;
                uint destLen2 = (uint)destLen;
                Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress2(d + destOffset, ref destLen2, s + sourceOffset, ref sourceLen2);
                return (int)destLen2;
            }
        }

        public static unsafe int Uncompress3(byte[] dest, int destOffset, int destLen, byte[] source, int sourceOffset, int sourceLen)
        {
            fixed (byte* d = dest, s = source)
            {
                uint sourceLen2 = (uint)sourceLen;
                uint destLen2 = (uint)destLen;
                Interop.ZLib.DN9_ZLibNg_v2_2_1_Uncompress3(d + destOffset, ref destLen2, s + sourceOffset, ref sourceLen2);
                return (int)destLen2;
            }
        }

    }
}

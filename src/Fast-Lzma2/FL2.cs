using FxResources.Nanook.GrindCore;
using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using static Nanook.GrindCore.Lzma.Interop;

namespace Nanook.GrindCore.Lzma
{
    internal static class FL2
    {
        #region Properties

        public static readonly Version Version = new Version(1, 0, 1);
        public static int VersionNumber => Version.Major * 100 * 100 + Version.Minor * 100 + Version.Build;
        public static string VersionString => $"{Version.Major}.{Version.Minor}.{Version.Build}";
        public static int MaxThreads => _maxThreads;
        public static int DictSizeMin => 1 << 20; //pow(2,20)
        public static int DictSizeMax => IntPtr.Size == 4 ? 1 << 27 : 1 << 30; //pow(2,30)

        public static int BlockOverlapMin => 0;
        public static int BlockOverlapMax => 14;

        public static int ResetIntervalMin => 1;
        public static int ResetIntervalMax => 16;

        public static int BufferResizeMin => 0;
        public static int BufferResizeMax => 4;
        public static int BufferResizeDefault => 2;

        public static int ChainLogMin => 4;
        public static int ChainLogMax => 14;

        public static int HybridCyclesMin => 1;
        public static int HybridCyclesMax => 64;

        public static int SearchDepthMin => 6;
        public static int SearchDepthMax => 254;

        public static int FastLengthMin => 6;
        public static int FastLengthMax => 273;

        public static int LCMin => 0;
        public static int LCMax => 4;

        public static int LPMin => 0;
        public static int LPMax => 4;

        public static int PBMin => 0;
        public static int PBMax => 4;
        public static int LCLP_MAX => 4;

        /// <summary>
        /// maximum compression level available
        /// </summary>
        public static int CompressionLevelMax => Interop.FastLzma2.FL2_maxCLevel();

        /// <summary>
        /// maximum compression level available in high mode
        /// </summary>
        public static int HighCompressionLevelMax => Interop.FastLzma2.FL2_maxHighCLevel();

        #endregion Properties

        private const int _maxThreads = 200;

        public static FL2CompressionParameters GetPresetLevelParameters(int level, int high)
        {
            FL2CompressionParameters parameters = new();
            nuint code = Interop.FastLzma2.FL2_getLevelParameters(level, high, ref parameters);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return parameters;
        }

        /// <summary>
        /// maximum compressed size in worst case scenario
        /// </summary>
        /// <param name="src"></param>
        /// <returns></returns>
        ///
        public static nuint FindCompressBound(nuint streamSize)
        {
            return Interop.FastLzma2.FL2_compressBound(streamSize);
        }

        public static nuint FindCompressBound(byte[] src)
        {
            if (src is null)
                throw new ArgumentNullException(nameof(src));

            return Interop.FastLzma2.FL2_compressBound((nuint)src.Length);
        }

        /// <summary>
        /// Find Decompressed Size of a Compressed Data
        /// </summary>
        /// <param name="data">Compressed Data</param>
        /// <returns>Decompressed Size</returns>
        /// <exception cref="Exception"></exception>
        public static nuint FindDecompressedSize(byte[] data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));
            var ContentSizeError = UIntPtr.Size == 4 ? uint.MaxValue : ulong.MaxValue;
            nuint size = Interop.FastLzma2.FL2_findDecompressedSize(data, (nuint)data.LongLength);
            if (size == ContentSizeError)
            {
                throw new FL2Exception(size);
            }
            return size;
        }

        public static unsafe nuint FindDecompressedSize(string filePath)
        {
            FileInfo file = new FileInfo(filePath);
            if (!file.Exists)
            {
                throw new FileNotFoundException("File not found", filePath);
            }
            using (DirectFileAccessor accessor = new DirectFileAccessor(filePath, FileMode.Open, null, file.Length, MemoryMappedFileAccess.ReadWrite))
            {
                var size = Interop.FastLzma2.FL2_findDecompressedSize(accessor.mmPtr, (nuint)file.Length);
                if (size == (UIntPtr.Size == 4 ? uint.MaxValue : ulong.MaxValue))
                {
                    throw new FL2Exception(size);
                }
                return size;
            }
        }

        public static byte[] Compress(byte[] data, int Level)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            byte[] compressed = new byte[Interop.FastLzma2.FL2_compressBound((nuint)data.Length)];
            nuint code = Interop.FastLzma2.FL2_compress(compressed, (nuint)compressed.Length, data, (nuint)data.Length, Level);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return compressed[..(int)code];
        }

        public static byte[] CompressMT(byte[] data, int Level, uint nbThreads)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));
            byte[] compressed = new byte[Interop.FastLzma2.FL2_compressBound((nuint)data.Length)];
            nuint code = Interop.FastLzma2.FL2_compressMt(compressed, (nuint)compressed.Length, data, (nuint)data.Length, Level, nbThreads);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return compressed[0..(int)code];
        }

        public static byte[] Decompress(byte[] data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));
            nuint decompressedSize = Interop.FastLzma2.FL2_findDecompressedSize(data, (nuint)data.Length);
            if (FL2Exception.IsError(decompressedSize))
            {
                throw new FL2Exception(decompressedSize);
            }
            byte[] decompressed = new byte[decompressedSize];
            nuint code = Interop.FastLzma2.FL2_decompress(decompressed, decompressedSize, data, (nuint)data.Length);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return decompressed[0..(int)code];
        }

        public static byte[] DecompressMT(byte[] data, uint nbThreads)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));
            nuint decompressedSize = Interop.FastLzma2.FL2_findDecompressedSize(data, (nuint)data.Length);
            byte[] decompressed = new byte[decompressedSize];
            nuint code = Interop.FastLzma2.FL2_decompressMt(decompressed, decompressedSize, data, (nuint)data.Length, nbThreads);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return decompressed[0..(int)code];
        }

        public static nuint EstimateCompressMemoryUsage(int compressionLevel, uint nbThreads)
            => Interop.FastLzma2.FL2_estimateCCtxSize(compressionLevel, nbThreads);

        public static nuint EstimateCompressMemoryUsage(FL2CompressionParameters parameters, uint nbThreads)
            => Interop.FastLzma2.FL2_estimateCCtxSize_byParams(ref parameters, nbThreads);

        public static nuint EstimateCompressMemoryUsage(nint context)
            => Interop.FastLzma2.FL2_estimateCCtxSize_usingCCtx(context);

        public static nuint EstimateDecompressMemoryUsage(uint nbThreads)
            => Interop.FastLzma2.FL2_estimateDCtxSize(nbThreads);

        public static nuint GetDictSizeFromProp(byte prop)
            => Interop.FastLzma2.FL2_getDictSizeFromProp(prop);
    }
}
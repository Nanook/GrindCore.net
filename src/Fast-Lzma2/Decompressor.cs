using FxResources.Nanook.GrindCore;
using System;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// Fast LZMA2 Decompress Context
    /// </summary>
    internal partial class Decompressor : IDisposable
    {
        private readonly nint _context;
        public nint ContextPtr => _context;
        private bool disposedValue;

        /// <summary>
        /// Thread use of the context
        /// </summary>
        public uint ThreadCount => Interop.FastLzma2.FL2_getDCtxThreadCount(_context);

        /// <summary>
        /// Initialize new decompress context
        /// </summary>
        /// <param name="nbThreads"></param>
        public Decompressor(uint nbThreads = 0)
        {
            if (nbThreads == 1)
            {
                _context = Interop.FastLzma2.FL2_createDCtx();
            }
            else
            {
                _context = Interop.FastLzma2.FL2_createDCtxMt(nbThreads);
            }
        }

        /// <summary>
        /// Initial new context with specific dict size property
        /// </summary>
        /// <param name="prop">dictSizeProperty</param>
        public void Init(byte prop)
        {
            Interop.FastLzma2.FL2_initDCtx(_context, prop);
        }

        /// <summary>
        /// Decompress
        /// </summary>
        /// <param name="data">Fast LZMA2 data</param>
        /// <returns>Raw data</returns>
        /// <exception cref="FL2Exception"></exception>
        public byte[] Decompress(byte[] data)
        {
            if (data is null)
                throw new ArgumentNullException(nameof(data));

            nuint decompressedSize = FL2.FindDecompressedSize(data);
            byte[] decompressed = new byte[decompressedSize];
            nuint code = Interop.FastLzma2.FL2_decompressDCtx(_context, decompressed, decompressedSize, data, (nuint)data.Length);
            if (FL2Exception.IsError(code))
            {
                throw new FL2Exception(code);
            }
            return decompressed[0..(int)code];
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing) { }
                Interop.FastLzma2.FL2_freeDCtx(_context);
                disposedValue = true;
            }
        }

        ~Decompressor()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
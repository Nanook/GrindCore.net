using System;
using System.IO;
using System.Threading;

namespace Nanook.GrindCore.Lzma
{
    /// <summary>
    /// A non compression stream that does a stream copy. To allow a Stream copy to work seamlessly with this library and CompressionStream base.
    /// </summary>
    public class CopyStream : CompressionStream, ICompressionDefaults
    {
        internal override CompressionAlgorithm Algorithm => CompressionAlgorithm.Copy;
        internal override int BufferSizeInput => 1 * 0x400 * 0x400;
        internal override int BufferSizeOutput { get; }
        CompressionType ICompressionDefaults.LevelFastest => CompressionType.Level0;
        CompressionType ICompressionDefaults.LevelOptimal => CompressionType.Level0;
        CompressionType ICompressionDefaults.LevelSmallestSize => CompressionType.Level0;

        /// <summary>
        /// Initializes a new instance of CopyStream.
        /// </summary>
        public CopyStream(Stream stream, CompressionOptions options) : base(true, stream, options)
        {
            BufferSizeOutput = CacheThreshold;
        }

        /// <summary>
        /// Reads data from the stream and decompresses it using LZMA. Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override int OnRead(CompressionBuffer data, CancellableTask cancel, int limit, out int bytesReadFromStream)
        {
            if (!this.CanRead)
                throw new NotSupportedException("Not for Compression mode");

            cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

            bytesReadFromStream = BaseStream.Read(data.Data, data.Size, data.AvailableWrite);
            data.Write(bytesReadFromStream);
            return bytesReadFromStream;
        }


        /// <summary>
        /// Compresses data using LZMA and writes it to the stream. Position is updated with running total of bytes processed from source stream.
        /// </summary>
        internal override void OnWrite(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream)
        {
            if (!this.CanWrite)
                throw new NotSupportedException("Not for Decompression mode");

            bytesWrittenToStream = data.AvailableRead;
            cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken

            BaseStream.Write(data.Data, data.Pos, data.AvailableRead);
            data.Read(data.AvailableRead);

        }


        /// <summary>
        /// Flushes any remaining compressed data to the stream.
        /// </summary>
        internal override void OnFlush(CompressionBuffer data, CancellableTask cancel, out int bytesWrittenToStream, bool flush, bool complete)
        {
            bytesWrittenToStream = 0;
            if (IsCompress)
            {
                if (data.AvailableRead != 0)
                {
                    cancel.ThrowIfCancellationRequested(); //will exception if cancelled on frameworks that support the CancellationToken
                    BaseStream.Write(data.Data, data.Pos, data.AvailableRead);
                    data.Read(data.AvailableRead);
                }
            }
        }

        /// <summary>
        /// Disposes the LzmaStream and its resources.
        /// </summary>
        protected override void OnDispose()
        {
        }

    }
}
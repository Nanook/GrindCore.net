using Nanook.GrindCore;
using Nanook.GrindCore.Copy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Wraps a source stream for use with GrindCore compression classes, providing enhanced buffering and position management.
    /// This stream enables efficient resetting of the stream position when overreading occurs by using an internal buffer,
    /// reducing the performance cost of frequent small reads. It supports non-disposing behavior, stream limits, and accurate
    /// position tracking. While not required by GrindCore compression streams, using this class ensures that stream positions
    /// remain accurate, allowing other code to reliably resume operations from the correct position.
    /// </summary>
    internal class CompressionReaderStream : CopyStream
    {
        /// <summary>
        /// Creates a new <see cref="CompressionReaderStream"/> that wraps the specified stream with internal buffering and position management,
        /// enabling efficient reading and accurate position tracking for GrindCore compression operations.
        /// </summary>
        /// <param name="stream">The source <see cref="Stream"/> to read from.</param>
        /// <param name="leaveOpen">If <c>true</c>, the underlying stream remains open after this stream is disposed; otherwise, it is closed.</param>
        /// <param name="bufferSize">The size of the internal buffer, in bytes, used to optimize read operations and allow position resets.</param>
        /// <param name="limit">The maximum number of bytes to read from the stream, or <c>null</c> for no limit. Used to enforce a read boundary.</param>
        public CompressionReaderStream(Stream stream, bool leaveOpen = false, int bufferSize = 0x100000, long? limit = null)
            : base(stream, new CompressionOptions() { PositionLimit = limit, LeaveOpen = leaveOpen, BufferSize = bufferSize })
        {
        }
    }
}

using System;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Handles data being processed that does not comply with alignment and padding requirements.
    /// For example, some hash algorithms (e.g., MD2) require data to be in fixed-size blocks.
    /// This class ensures that requirement is fulfilled by buffering and processing data in block-sized chunks.
    /// </summary>
    internal class HashBuffer
    {
        private byte[] _buffer;
        private int _used;

        /// <summary>
        /// Initializes a new instance of the <see cref="HashBuffer"/> class with the specified block size.
        /// </summary>
        /// <param name="bytes">The block size in bytes.</param>
        public HashBuffer(int bytes)
        {
            _used = 0;
            _buffer = new byte[bytes];
        }

        /// <summary>
        /// Gets a value indicating whether there is any buffered data.
        /// </summary>
        public bool HasData => _used != 0;

        /// <summary>
        /// Processes the input data in block-sized chunks, buffering any remainder.
        /// </summary>
        /// <param name="data">The input data array.</param>
        /// <param name="offset">The offset in the input array at which to begin processing.</param>
        /// <param name="length">The number of bytes to process.</param>
        /// <param name="process">The action to call for each full block of data.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> or <paramref name="process"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="offset"/> or <paramref name="length"/> is negative, or if the range is invalid.</exception>
        public void Process(byte[] data, int offset, int length, Action<byte[], int, int> process)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (process == null)
                throw new ArgumentNullException(nameof(process));
            if (offset < 0 || length < 0 || offset + length > data.Length)
                throw new ArgumentOutOfRangeException();

            if (_used > 0)
            {
                // Add bytes from data to fill _buffer
                int toCopy = Math.Min(length, _buffer.Length - _used);
                Array.Copy(data, offset, _buffer, _used, toCopy);
                _used += toCopy;
                offset += toCopy;
                length -= toCopy;

                // Call process if we have a full buffer
                if (_used == _buffer.Length)
                {
                    process(_buffer, 0, _used);
                    _used = 0;
                }
            }

            if (length != 0)
            {
                // Add the remainder bytes to the buffer
                int remainder = length % _buffer.Length;
                if (remainder != 0)
                {
                    Array.Copy(data, offset + (length - remainder), _buffer, _used, remainder);
                    _used += remainder;
                    length -= remainder;
                }
            }

            // Process the data
            if (length != 0)
                process(data, offset, length);
        }

        /// <summary>
        /// Completes the processing by handling any remaining buffered data.
        /// </summary>
        /// <param name="process">The action to call for the final block of data.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="process"/> is null.</exception>
        public void Complete(Action<byte[], int, int> process)
        {
            if (process == null)
                throw new ArgumentNullException(nameof(process));

            if (_used > 0)
            {
                // Calculate the number of padding bytes needed
                int paddingLength = _buffer.Length - _used;
                byte paddingValue = (byte)paddingLength;

                // Process the full buffer
                process(_buffer, 0, _used);

                // Reset the buffer
                _used = 0;
            }
        }
    }
}


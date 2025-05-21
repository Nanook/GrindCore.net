using System;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Handles data being processed that does not comply with alignment and padding requirements.
    /// E.g. MD2 requires data to be in 16 byte blocks. This class ensures this requirement is fulfilled
    /// </summary>
    internal class HashBuffer
    {
        private byte[] _buffer;
        private int _used;

        public HashBuffer(int bytes)
        {
            _used = 0;
            _buffer = new byte[bytes];
        }

        public bool HasData => _used != 0;

        public void Process(byte[] data, int offset, int length, Action<byte[], int, int> process)
        {
            if (_used > 0)
            {
                // Add bytes from data to fill _inData
                int toCopy = Math.Min(length, _buffer.Length - _used);
                Array.Copy(data, offset, _buffer, _used, toCopy);
                _used += toCopy;
                offset += toCopy;
                length -= toCopy;

                // Call process if we have a full _outBuffer
                if (_used == _buffer.Length)
                {
                    process(_buffer, 0, _used);
                    _used = 0;
                }
            }

            if (length != 0)
            {
                // Add the remainder bytes to the _outBuffer
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

        public void Complete(Action<byte[], int, int> process)
        {
            if (_used > 0)
            {
                // Calculate the number of padding bytes needed
                int paddingLength = _buffer.Length - _used;
                byte paddingValue = (byte)paddingLength;

                // Process the full _outBuffer
                process(_buffer, 0, _used);

                // Reset the _outBuffer
                _used = 0;
            }
        }

    }
}

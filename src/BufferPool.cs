using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;


#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
using System.Buffers;
#endif

namespace Nanook.GrindCore
{
    public static class BufferPool
    {
        private static readonly Dictionary<byte[], DateTime> _pool = new Dictionary<byte[], DateTime>();
        private static readonly object _lock = new object();
        private static readonly int _staleThresholdSeconds = 5;
        private static readonly Thread _cleanupThread;

        static BufferPool()
        {
            _cleanupThread = new Thread(CleanupStaleBuffers) { IsBackground = true };
            _cleanupThread.Start();
        }

        public static byte[] Rent(int size)
        {
            lock (_lock)
            {
                byte[]? closestBuffer = null;

                foreach (var entry in _pool.Keys)
                {
                    if (entry.Length == size) //entry.Length >= size && (closestBuffer == null || entry.Length < closestBuffer.Length))
                        closestBuffer = entry;
                }

                if (closestBuffer != null)
                {
                    _pool.Remove(closestBuffer);
                    return closestBuffer;
                }

                return new byte[size];
            }
        }

        public static void Return(byte[] buffer)
        {
            if (buffer == null)
                return;
            lock (_lock)
            {
                _pool[buffer] = DateTime.UtcNow;
            }
        }

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
        public static void RentSpan(int size, out Span<byte> span)
        {
            var buffer = Rent(size);
            span = buffer.AsSpan(0, size); // Create a span of the requested size
        }

        public static void RentReadOnlySpan(int size, out ReadOnlySpan<byte> span)
        {
            var buffer = Rent(size);
            span = buffer.AsSpan(0, size); // Create a read-only span of the requested size
        }
#endif
        private static void CleanupStaleBuffers()
        {
            while (true)
            {
                Thread.Sleep(1000);
                DateTime now = DateTime.UtcNow;

                lock (_lock)
                {
                    var staleBuffers = new List<byte[]>();

                    foreach (var entry in _pool)
                    {
                        if ((now - entry.Value).TotalSeconds > _staleThresholdSeconds)
                            staleBuffers.Add(entry.Key);
                    }

                    foreach (var staleBuffer in staleBuffers)
                        _pool.Remove(staleBuffer);
                }
            }
        }
    }
}
using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;

#if !CLASSIC && (NETSTANDARD2_1_OR_GREATER || NETCOREAPP3_0_OR_GREATER)
using System.Buffers;
#endif

namespace Nanook.GrindCore
{
    /// <summary>
    /// Provides a simple thread-safe pool for renting and returning byte arrays to reduce memory allocations.
    /// </summary>
    public static class BufferPool
    {
        private static readonly Dictionary<byte[], DateTime> _pool = new Dictionary<byte[], DateTime>();
        private static readonly object _lock = new object();
        private static readonly int _staleThresholdSeconds = 5;
        private static readonly Thread _cleanupThread;

        /// <summary>
        /// Initializes the <see cref="BufferPool"/> class and starts the background cleanup thread.
        /// </summary>
        static BufferPool()
        {
            _cleanupThread = new Thread(CleanupStaleBuffers) { IsBackground = true };
            _cleanupThread.Start();
        }

        /// <summary>
        /// Rents a byte array of the specified size from the pool, or allocates a new one if none are available.
        /// </summary>
        /// <param name="size">The minimum size of the buffer to rent.</param>
        /// <returns>A byte array of the requested size.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="size"/> is not positive.</exception>
        public static byte[] Rent(long size)
        {
            if (size < 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Buffer size must be positive.");

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

        /// <summary>
        /// Returns a byte array to the pool for reuse.
        /// </summary>
        /// <param name="buffer">The buffer to return. If null, the call is ignored.</param>
        public static void Return(byte[] buffer)
        {
            if (buffer == null)
                return;
            lock (_lock)
            {
                _pool[buffer] = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Periodically removes stale buffers from the pool that have not been used for a threshold period.
        /// </summary>
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


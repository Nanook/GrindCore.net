using System;
using System.Security.Cryptography;

namespace Nanook.GrindCore
{
    /// <summary>
    /// Base class for all GrindCore hash algorithms, providing a generic method to retrieve the finalized hash as a native type.
    /// </summary>
    public abstract class HashAlgorithmGC : HashAlgorithm
    {
        /// <summary>
        /// Returns the finalized hash value as the specified type, allowing direct access to the hash as a native type
        /// (such as <see cref="uint"/> or <see cref="ulong"/>) for algorithms that produce numeric hashes, or as a <see cref="byte"/> for general use.
        /// This is intended to avoid unnecessary byte conversions for hash algorithms that natively return numeric values.
        /// </summary>
        /// <typeparam name="T">The type to return the hash as. Only <see cref="byte"/> is supported by default.</typeparam>
        /// <returns>The hash value as the specified type.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the hash has not been finalized (i.e., <see cref="HashAlgorithm.HashFinal"/> has not been called).
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// Thrown if <typeparamref name="T"/> is not <see cref="byte"/> and not supported by the derived class.
        /// </exception>
        public virtual T HashAsType<T>()
        {
            if (State != 0 || HashValue == null)
                throw new InvalidOperationException("Hash not finalized.");

            if (typeof(T) == typeof(byte[]))
                return (T)(object)HashValue.Clone();

            throw new NotSupportedException($"Type {typeof(T)} is not supported. Use byte[].");
        }
    }
}
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Nanook.GrindCore
{
#if CLASSIC || NET45_OR_GREATER || NETSTANDARD || NETCOREAPP

    /// <summary>
    /// Represents a cancellable task using a <see cref="CancellationToken"/> for supported frameworks.
    /// </summary>
    internal readonly struct CancellableTask
    {
        /// <summary>
        /// Gets the associated <see cref="CancellationToken"/>.
        /// </summary>
        public readonly CancellationToken Token;

        private readonly bool _tokenSet;

        /// <summary>
        /// Initializes a new instance of the <see cref="CancellableTask"/> struct with a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to associate with this task.</param>
        public CancellableTask(CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            _tokenSet = true;
        }

        /// <summary>
        /// Gets a value indicating whether cancellation has been requested for this task.
        /// </summary>
        public bool IsCancellationRequested => _tokenSet && Token.IsCancellationRequested;

        /// <summary>
        /// Throws an <see cref="OperationCanceledException"/> if cancellation has been requested.
        /// </summary>
        /// <exception cref="OperationCanceledException">Thrown if cancellation has been requested.</exception>
        public void ThrowIfCancellationRequested()
        {
            if (_tokenSet)
                Token.ThrowIfCancellationRequested();
        }
    }

#else

    /// <summary>
    /// Represents a cancellable task for frameworks that do not support CancellationToken/>.
    /// </summary>
    internal readonly struct CancellableTask
    {
        /// <summary>
        /// Gets a value indicating whether cancellation has been requested for this task. Always <c>false</c> in unsupported frameworks.
        /// </summary>
        public bool IsCancellationRequested => false;

        /// <summary>
        /// Does nothing, as cancellation is not supported in this framework.
        /// </summary>
        public void ThrowIfCancellationRequested()
        {
            // Do nothing as CancellationToken is not supported
        }
    }

#endif
}


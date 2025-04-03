using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;


namespace Nanook.GrindCore
{
#if CLASSIC || NET45_OR_GREATER || NETSTANDARD || NETCOREAPP

    internal readonly struct CancellableTask
    {
        public readonly CancellationToken Token;
        private readonly bool _tokenSet = false;

        // Constructor that accepts a CancellationToken
        public CancellableTask(CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            _tokenSet = true;
        }

        // Check if a cancellation has been requested
        public bool IsCancellationRequested => _tokenSet && Token.IsCancellationRequested;

        public void ThrowIfCancellationRequested()
        {
            if (_tokenSet)
                Token.ThrowIfCancellationRequested();
        }
    }

#else

internal readonly struct CancellableTask
{
    // Fallback for frameworks that do not support CancellationToken
    public bool IsCancellationRequested => false;

    public void ThrowIfCancellationRequested()
    {
        // Do nothing as CancellationToken is not supported
    }
}

#endif
}

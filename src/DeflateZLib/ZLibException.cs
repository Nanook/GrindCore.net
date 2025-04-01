


using System.ComponentModel;
using System.Runtime.Serialization;
using System.IO;
using System;

using ZErrorCode = Nanook.GrindCore.Interop.ZLib.ErrorCode;
using ZFlushCode = Nanook.GrindCore.Interop.ZLib.FlushCode;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// This is the exception that is thrown when a ZLib returns an error code indicating an unrecoverable error.
    /// </summary>
    [Serializable]
    //[System.Runtime.CompilerServices.TypeForwardedFrom("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public class ZLibException : IOException, ISerializable
    {
        private readonly string? _zlibErrorContext = string.Empty;
        private readonly string? _zlibErrorMessage = string.Empty;
        private readonly ZErrorCode _zlibErrorCode = ZErrorCode.Ok;

        /// <summary>
        /// This is the preferred constructor to use.
        /// The other constructors are provided for compliance to Fx design guidelines.
        /// </summary>
        /// <param name="message">A (localised) human readable error description.</param>
        /// <param name="zlibErrorContext">A description of the context within zlib where the error occurred (e.g. the function name).</param>
        /// <param name="zlibErrorCode">The error code returned by a ZLib function that caused this exception.</param>
        /// <param name="zlibErrorMessage">The string provided by ZLib as error information (unlocalised).</param>
        public ZLibException(string? message, string? zlibErrorContext, int zlibErrorCode, string? zlibErrorMessage) : base(message)
        {
            _zlibErrorContext = zlibErrorContext;
            _zlibErrorCode = (ZErrorCode)zlibErrorCode;
            _zlibErrorMessage = zlibErrorMessage;
        }

        /// <summary>
        /// This constructor is provided in compliance with common .NET Framework design patterns;
        /// developers should prefer using the constructor
        /// <code>public ZLibException(string message, string zlibErrorContext, ZLibNative.ErrorCode zlibErrorCode, string zlibErrorMessage)</code>.
        /// </summary>
        public ZLibException() { }

        /// <summary>
        /// This constructor is provided in compliance with common .NET Framework design patterns;
        /// developers should prefer using the constructor
        /// <code>public ZLibException(string message, string zlibErrorContext, ZLibNative.ErrorCode zlibErrorCode, string zlibErrorMessage)</code>.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a <code>null</code>.</param>
        public ZLibException(string? message, Exception? innerException) : base(message, innerException) { }

    }
}

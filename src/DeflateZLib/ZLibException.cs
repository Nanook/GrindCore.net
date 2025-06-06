using System.ComponentModel;
using System.Runtime.Serialization;
using System.IO;
using System;

using ZErrorCode = Nanook.GrindCore.Interop.ZLib.ErrorCode;

namespace Nanook.GrindCore.DeflateZLib
{
    /// <summary>
    /// Represents errors that occur when a ZLib operation returns an error code indicating an unrecoverable error.
    /// </summary>
    [Serializable]
    //[System.Runtime.CompilerServices.TypeForwardedFrom("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")]
    public class ZLibException : IOException, ISerializable
    {
        /// <summary>
        /// Gets the context within zlib where the error occurred (e.g. the function name).
        /// </summary>
        private readonly string? _zlibErrorContext = string.Empty;

        /// <summary>
        /// Gets the string provided by ZLib as error information (unlocalized).
        /// </summary>
        private readonly string? _zlibErrorMessage = string.Empty;

        /// <summary>
        /// Gets the error code returned by a ZLib function that caused this exception.
        /// </summary>
        private readonly ZErrorCode _zlibErrorCode = ZErrorCode.Ok;

        /// <summary>
        /// Initializes a new instance of the <see cref="ZLibException"/> class with a specified error message, context, error code, and ZLib error message.
        /// </summary>
        /// <param name="message">A (localized) human readable error description.</param>
        /// <param name="zlibErrorContext">A description of the context within zlib where the error occurred (e.g. the function name).</param>
        /// <param name="zlibErrorCode">The error code returned by a ZLib function that caused this exception.</param>
        /// <param name="zlibErrorMessage">The string provided by ZLib as error information (unlocalized).</param>
        public ZLibException(string? message, string? zlibErrorContext, int zlibErrorCode, string? zlibErrorMessage) : base(message)
        {
            _zlibErrorContext = zlibErrorContext;
            _zlibErrorCode = (ZErrorCode)zlibErrorCode;
            _zlibErrorMessage = zlibErrorMessage;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZLibException"/> class.
        /// This constructor is provided for compliance with .NET Framework design patterns.
        /// </summary>
        public ZLibException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ZLibException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// This constructor is provided for compliance with .NET Framework design patterns.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or <c>null</c>.</param>
        public ZLibException(string? message, Exception? innerException) : base(message, innerException) { }
    }
}

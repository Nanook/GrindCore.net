using System;
using System.Linq;

namespace Nanook.GrindCore.FastLzma2
{
    /// <summary>
    /// Represents error codes returned by the Fast-LZMA2 library.
    /// </summary>
    public enum FL2ErrorCode
    {
        /// <summary>No error detected.</summary>
        NoError = 0,
        /// <summary>Generic error.</summary>
        Generic = 1,
        /// <summary>Internal error (bug).</summary>
        InternalError = 2,
        /// <summary>Corrupted block detected.</summary>
        CorruptionDetected = 3,
        /// <summary>Restored data doesn't match checksum.</summary>
        ChecksumWrong = 4,
        /// <summary>Unsupported parameter.</summary>
        ParameterUnsupported = 5,
        /// <summary>Parameter is out of bound.</summary>
        ParameterOutOfBound = 6,
        /// <summary>Parameters lc+lp &gt; 4.</summary>
        LclpMaxExceeded = 7,
        /// <summary>Not possible at this stage of encoding.</summary>
        StageWrong = 8,
        /// <summary>Context should be initialized first.</summary>
        InitMissing = 9,
        /// <summary>Allocation error: not enough memory.</summary>
        MemoryAllocation = 10,
        /// <summary>Destination buffer is too small.</summary>
        DstSizeTooSmall = 11,
        /// <summary>Source size is incorrect.</summary>
        SrcSizeWrong = 12,
        /// <summary>Processing was canceled by a call to FL2_cancelCStream() or FL2_cancelDStream().</summary>
        Canceled = 13,
        /// <summary>Streaming progress halted due to buffer(s) full/empty.</summary>
        Buffer = 14,
        /// <summary>Wait timed out. Timeouts should be handled before errors using FL2_isTimedOut().</summary>
        TimedOut = 15,
        /// <summary>
        /// Maximum error code value. Do not use directly; may change in future versions.
        /// Use FL2_isError() instead.
        /// </summary>
        MaxCode = 20
    }

    /// <summary>
    /// Represents errors that occur during Fast-LZMA2 compression or decompression.
    /// </summary>
    public class FL2Exception : Exception
    {
        private readonly FL2ErrorCode _errorCode;

        /// <summary>
        /// Gets the Fast-LZMA2 error code associated with this exception.
        /// </summary>
        public FL2ErrorCode ErrorCode => _errorCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="FL2Exception"/> class from a native error code.
        /// </summary>
        /// <param name="code">The native error code.</param>
        internal FL2Exception(nuint code) : this((int)code) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="FL2Exception"/> class from an integer error code.
        /// </summary>
        /// <param name="code">The error code.</param>
        public FL2Exception(int code) : base(GetErrorString(GetErrorCode(code)))
        {
            _errorCode = GetErrorCode(code);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FL2Exception"/> class from a <see cref="FL2ErrorCode"/>.
        /// </summary>
        /// <param name="code">The error code.</param>
        public FL2Exception(FL2ErrorCode code) : base(GetErrorString(code))
        {
            _errorCode = code;
        }

        /// <summary>
        /// Gets the <see cref="FL2ErrorCode"/> for a given native code.
        /// </summary>
        /// <param name="code">The native error code.</param>
        /// <returns>The corresponding <see cref="FL2ErrorCode"/>.</returns>
        internal static FL2ErrorCode GetErrorCode(nuint code) => FL2Exception.GetErrorCode((int)code);

        /// <summary>
        /// Gets the <see cref="FL2ErrorCode"/> for a given integer code.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <returns>The corresponding <see cref="FL2ErrorCode"/>.</returns>
        public static FL2ErrorCode GetErrorCode(int code)
        {
            if (!IsError(code))
                return FL2ErrorCode.NoError;
            return (FL2ErrorCode)(0 - code);
        }

        /// <summary>
        /// Determines whether the specified native code represents an error.
        /// </summary>
        /// <param name="code">The native error code.</param>
        /// <returns><c>true</c> if the code represents an error; otherwise, <c>false</c>.</returns>
        internal static bool IsError(nuint code) => code > 0 - Enum.GetValues(typeof(FL2ErrorCode)).Cast<uint>().Max();

        /// <summary>
        /// Determines whether the specified integer code represents an error.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <returns><c>true</c> if the code represents an error; otherwise, <c>false</c>.</returns>
        public static bool IsError(int code) => FL2Exception.IsError((nuint)code);

        /// <summary>
        /// Gets a human-readable error string for the specified <see cref="FL2ErrorCode"/>.
        /// </summary>
        /// <param name="code">The error code.</param>
        /// <returns>A string describing the error.</returns>
        public static string GetErrorString(FL2ErrorCode code) => code switch
        {
            FL2ErrorCode.NoError => "No error detected",
            FL2ErrorCode.Generic => "Error (generic)",
            FL2ErrorCode.InternalError => "Internal error (bug)",
            FL2ErrorCode.CorruptionDetected => "Corrupted block detected",
            FL2ErrorCode.ChecksumWrong => "Restored data doesn't match checksum",
            FL2ErrorCode.ParameterUnsupported => "Unsupported parameter",
            FL2ErrorCode.ParameterOutOfBound => "Parameter is out of bound",
            FL2ErrorCode.LclpMaxExceeded => "Parameters lc+lp > 4",
            FL2ErrorCode.StageWrong => "Not possible at this stage of encoding",
            FL2ErrorCode.InitMissing => "Context should be init first",
            FL2ErrorCode.MemoryAllocation => "Allocation error => not enough memory",
            FL2ErrorCode.DstSizeTooSmall => "Destination buffer is too small",
            FL2ErrorCode.SrcSizeWrong => "Src size is incorrect",
            FL2ErrorCode.Canceled => "Processing was canceled by a call to FL2_cancelCStream() or FL2_cancelDStream()",
            FL2ErrorCode.Buffer => "Streaming progress halted due to buffer(s) full/empty",
            FL2ErrorCode.TimedOut => "Wait timed out. Timeouts should be handled before errors using FL2_isTimedOut()",
            /* following error codes are not stable and may be removed or changed in a future version */
            FL2ErrorCode.MaxCode => "",
            _ => "Unspecified error code",
        };
    }
}

//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading;
//using System.Threading.Tasks;

using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Nanook.GrindCore
{

    public static class Extensions
    {
        /// <summary>
        /// Copies the contents of the source stream to the destination stream.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The destination stream.</param>
        /// <param name="bufferSize">The _outBuffer size for copying.</param>
        public static void CopyTo(this Stream source, Stream destination, int bufferSize = 81920)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (!source.CanRead) throw new NotSupportedException("Source stream must be readable.");
            if (!destination.CanWrite) throw new NotSupportedException("Destination stream must be writable.");

            byte[] buffer = BufferPool.Rent(bufferSize);
            try
            {
                int bytesRead;
                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                    destination.Write(buffer, 0, bytesRead);
            }
            finally
            {
                BufferPool.Return(buffer);
            }
        }
    }

    public static class StringExtensions
    {
        /// <summary>
        /// Mimics the behavior of string.Join for two or more objects.
        /// </summary>
        public static string Join(this string separator, params object[] values)
        {
            if (separator == null) throw new ArgumentNullException(nameof(separator));
            if (values == null) throw new ArgumentNullException(nameof(values));

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null)
                    sb.Append(values[i]);

                if (i < values.Length - 1)
                    sb.Append(separator);
            }
            return sb.ToString();
        }
    }

//namespace Nanook.GrindCore
//{
//#if !NET6_0_OR_GREATER

//    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
//    public sealed class MemberNotNullAttribute : Attribute
//    {
//        public MemberNotNullAttribute() : this(new string[0]) { }

//        public MemberNotNullAttribute(params string[] memberNames)
//        {
//            MemberNames = memberNames;
//        }

//        public string[] MemberNames { get; }
//    }

//#endif

//#if !NET8_0_OR_GREATER
//    public static class SpanExtensions
//    {
//        public static bool ContainsAnyExceptInRange(this ReadOnlySpan<char> span, char start, char end)
//        {
//            foreach (var ch in span)
//            {
//                if (ch < start || ch > end)
//                    return true;
//            }
//            return false;
//        }
//    }
//#endif

//#if !NET8_0_OR_GREATER

//    public static class StreamExtensions
//    {
//        public static int ReadAtLeast(this Stream stream, Span<byte> _outBuffer, int minimumBytes, bool throwOnEndOfStream = false)
//        {
//            if (minimumBytes < 0)
//                throw new ArgumentOutOfRangeException(nameof(minimumBytes), "Minimum bytes to read cannot be negative.");

//            if (_outBuffer.Length < minimumBytes)
//                throw new ArgumentException("Buffer length must be at least as long as minimumBytes.", nameof(_outBuffer));

//            int totalBytesRead = 0;
//            while (totalBytesRead < minimumBytes)
//            {
//                int bytesRead = stream.Read(_outBuffer.Slice(totalBytesRead));
//                if (bytesRead == 0)
//                {
//                    if (throwOnEndOfStream)
//                        throw new EndOfStreamException("End of stream reached before reading the minimum required bytes.");
//                    break;
//                }
//                totalBytesRead += bytesRead;
//            }
//            return totalBytesRead;
//        }
//    }
//#endif

//#if !NET8_0_OR_GREATER

//    public class TaskToAsyncResult : IAsyncResult
//    {
//        private readonly Task _task;
//        private readonly object _state;

//        public TaskToAsyncResult(Task task, object state)
//        {
//            _task = task ?? throw new ArgumentNullException(nameof(task));
//            _state = state;
//        }

//        public object AsyncState => _state;
//        public WaitHandle AsyncWaitHandle => ((IAsyncResult)_task).AsyncWaitHandle;
//        public bool CompletedSynchronously => _task.IsCompleted && _task.Status != TaskStatus.WaitingForActivation;
//        public bool IsCompleted => _task.IsCompleted;
//        public Task Task => _task;

//        public static IAsyncResult Begin(Task task, AsyncCallback callback, object state)
//        {
//            var asyncResult = new TaskToAsyncResult(task, state);
//            if (callback != null)
//            {
//                task.ContinueWith(t => callback(asyncResult), TaskScheduler.Default);
//            }
//            return asyncResult;
//        }

//        public static TResult End<TResult>(IAsyncResult asyncResult)
//        {
//            var taskToAsyncResult = (TaskToAsyncResult)asyncResult;
//            return ((Task<TResult>)taskToAsyncResult.Task).Result;
//        }
//    }
//#endif

//#if !NET8_0_OR_GREATER

//    public static class ValueTaskExtensions
//    {
//        public static ValueTask<T> FromCanceled<T>(CancellationToken cancellationToken)
//        {
//            var tcs = new TaskCompletionSource<T>();
//            tcs.SetCanceled();
//            return new ValueTask<T>(tcs.Task);
//        }

//        public static ValueTask FromCanceled(CancellationToken cancellationToken)
//        {
//            var tcs = new TaskCompletionSource<bool>();
//            tcs.SetCanceled();
//            return new ValueTask(tcs.Task);
//        }

//        public static ValueTask<T> FromResult<T>(T result)
//        {
//            return new ValueTask<T>(Task.FromResult(result));
//        }
//    }
//#endif
}

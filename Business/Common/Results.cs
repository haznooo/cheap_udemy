using System;
using System.Collections.Generic;
using System.Text;

namespace Business.Common
{
    public class MyResult<T>
    {
        public bool IsSuccess { get; }
        public T Value { get; }
        public IEnumerable<Error> Errors { get; }
        public ErrorType FailureType { get; }

        // Keeps constructor clean
        protected MyResult(T value, bool success, IEnumerable<Error> errors, ErrorType failureType)
        {
            Value = value;
            IsSuccess = success;
            Errors = errors ?? Enumerable.Empty<Error>();
            FailureType = failureType;
        }

        public static MyResult<T> Success(T value) => new(value, true, null, ErrorType.None);

        public static MyResult<T> Failure(ErrorType type, params string[] messages)
        {
            var errors = messages.Select(m => new Error(m, type));
            return new(default!, false, errors, type);
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace Aarkam.Domain.Repositories
{
    /// <summary>
    /// Represents the outcome of an operation that does not return a data.
    /// Used for command-style operations (Create, Update, Delete, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is immutable after construction and provides:
    /// - Success/failure state
    /// - Human-readable message
    /// - Detailed errors (strings, validation failures, custom objects, exceptions, etc.)
    /// - Response code for internal mapping / logging
    /// </para>
    /// <para>
    /// For operations that return data on success, use the generic <see cref="ResultModel{TData}"/>.
    /// </para>
    /// </remarks>
    public class ResultModel
    {
        /// <summary>
        /// Gets a data indicating whether the operation succeeded.
        /// </summary>
        public bool IsSuccess { get; private set; }

        /// <summary>
        /// Gets a human-readable summary message (success confirmation or primary error reason).
        /// </summary>
        public string Message { get; private set; }

        /// <summary>
        /// Gets a read-only list of detailed errors (empty when successful).
        /// </summary>
        public IReadOnlyList<object> Errors { get; private set; }

        /// <summary>
        /// Gets an internal response or error code (e.g. "S000" = success, "E400" = validation failed).
        /// </summary>
        public string ResponseCode { get; private set; }

        /// <summary>
        /// Protected parameterless constructor (required for serialization / derived classes).
        /// </summary>
        protected ResultModel()
        {
            // Serialization / default only – do not use directly
            IsSuccess = false;
            Message = string.Empty;
            Errors = [];
            ResponseCode = "S000";
        }

        /// <summary>
        /// Protected constructor used by factory methods.
        /// </summary>
        protected ResultModel(bool isSuccess, string message, IEnumerable<object> errors = null, string responseCode = "S000")
        {
            IsSuccess = isSuccess;
            ResponseCode = responseCode ?? "S000";
            Message = message ?? string.Empty;

            var errorList = errors?.ToList() ?? new List<object>();
            Errors = errorList.AsReadOnly();

            // Auto-generate fallback message when failed and no message was provided
            if (!isSuccess && string.IsNullOrEmpty(message))
            {
                Message = errorList.Count switch
                {
                    0 => "Operation failed.",
                    1 => errorList[0]?.ToString() ?? "Unknown error",
                    _ => $"{errorList.Count} errors occurred."
                };
            }
        }

        // ── Factory methods ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates a successful result with default message.
        /// </summary>
        public static ResultModel Success()
            => new(true, "Operation completed successfully.", null, "S000");

        /// <summary>
        /// Creates a successful result with a custom message.
        /// </summary>
        public static ResultModel Success(string message)
            => new(true, message, null, "S000");

        /// <summary>
        /// Creates a failed result with a single error.
        /// </summary>
        public static ResultModel Failure(object error, string responseCode = "E500")
            => new(false, null, [error], responseCode);

        /// <summary>
        /// Creates a failed result with multiple errors.
        /// </summary>
        public static ResultModel Failure(IEnumerable<object> errors, string responseCode = "E400")
        {
            var errorList = errors?.ToList() ?? [];
            return new(false, null, errorList, responseCode);
        }
    }

    /// <summary>
    /// Generic result that carries a data of type <typeparamref name="TData"/> on success.
    /// </summary>
    /// <typeparam name="TData">The type of data returned on success.</typeparam>
    public class ResultModel<TData> : ResultModel
    {
        /// <summary>
        /// Gets the result data (only meaningful when <see cref="IsSuccess"/> is true).
        /// </summary>
        public TData Data { get; private set; }

        /// <summary>
        /// Protected parameterless constructor (for serialization).
        /// </summary>
        protected ResultModel() : base() { }

        /// <summary>
        /// Protected constructor used by factory methods.
        /// </summary>
        public ResultModel(TData data, bool isSuccess, string message, IEnumerable<object> errors = null, string responseCode = "S000")
            : base(isSuccess, message, errors, responseCode)
        {
            Data = data;
        }

        // ── Factory methods ─────────────────────────────────────────────────────

        /// <summary>
        /// Creates a successful result containing the provided data.
        /// </summary>
        public static ResultModel<TData> Success(TData data)
            => new(data, true, "Operation completed successfully.", null, "S000");

        /// <summary>
        /// Creates a successful result with data and custom message.
        /// </summary>
        public static ResultModel<TData> Success(TData data, string message)
            => new(data, true, message, null, "S000");

        /// <summary>
        /// Creates a failed result with a single error (data will be default/null).
        /// </summary>
        public new static ResultModel<TData> Failure(object error, string responseCode = "E500")
            => new(default, false, null, [error], responseCode);

        /// <summary>
        /// Creates a failed result with multiple errors (data will be default/null).
        /// </summary>
        public new static ResultModel<TData> Failure(IEnumerable<object> errors, string responseCode = "E400")
        {
            var errorList = errors?.ToList() ?? [];
            return new(default, false, null, errorList, responseCode);
        }

        /// <summary>
        /// Creates success if data is not null/default, otherwise failure.
        /// Very useful in repository/service methods.
        /// </summary>
        public static ResultModel<TData> FromValue(TData data, string failureMessage = "Resource not found")
            => data is not null && !EqualityComparer<TData>.Default.Equals(data, default)
                ? Success(data)
                : Failure(failureMessage, "E404");

        /// <summary>
        /// Creates success if data is not null/default, otherwise failure with custom response code.
        /// </summary>
        public static ResultModel<TData> FromValue(TData data, string failureMessage, string failureResponseCode)
            => data is not null && !EqualityComparer<TData>.Default.Equals(data, default)
                ? Success(data)
                : Failure(failureMessage, failureResponseCode);
    }
}
using Aarkam.DDD.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aarkam.Domain.Repositories
{
    /// <summary>
    /// Represents the standardized outcome of an operation that does not return data.
    /// Used for command-style operations (Create, Update, Delete, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is immutable after construction and provides:
    /// - Success/failure state
    /// - Human-readable message
    /// - Detailed errors (strings, validation failures, custom objects, exceptions, etc.)
    /// - Response/Error code for internal mapping, logging, and client-side error rendering
    /// - Categorical severity rating (<see cref="ExceptionSeverity"/>) matching the domain model
    /// - Structured metadata contextual dictionary (<see cref="Details"/>)
    /// </para>
    /// <para>
    /// Fully integrates with <see cref="DomainException"/>, allowing direct conversion of structured domain faults.
    /// For operations that return data on success, use the generic <see cref="ResultModel{TData}"/>.
    /// </para>
    /// </remarks>
    public class ResultModel
    {
        /// <summary>
        /// Gets a value indicating whether the operation succeeded.
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
        /// Gets an internal response or machine-readable error code (e.g. "S000" = success, "ERR_ENTITY_NOT_FOUND" = not found).
        /// </summary>
        public string ResponseCode { get; private set; }

        /// <summary>
        /// Gets the unique machine-readable error code (synonym for <see cref="ResponseCode"/> for DDD consistency).
        /// </summary>
        public string ErrorCode => ResponseCode;

        /// <summary>
        /// Gets the categorical severity rating of the fault (e.g., Warning, Error, Critical), or null if successful.
        /// </summary>
        public ExceptionSeverity? Severity { get; private set; }

        /// <summary>
        /// Gets optional structured contextual telemetry or diagnostic key-value pairs associated with the error.
        /// </summary>
        public IReadOnlyDictionary<string, object?>? Details { get; private set; }

        /// <summary>
        /// Protected parameterless constructor (required for serialization / derived classes).
        /// </summary>
        protected ResultModel()
        {
            IsSuccess = false;
            Message = string.Empty;
            Errors = [];
            ResponseCode = "S000";
            Severity = null;
            Details = null;
        }

        /// <summary>
        /// Protected constructor used by factory methods.
        /// </summary>
        protected ResultModel(
            bool isSuccess,
            string? message,
            IEnumerable<object>? errors = null,
            string? responseCode = "S000",
            ExceptionSeverity? severity = null,
            IEnumerable<KeyValuePair<string, object?>>? details = null)
        {
            IsSuccess = isSuccess;
            ResponseCode = responseCode ?? (isSuccess ? "S000" : "E500");
            Message = message ?? string.Empty;
            Severity = severity;
            Details = details != null 
                ? new Dictionary<string, object?>(details.ToDictionary(kv => kv.Key, kv => kv.Value))
                : null;

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
        /// Creates a successful result with a custom message and optional response code.
        /// </summary>
        public static ResultModel Success(string message, string responseCode = "S000")
            => new(true, message, null, responseCode);

        /// <summary>
        /// Creates a default failed result.
        /// </summary>
        public static ResultModel Failure()
            => new(false, "Operation failed.", null, "E500");

        /// <summary>
        /// Creates a failed result with a single error object and optional response code and severity.
        /// </summary>
        public static ResultModel Failure(
            object error, 
            string responseCode = "E500", 
            ExceptionSeverity? severity = ExceptionSeverity.Error)
            => new(false, null, [error], responseCode, severity);

        /// <summary>
        /// Creates a failed result with a specific error message, machine-readable error code, severity, and contextual details.
        /// </summary>
        public static ResultModel Failure(
            string message,
            string responseCode = "E500",
            ExceptionSeverity? severity = ExceptionSeverity.Error,
            IEnumerable<KeyValuePair<string, object?>>? details = null)
            => new(false, message, null, responseCode, severity, details);

        /// <summary>
        /// Creates a failed result with multiple error objects.
        /// </summary>
        public static ResultModel Failure(
            IEnumerable<object> errors, 
            string responseCode = "E400", 
            ExceptionSeverity? severity = ExceptionSeverity.Error)
        {
            var errorList = errors?.ToList() ?? [];
            return new(false, null, errorList, responseCode, severity);
        }

        /// <summary>
        /// Creates a failed <see cref="ResultModel"/> directly from a structured <see cref="DomainException"/>, 
        /// automatically mapping its <see cref="DomainException.ErrorCode"/>, <see cref="DomainException.Severity"/>, 
        /// <see cref="DomainException.Message"/>, and <see cref="DomainException.Details"/>.
        /// </summary>
        /// <param name="domainException">The domain exception instance to convert.</param>
        /// <returns>A structured failure <see cref="ResultModel"/>.</returns>
        public static ResultModel FromException(DomainException domainException)
        {
            if (domainException is null) throw new ArgumentNullException(nameof(domainException));

            return new ResultModel(
                isSuccess: false,
                message: domainException.Message,
                errors: [domainException.Message],
                responseCode: domainException.ErrorCode,
                severity: domainException.Severity,
                details: domainException.Details);
        }

        /// <summary>
        /// Creates a failed <see cref="ResultModel"/> from an arbitrary <see cref="Exception"/>.
        /// If the exception is or wraps a <see cref="DomainException"/>, its structured properties are preserved.
        /// </summary>
        /// <param name="exception">The exception instance to convert.</param>
        /// <returns>A structured failure <see cref="ResultModel"/>.</returns>
        public static ResultModel FromException(Exception exception)
        {
            if (exception is null) throw new ArgumentNullException(nameof(exception));

            if (exception is DomainException domainEx)
            {
                return FromException(domainEx);
            }

            return new ResultModel(
                isSuccess: false,
                message: exception.Message,
                errors: [exception.Message],
                responseCode: "E500",
                severity: ExceptionSeverity.Critical,
                details: null);
        }
    }

    /// <summary>
    /// Generic result that carries data of type <typeparamref name="TData"/> on success.
    /// </summary>
    /// <typeparam name="TData">The type of data returned on success.</typeparam>
    public class ResultModel<TData> : ResultModel
    {
        /// <summary>
        /// Gets the result data (only meaningful when <see cref="ResultModel.IsSuccess"/> is true).
        /// </summary>
        public TData? Data { get; private set; }

        /// <summary>
        /// Protected parameterless constructor (for serialization).
        /// </summary>
        protected ResultModel() : base()
        {
            Data = default;
        }

        /// <summary>
        /// Protected constructor used by factory methods.
        /// </summary>
        public ResultModel(
            TData? data,
            bool isSuccess,
            string? message,
            IEnumerable<object>? errors = null,
            string? responseCode = "S000",
            ExceptionSeverity? severity = null,
            IEnumerable<KeyValuePair<string, object?>>? details = null)
            : base(isSuccess, message, errors, responseCode, severity, details)
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
        public static ResultModel<TData> Success(TData data, string message, string responseCode = "S000")
            => new(data, true, message, null, responseCode);

        /// <summary>
        /// Creates a default failed generic result.
        /// </summary>
        public new static ResultModel<TData> Failure()
            => new(default, false, "Operation failed.", null, "E500");

        /// <summary>
        /// Creates a failed result with a single error (data will be default/null).
        /// </summary>
        public new static ResultModel<TData> Failure(
            object error, 
            string responseCode = "E500", 
            ExceptionSeverity? severity = ExceptionSeverity.Error)
            => new(default, false, null, [error], responseCode, severity);

        /// <summary>
        /// Creates a failed result with a specific error message, machine-readable error code, severity, and contextual details.
        /// </summary>
        public new static ResultModel<TData> Failure(
            string message,
            string responseCode = "E500",
            ExceptionSeverity? severity = ExceptionSeverity.Error,
            IEnumerable<KeyValuePair<string, object?>>? details = null)
            => new(default, false, message, null, responseCode, severity, details);

        /// <summary>
        /// Creates a failed result with multiple errors (data will be default/null).
        /// </summary>
        public new static ResultModel<TData> Failure(
            IEnumerable<object> errors, 
            string responseCode = "E400", 
            ExceptionSeverity? severity = ExceptionSeverity.Error)
        {
            var errorList = errors?.ToList() ?? [];
            return new(default, false, null, errorList, responseCode, severity);
        }

        /// <summary>
        /// Creates a failed <see cref="ResultModel{TData}"/> directly from a structured <see cref="DomainException"/>, 
        /// automatically mapping its <see cref="DomainException.ErrorCode"/>, <see cref="DomainException.Severity"/>, 
        /// and <see cref="DomainException.Details"/>.
        /// </summary>
        /// <param name="domainException">The domain exception instance to convert.</param>
        /// <returns>A structured failure result carrying default data.</returns>
        public new static ResultModel<TData> FromException(DomainException domainException)
        {
            if (domainException is null) throw new ArgumentNullException(nameof(domainException));

            return new ResultModel<TData>(
                data: default,
                isSuccess: false,
                message: domainException.Message,
                errors: [domainException.Message],
                responseCode: domainException.ErrorCode,
                severity: domainException.Severity,
                details: domainException.Details);
        }

        /// <summary>
        /// Creates a failed <see cref="ResultModel{TData}"/> from an arbitrary <see cref="Exception"/>.
        /// If the exception is or wraps a <see cref="DomainException"/>, its structured properties are preserved.
        /// </summary>
        /// <param name="exception">The exception instance to convert.</param>
        /// <returns>A structured failure result carrying default data.</returns>
        public new static ResultModel<TData> FromException(Exception exception)
        {
            if (exception is null) throw new ArgumentNullException(nameof(exception));

            if (exception is DomainException domainEx)
            {
                return FromException(domainEx);
            }

            return new ResultModel<TData>(
                data: default,
                isSuccess: false,
                message: exception.Message,
                errors: [exception.Message],
                responseCode: "E500",
                severity: ExceptionSeverity.Critical,
                details: null);
        }

        /// <summary>
        /// Creates success if data is not null/default, otherwise returns a structured failure.
        /// </summary>
        public static ResultModel<TData> FromValue(TData? data, string failureMessage = "Resource not found")
            => data is not null && !EqualityComparer<TData>.Default.Equals(data, default!)
                ? Success(data)
                : Failure(failureMessage, "E404", ExceptionSeverity.Warning);

        /// <summary>
        /// Creates success if data is not null/default, otherwise returns a structured failure with custom error code and severity.
        /// </summary>
        public static ResultModel<TData> FromValue(
            TData? data, 
            string failureMessage, 
            string failureResponseCode, 
            ExceptionSeverity? severity = ExceptionSeverity.Warning)
            => data is not null && !EqualityComparer<TData>.Default.Equals(data, default!)
                ? Success(data)
                : Failure(failureMessage, failureResponseCode, severity);
    }
}

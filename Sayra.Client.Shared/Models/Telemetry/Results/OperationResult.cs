using System;

namespace Sayra.Client.Shared.Models.Telemetry.Results
{
    /// <summary>
    /// Represents the structured, standardized result of an execution operation.
    /// </summary>
    public record OperationResult
    {
        /// <summary>Gets a value indicating whether the operation was successful.</summary>
        public bool IsSuccess { get; init; }

        /// <summary>Gets the error or success details message associated with the outcome.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets the exact timestamp of the result generation.</summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        /// <summary>Creates a successful operation result.</summary>
        public static OperationResult Success(string message = "") => new() { IsSuccess = true, Message = message };

        /// <summary>Creates a failed operation result.</summary>
        public static OperationResult Failure(string error) => new() { IsSuccess = false, Message = error };
    }

    /// <summary>
    /// Represents the structured, standardized result of an execution operation carrying a typed payload.
    /// </summary>
    /// <typeparam name="T">The type of the payload.</typeparam>
    public record OperationResult<T> : OperationResult
    {
        /// <summary>Gets the payload data returned by the operation.</summary>
        public T? Data { get; init; }

        /// <summary>Creates a successful operation result with data.</summary>
        public static OperationResult<T> Success(T data, string message = "") => new() { IsSuccess = true, Data = data, Message = message };

        /// <summary>Creates a failed operation result.</summary>
        public static new OperationResult<T> Failure(string error) => new() { IsSuccess = false, Message = error };
    }
}

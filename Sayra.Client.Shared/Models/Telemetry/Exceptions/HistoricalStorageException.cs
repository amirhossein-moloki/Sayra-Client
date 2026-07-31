using System;

namespace Sayra.Client.Shared.Models.Telemetry.Exceptions
{
    /// <summary>
    /// Exception thrown when saving, compressing, or retrieving long-term consolidated metrics fails.
    /// </summary>
    public class HistoricalStorageException : ObservabilityException
    {
        /// <summary>Initializes a new instance of the <see cref="HistoricalStorageException"/> class.</summary>
        public HistoricalStorageException() { }

        /// <summary>Initializes a new instance of the <see cref="HistoricalStorageException"/> class with a specified error message.</summary>
        /// <param name="message">The message that describes the error.</param>
        public HistoricalStorageException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="HistoricalStorageException"/> class with a specified error message and inner exception.</summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public HistoricalStorageException(string message, Exception innerException) : base(message, innerException) { }
    }
}

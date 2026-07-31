using System;

namespace Sayra.Client.Shared.Models.Telemetry.Exceptions
{
    /// <summary>
    /// Exception thrown when metrics collection, calculation, or aggregation operations fail.
    /// </summary>
    public class MetricsException : ObservabilityException
    {
        /// <summary>Initializes a new instance of the <see cref="MetricsException"/> class.</summary>
        public MetricsException() { }

        /// <summary>Initializes a new instance of the <see cref="MetricsException"/> class with a specified error message.</summary>
        /// <param name="message">The message that describes the error.</param>
        public MetricsException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="MetricsException"/> class with a specified error message and inner exception.</summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public MetricsException(string message, Exception innerException) : base(message, innerException) { }
    }
}

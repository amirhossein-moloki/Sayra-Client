using System;

namespace Sayra.Client.Shared.Models.Telemetry.Exceptions
{
    /// <summary>
    /// Exception thrown when telemetry recording or buffer operations fail.
    /// </summary>
    public class TelemetryException : ObservabilityException
    {
        /// <summary>Initializes a new instance of the <see cref="TelemetryException"/> class.</summary>
        public TelemetryException() { }

        /// <summary>Initializes a new instance of the <see cref="TelemetryException"/> class with a specified error message.</summary>
        /// <param name="message">The message that describes the error.</param>
        public TelemetryException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="TelemetryException"/> class with a specified error message and inner exception.</summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public TelemetryException(string message, Exception innerException) : base(message, innerException) { }
    }
}

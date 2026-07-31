using System;

namespace Sayra.Client.Shared.Models.Telemetry.Exceptions
{
    /// <summary>
    /// Exception thrown when workstation diagnostic report generation or queries fail.
    /// </summary>
    public class DiagnosticsException : ObservabilityException
    {
        /// <summary>Initializes a new instance of the <see cref="DiagnosticsException"/> class.</summary>
        public DiagnosticsException() { }

        /// <summary>Initializes a new instance of the <see cref="DiagnosticsException"/> class with a specified error message.</summary>
        /// <param name="message">The message that describes the error.</param>
        public DiagnosticsException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="DiagnosticsException"/> class with a specified error message and inner exception.</summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public DiagnosticsException(string message, Exception innerException) : base(message, innerException) { }
    }
}

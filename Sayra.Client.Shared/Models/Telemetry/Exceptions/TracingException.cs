using System;

namespace Sayra.Client.Shared.Models.Telemetry.Exceptions
{
    /// <summary>
    /// Exception thrown when distributed context tracing or span correlation operations fail.
    /// </summary>
    public class TracingException : ObservabilityException
    {
        /// <summary>Initializes a new instance of the <see cref="TracingException"/> class.</summary>
        public TracingException() { }

        /// <summary>Initializes a new instance of the <see cref="TracingException"/> class with a specified error message.</summary>
        /// <param name="message">The message that describes the error.</param>
        public TracingException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="TracingException"/> class with a specified error message and inner exception.</summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public TracingException(string message, Exception innerException) : base(message, innerException) { }
    }
}

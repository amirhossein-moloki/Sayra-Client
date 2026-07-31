using System;

namespace Sayra.Client.Shared.Models.Telemetry.Exceptions
{
    /// <summary>
    /// Base class for all domain exceptions thrown within the Observability, Monitoring, and Telemetry platform.
    /// </summary>
    public class ObservabilityException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ObservabilityException"/> class.
        /// </summary>
        public ObservabilityException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservabilityException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ObservabilityException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ObservabilityException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public ObservabilityException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

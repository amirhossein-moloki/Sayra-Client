using System;

namespace Sayra.Client.Shared.Models.Telemetry.Exceptions
{
    /// <summary>
    /// Exception thrown when workstation heartbeat or background kernel process monitoring fails.
    /// </summary>
    public class MonitoringException : ObservabilityException
    {
        /// <summary>Initializes a new instance of the <see cref="MonitoringException"/> class.</summary>
        public MonitoringException() { }

        /// <summary>Initializes a new instance of the <see cref="MonitoringException"/> class with a specified error message.</summary>
        /// <param name="message">The message that describes the error.</param>
        public MonitoringException(string message) : base(message) { }

        /// <summary>Initializes a new instance of the <see cref="MonitoringException"/> class with a specified error message and inner exception.</summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public MonitoringException(string message, Exception innerException) : base(message, innerException) { }
    }
}

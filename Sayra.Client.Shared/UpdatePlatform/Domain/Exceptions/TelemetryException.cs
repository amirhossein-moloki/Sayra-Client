using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a telemetry operation or validation fails.
    /// </summary>
    public class TelemetryException : UpdateException
    {
        public TelemetryException() { }

        public TelemetryException(string message) : base(message) { }

        public TelemetryException(string message, Exception innerException) : base(message, innerException) { }
    }
}

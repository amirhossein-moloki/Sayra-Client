using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a telemetry buffering or queuing operation fails.
    /// </summary>
    public class ReportingQueueException : UpdateException
    {
        public ReportingQueueException() { }

        public ReportingQueueException(string message) : base(message) { }

        public ReportingQueueException(string message, Exception innerException) : base(message, innerException) { }
    }
}

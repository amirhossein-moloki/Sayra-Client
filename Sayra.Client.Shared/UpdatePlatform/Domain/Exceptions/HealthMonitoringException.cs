using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when health monitoring checks or initialization fails.
    /// </summary>
    public class HealthMonitoringException : UpdateException
    {
        public HealthMonitoringException() { }

        public HealthMonitoringException(string message) : base(message) { }

        public HealthMonitoringException(string message, Exception innerException) : base(message, innerException) { }
    }
}

using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when an update action is attempted outside of the configured maintenance window.
    /// </summary>
    public class MaintenanceWindowViolationException : UpdateException
    {
        public MaintenanceWindowViolationException() { }

        public MaintenanceWindowViolationException(string message) : base(message) { }

        public MaintenanceWindowViolationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

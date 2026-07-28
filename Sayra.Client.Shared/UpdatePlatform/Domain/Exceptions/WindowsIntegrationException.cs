using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when Windows native integration components fail to execute or coordinate correctly.
    /// </summary>
    public class WindowsIntegrationException : UpdateException
    {
        public WindowsIntegrationException() { }

        public WindowsIntegrationException(string message) : base(message) { }

        public WindowsIntegrationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

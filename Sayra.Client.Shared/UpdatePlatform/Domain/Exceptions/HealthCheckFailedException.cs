using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when post-installation or post-rollback sanity checks fail.
    /// </summary>
    public class HealthCheckFailedException : UpdateException
    {
        public HealthCheckFailedException() { }

        public HealthCheckFailedException(string message) : base(message) { }

        public HealthCheckFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}

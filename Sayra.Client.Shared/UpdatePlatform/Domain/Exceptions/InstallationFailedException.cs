using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when the update installation process fails.
    /// </summary>
    public class InstallationFailedException : InstallationException
    {
        public InstallationFailedException(string message) : base(message) { }
        public InstallationFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}

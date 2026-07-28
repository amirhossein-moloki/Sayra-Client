using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when validation of the installation (integrity, manifest, files) fails.
    /// </summary>
    public class InstallationValidationException : UpdateValidationException
    {
        public InstallationValidationException(string message) : base(message) { }
        public InstallationValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

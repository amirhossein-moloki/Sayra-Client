using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when system backup package verification or integrity checking fails.
    /// </summary>
    public class BackupValidationException : UpdateException
    {
        public BackupValidationException() { }

        public BackupValidationException(string message) : base(message) { }

        public BackupValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when the system automatic recovery or self-healing pipeline fails.
    /// </summary>
    public class RecoveryFailedException : UpdateException
    {
        public RecoveryFailedException() { }

        public RecoveryFailedException(string message) : base(message) { }

        public RecoveryFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}

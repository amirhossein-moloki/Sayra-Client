using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a system rollback execution fails or is aborted.
    /// </summary>
    public class RollbackFailedException : RollbackException
    {
        public RollbackFailedException() { }

        public RollbackFailedException(string message) : base(message) { }

        public RollbackFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}

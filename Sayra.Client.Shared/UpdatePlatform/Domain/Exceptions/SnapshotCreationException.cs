using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when snapshot generation fails.
    /// </summary>
    public class SnapshotCreationException : RollbackException
    {
        public SnapshotCreationException() { }

        public SnapshotCreationException(string message) : base(message) { }

        public SnapshotCreationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

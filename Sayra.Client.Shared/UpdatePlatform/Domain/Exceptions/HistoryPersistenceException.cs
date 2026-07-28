using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when update history or rollback logs fail to be written or retrieved from persistent storage.
    /// </summary>
    public class HistoryPersistenceException : StorageException
    {
        public HistoryPersistenceException() { }

        public HistoryPersistenceException(string message) : base(message) { }

        public HistoryPersistenceException(string message, Exception innerException) : base(message, innerException) { }
    }
}

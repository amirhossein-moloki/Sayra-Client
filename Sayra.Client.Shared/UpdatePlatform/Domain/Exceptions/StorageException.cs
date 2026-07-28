using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Base exception for all local storage, database, and repository operations on the update platform.
    /// </summary>
    public class StorageException : UpdateException
    {
        public StorageException() { }

        public StorageException(string message) : base(message) { }

        public StorageException(string message, Exception innerException) : base(message, innerException) { }
    }
}

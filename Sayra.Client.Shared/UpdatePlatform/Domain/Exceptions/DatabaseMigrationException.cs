using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when schema migrations on the SQLCipher update platform database fail.
    /// </summary>
    public class DatabaseMigrationException : StorageException
    {
        public DatabaseMigrationException() { }

        public DatabaseMigrationException(string message) : base(message) { }

        public DatabaseMigrationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

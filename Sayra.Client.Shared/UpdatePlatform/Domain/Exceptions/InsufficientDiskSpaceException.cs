using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when there is insufficient disk space on the target workstation drive.
    /// </summary>
    public class InsufficientDiskSpaceException : StorageException
    {
        public InsufficientDiskSpaceException() { }

        public InsufficientDiskSpaceException(string message) : base(message) { }

        public InsufficientDiskSpaceException(string message, Exception innerException) : base(message, innerException) { }
    }
}

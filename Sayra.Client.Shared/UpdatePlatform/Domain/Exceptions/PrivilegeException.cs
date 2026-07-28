using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when privilege verification fails or required privileges are missing.
    /// </summary>
    public class PrivilegeException : UpdateException
    {
        public PrivilegeException() { }

        public PrivilegeException(string message) : base(message) { }

        public PrivilegeException(string message, Exception innerException) : base(message, innerException) { }
    }
}

using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when security validations for updates or binary payloads fail.
    /// </summary>
    public class SecurityValidationException : UpdateException
    {
        public SecurityValidationException() { }

        public SecurityValidationException(string message) : base(message) { }

        public SecurityValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

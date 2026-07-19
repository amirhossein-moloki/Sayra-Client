using System;

namespace Sayra.Client.Diagnostics.Exceptions
{
    public class ValidationException : HardwareProviderException
    {
        public ValidationException(string message) : base(message) { }
        public ValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

using System;

namespace Sayra.Client.Diagnostics.Exceptions
{
    public class ProviderUnavailableException : HardwareProviderException
    {
        public ProviderUnavailableException(string message) : base(message) { }
        public ProviderUnavailableException(string message, Exception innerException) : base(message, innerException) { }
    }
}

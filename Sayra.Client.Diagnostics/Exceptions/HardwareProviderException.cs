using System;

namespace Sayra.Client.Diagnostics.Exceptions
{
    public class HardwareProviderException : Exception
    {
        public HardwareProviderException(string message) : base(message) { }
        public HardwareProviderException(string message, Exception innerException) : base(message, innerException) { }
    }
}

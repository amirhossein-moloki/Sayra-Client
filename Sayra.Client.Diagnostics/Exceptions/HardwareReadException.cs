using System;

namespace Sayra.Client.Diagnostics.Exceptions
{
    public class HardwareReadException : HardwareProviderException
    {
        public HardwareReadException(string message) : base(message) { }
        public HardwareReadException(string message, Exception innerException) : base(message, innerException) { }
    }
}

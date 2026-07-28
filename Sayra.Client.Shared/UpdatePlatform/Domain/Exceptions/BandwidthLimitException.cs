using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Thrown when bandwidth limit constraints or policy checks fail.
    /// </summary>
    public class BandwidthLimitException : UpdateException
    {
        public BandwidthLimitException() { }
        public BandwidthLimitException(string message) : base(message) { }
        public BandwidthLimitException(string message, Exception innerException) : base(message, innerException) { }
    }
}

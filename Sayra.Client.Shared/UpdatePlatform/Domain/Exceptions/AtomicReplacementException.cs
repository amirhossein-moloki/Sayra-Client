using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when atomic file replacement or directory swap operations fail.
    /// </summary>
    public class AtomicReplacementException : UpdateException
    {
        public AtomicReplacementException(string message) : base(message) { }
        public AtomicReplacementException(string message, Exception innerException) : base(message, innerException) { }
    }
}

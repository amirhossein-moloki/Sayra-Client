using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Thrown when no mirrors or CDN endpoints are available, or they fail to respond.
    /// </summary>
    public class MirrorUnavailableException : UpdateException
    {
        public MirrorUnavailableException() { }
        public MirrorUnavailableException(string message) : base(message) { }
        public MirrorUnavailableException(string message, Exception innerException) : base(message, innerException) { }
    }
}

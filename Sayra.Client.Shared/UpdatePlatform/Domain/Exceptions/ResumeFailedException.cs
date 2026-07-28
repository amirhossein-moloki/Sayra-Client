using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Thrown when resuming an interrupted download fails.
    /// </summary>
    public class ResumeFailedException : UpdateException
    {
        public ResumeFailedException() { }
        public ResumeFailedException(string message) : base(message) { }
        public ResumeFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}

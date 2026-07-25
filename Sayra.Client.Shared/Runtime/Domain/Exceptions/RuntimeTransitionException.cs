using System;

namespace Sayra.Client.Shared.Runtime.Domain.Exceptions
{
    public class RuntimeTransitionException : RuntimeException
    {
        public RuntimeTransitionException() { }
        public RuntimeTransitionException(string message) : base(message) { }
        public RuntimeTransitionException(string message, Exception innerException) : base(message, innerException) { }
    }
}

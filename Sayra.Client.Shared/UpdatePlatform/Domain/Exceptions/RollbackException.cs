using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when restoration, snapshot extraction, or previous state recovery operations fail.
    /// </summary>
    public class RollbackException : UpdateException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RollbackException"/> class.
        /// </summary>
        public RollbackException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RollbackException"/> class with a specified message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public RollbackException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="RollbackException"/> class with a specified message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception reference.</param>
        public RollbackException(string message, Exception innerException) : base(message, innerException) { }
    }
}

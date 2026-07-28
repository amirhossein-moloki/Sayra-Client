using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Base exception class for all update platform failures.
    /// </summary>
    public class UpdateException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateException"/> class.
        /// </summary>
        public UpdateException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateException"/> class with a specified message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public UpdateException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateException"/> class with a specified message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception reference.</param>
        public UpdateException(string message, Exception innerException) : base(message, innerException) { }
    }
}

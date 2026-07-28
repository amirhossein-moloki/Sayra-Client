using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when update components, manifests, or versions fail integrity or specification validation checks.
    /// </summary>
    public class UpdateValidationException : UpdateException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateValidationException"/> class.
        /// </summary>
        public UpdateValidationException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateValidationException"/> class with a specified message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public UpdateValidationException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateValidationException"/> class with a specified message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception reference.</param>
        public UpdateValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

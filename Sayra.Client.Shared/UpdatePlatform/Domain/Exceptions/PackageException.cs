using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when anomalies, corruption, or signature mismatch errors occur within update package archives.
    /// </summary>
    public class PackageException : UpdateException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PackageException"/> class.
        /// </summary>
        public PackageException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageException"/> class with a specified message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public PackageException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PackageException"/> class with a specified message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception reference.</param>
        public PackageException(string message, Exception innerException) : base(message, innerException) { }
    }
}

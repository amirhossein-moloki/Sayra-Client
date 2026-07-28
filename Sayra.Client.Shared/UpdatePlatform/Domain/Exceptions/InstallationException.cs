using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when structural, write access, SCM, or service swap failures occur during update package installation.
    /// </summary>
    public class InstallationException : UpdateException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class.
        /// </summary>
        public InstallationException() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class with a specified message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public InstallationException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InstallationException"/> class with a specified message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="innerException">The inner exception reference.</param>
        public InstallationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

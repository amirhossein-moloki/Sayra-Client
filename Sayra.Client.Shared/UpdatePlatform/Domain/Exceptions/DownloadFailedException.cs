using System;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Thrown when the download of an update package fails.
    /// </summary>
    public class DownloadFailedException : UpdateException
    {
        public DownloadFailedException() { }
        public DownloadFailedException(string message) : base(message) { }
        public DownloadFailedException(string message, Exception innerException) : base(message, innerException) { }
    }
}

using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when native Windows Restart Manager operations fail.
    /// </summary>
    public class RestartManagerException : UpdateException
    {
        public int ErrorCode { get; }

        public RestartManagerException(string message) : base(message) { }
        public RestartManagerException(string message, int errorCode) : base($"{message} (Error Code: {errorCode})")
        {
            ErrorCode = errorCode;
        }
        public RestartManagerException(string message, Exception innerException) : base(message, innerException) { }
    }
}

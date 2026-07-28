using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a diagnostic report generation fails.
    /// </summary>
    public class DiagnosticReportException : UpdateException
    {
        public DiagnosticReportException() { }

        public DiagnosticReportException(string message) : base(message) { }

        public DiagnosticReportException(string message, Exception innerException) : base(message, innerException) { }
    }
}

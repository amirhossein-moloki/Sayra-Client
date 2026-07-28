using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when eligibility evaluation fails due to internal errors or status conflicts.
    /// </summary>
    public class EligibilityEvaluationException : UpdateException
    {
        public EligibilityEvaluationException() { }

        public EligibilityEvaluationException(string message) : base(message) { }

        public EligibilityEvaluationException(string message, Exception innerException) : base(message, innerException) { }
    }
}

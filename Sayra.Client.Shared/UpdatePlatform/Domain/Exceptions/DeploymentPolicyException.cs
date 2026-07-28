using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a deployment policy rule blocks or restricts an update operation.
    /// </summary>
    public class DeploymentPolicyException : UpdateException
    {
        public DeploymentPolicyException() { }

        public DeploymentPolicyException(string message) : base(message) { }

        public DeploymentPolicyException(string message, Exception innerException) : base(message, innerException) { }
    }
}

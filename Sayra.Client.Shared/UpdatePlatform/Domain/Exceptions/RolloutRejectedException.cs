using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions
{
    /// <summary>
    /// Exception thrown when a workstation is excluded or rejected from a staged rollout.
    /// </summary>
    public class RolloutRejectedException : UpdateException
    {
        public RolloutRejectedException() { }

        public RolloutRejectedException(string message) : base(message) { }

        public RolloutRejectedException(string message, Exception innerException) : base(message, innerException) { }
    }
}

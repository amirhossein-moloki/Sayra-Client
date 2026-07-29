using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Recovery.Policies
{
    /// <summary>
    /// Configuration model for defining recovery execution dependencies and failure propagation rules.
    /// This model is immutable and serializable.
    /// </summary>
    public class DependencyPolicy
    {
        /// <summary>
        /// Gets the list of critical subsystems that must be fully healthy before recovering the target subsystem.
        /// </summary>
        public List<string> PreRecoveryDependencies { get; init; } = new();

        /// <summary>
        /// Gets a value indicating whether failures in this subsystem should immediately propagate to mark dependent subsystems as critical.
        /// </summary>
        public bool PropagateFailures { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether recovering this subsystem should trigger a cascade check/reinitialization on its immediate dependencies.
        /// </summary>
        public bool CascadeRecovery { get; init; }

        /// <summary>
        /// Gets a value indicating whether recovery of the target subsystem should be skipped if any of its critical dependencies are currently offline.
        /// </summary>
        public bool FailClosedOnDependencyFailure { get; init; } = true;
    }
}

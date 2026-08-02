using System;

namespace Sayra.Client.Shared.Models.Telemetry
{
    /// <summary>
    /// Represents an immutable summary of workstation update eligibility, rollout, and policy compliance.
    /// </summary>
    public record DashboardComplianceSummaryReadModel
    {
        /// <summary>
        /// Gets the compliance rating of policy configuration applications (0.0 to 100.0).
        /// </summary>
        public double PolicyCompliancePercent { get; init; }

        /// <summary>
        /// Gets the count of workstations with pending system updates.
        /// </summary>
        public int PendingUpdatesCount { get; init; }

        /// <summary>
        /// Gets the string-formatted timestamp of the last successfully applied policy configuration.
        /// </summary>
        public string LastPolicyAppliedTimestamp { get; init; } = string.Empty;

        /// <summary>
        /// Gets a value indicating whether security policies are actively enforced on the workstation.
        /// </summary>
        public bool SecurityPoliciesEnforced { get; init; } = true;

        /// <summary>
        /// Gets the exact timestamp when this read model was generated.
        /// </summary>
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }
}

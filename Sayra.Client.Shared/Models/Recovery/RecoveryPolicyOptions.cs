using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace Sayra.Client.Shared.Models.Recovery
{
    /// <summary>
    /// Configuration options for managing lists of reusable subsystem recovery policies.
    /// </summary>
    public class RecoveryPolicyOptions
    {
        /// <summary>
        /// Gets or sets the custom policies defined per subsystem.
        /// </summary>
        public List<RecoveryPolicy> CustomPolicies { get; set; } = new();
    }
}

using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the outcome of an update eligibility evaluation.
    /// </summary>
    public class EligibilityResult
    {
        /// <summary>
        /// Indicates if the workstation is eligible for the update.
        /// </summary>
        public bool IsEligible { get; set; }

        /// <summary>
        /// Detailed list of reasons for eligibility or ineligibility.
        /// </summary>
        public List<string> Reasons { get; set; } = new();

        /// <summary>
        /// When the evaluation occurred.
        /// </summary>
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    }
}

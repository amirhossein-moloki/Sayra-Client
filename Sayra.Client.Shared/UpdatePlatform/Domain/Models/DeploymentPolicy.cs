using System;

namespace Sayra.Client.Shared.UpdatePlatform.Domain.Models
{
    /// <summary>
    /// Represents the deployment policy applied to an update manifest or channel.
    /// </summary>
    public class DeploymentPolicy
    {
        /// <summary>
        /// Indicates if updates should be automatically checked, downloaded, and installed.
        /// </summary>
        public bool IsAutomatic { get; set; } = true;

        /// <summary>
        /// Indicates if updates require manual administrator or user approval before installation.
        /// </summary>
        public bool RequiresApproval { get; set; }

        /// <summary>
        /// Indicates if the update is mandatory/forced, bypassing regular constraints (such as active sessions).
        /// </summary>
        public bool IsForced { get; set; }

        /// <summary>
        /// Indicates if the update is optional for the workstation.
        /// </summary>
        public bool IsOptional { get; set; }

        /// <summary>
        /// Indicates if the update is an emergency hotfix that overrides all maintenance windows and ring delays.
        /// </summary>
        public bool IsEmergency { get; set; }

        /// <summary>
        /// Number of days to defer update eligibility after the manifest release date.
        /// </summary>
        public int DeferralDays { get; set; }
    }
}

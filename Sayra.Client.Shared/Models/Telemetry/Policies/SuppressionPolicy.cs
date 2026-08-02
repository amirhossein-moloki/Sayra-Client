using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models.Telemetry.Policies
{
    /// <summary>
    /// Reusable policy for temporary, permanent, maintenance windows, subsystem or rule alert suppressions.
    /// </summary>
    public class SuppressionPolicy
    {
        public bool IsSuppressed { get; set; }
        public DateTime? SuppressUntil { get; set; }
        public List<string> SuppressedSubsystems { get; set; } = new();
        public List<string> SuppressedRules { get; set; } = new();
        public bool MaintenanceWindowOnly { get; set; }
    }
}

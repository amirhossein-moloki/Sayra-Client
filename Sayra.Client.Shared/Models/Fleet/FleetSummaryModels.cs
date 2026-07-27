using System;
using System.Collections.Generic;

namespace Sayra.Client.Shared.Models
{
    public class FleetHealthSummary
    {
        public int TotalWorkstations { get; set; }
        public int OnlineCount { get; set; }
        public int OfflineCount { get; set; }
        public int MaintenanceCount { get; set; }
        public int HealthyCount { get; set; }
        public int WarningCount { get; set; }
        public int CriticalCount { get; set; }
    }

    public class FleetDiagnosticsSummary
    {
        public int TotalChecksPerformed { get; set; }
        public int SystemsWithDiskIssues { get; set; }
        public int SystemsWithTempIssues { get; set; }
        public int SystemsWithRamIssues { get; set; }
    }

    public class FleetPolicyStatus
    {
        public int PolicyAppliedCount { get; set; }
        public int OutOfSyncCount { get; set; }
        public Dictionary<string, int> PolicyVersionDistribution { get; set; } = new();
    }

    public class FleetSecurityStatus
    {
        public int SecureCount { get; set; }
        public int ViolatedCount { get; set; }
        public int BlockedApplicationsDetected { get; set; }
        public int RegistryTamperDetections { get; set; }
    }

    public class FleetVersionSummary
    {
        public string LatestClientVersion { get; set; } = "1.0.0";
        public Dictionary<string, int> ClientVersionDistribution { get; set; } = new();
    }

    public class FleetInventorySummary
    {
        public int TotalGamesInstalled { get; set; }
        public Dictionary<string, int> TopSoftwareInstalled { get; set; } = new();
    }

    public class FleetResourceUsageSummary
    {
        public double AverageCpuUsagePercent { get; set; }
        public double AverageRamUsagePercent { get; set; }
        public double AverageGpuUsagePercent { get; set; }
        public double AverageGpuTempCelsius { get; set; }
        public double AverageCpuTempCelsius { get; set; }
    }
}

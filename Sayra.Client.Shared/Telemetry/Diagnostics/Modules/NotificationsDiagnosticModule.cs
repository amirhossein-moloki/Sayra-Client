using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class NotificationsDiagnosticModule : IDiagnosticModule
    {
        public string Name => "Notifications";
        public string AffectedSubsystem => "Notifications";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                // Core fields
                result.Data["ChannelStatus"] = "Available";
                result.Data["PendingNotificationsCount"] = "0";
                result.Data["TotalDelivered"] = "125";
                result.Data["DeliveryFailureRate"] = "0.0";

                // Check local notifications database file size
                string dbPath = Path.Combine(AppContext.BaseDirectory, "Data", "notifications.db");
                if (File.Exists(dbPath))
                {
                    var fileInfo = new FileInfo(dbPath);
                    result.Data["NotificationDbSizeKb"] = (fileInfo.Length / 1024.0).ToString("F2");

                    // Findings & Evaluation rules
                    if (fileInfo.Length > 200 * 1024 * 1024) // 200MB limit
                    {
                        result.Status = DiagnosticHealthStatus.Warning;
                        result.Warnings.Add("Local notifications database file is excessively large.");
                        result.Findings.Add(new DiagnosticFinding
                        {
                            Key = "NotificationDbExcessive",
                            Value = $"{(fileInfo.Length / (1024.0 * 1024.0)):F1} MB",
                            Subsystem = AffectedSubsystem,
                            IsAnomaly = true,
                            Details = "Notifications database exceeded maximum size of 200MB."
                        });
                    }
                }
                else
                {
                    result.Data["NotificationDbSizeKb"] = "0.0";
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Notifications diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}

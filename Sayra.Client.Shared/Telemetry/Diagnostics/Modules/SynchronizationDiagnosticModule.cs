using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class SynchronizationDiagnosticModule : IDiagnosticModule
    {
        private readonly IServiceProvider? _serviceProvider;

        public SynchronizationDiagnosticModule(IServiceProvider? serviceProvider = null)
        {
            _serviceProvider = serviceProvider;
        }

        public string Name => "Synchronization";
        public string AffectedSubsystem => "Synchronization";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                bool isSyncActive = true;
                bool isSyncHealthy = true;
                DateTime lastSyncTime = DateTime.UtcNow.AddMinutes(-5);

                if (_serviceProvider != null)
                {
                    try
                    {
                        var syncServiceType = Type.GetType("SayraClient.Services.IWorkstationSyncService, SayraClient")
                            ?? Type.GetType("SayraClient.Services.IWorkstationSyncService");

                        if (syncServiceType != null)
                        {
                            var syncService = _serviceProvider.GetService(syncServiceType);
                            if (syncService != null)
                            {
                                var healthyMethod = syncService.GetType().GetMethod("IsHealthy");
                                if (healthyMethod != null) isSyncHealthy = (bool)healthyMethod.Invoke(syncService, null)!;

                                var activeMethod = syncService.GetType().GetMethod("IsActive");
                                if (activeMethod != null) isSyncActive = (bool)activeMethod.Invoke(syncService, null)!;

                                var lastSyncMethod = syncService.GetType().GetMethod("GetLastSyncTime");
                                if (lastSyncMethod != null) lastSyncTime = (DateTime)lastSyncMethod.Invoke(syncService, null)!;
                            }
                        }
                    }
                    catch
                    {
                        isSyncHealthy = false;
                    }
                }

                result.Data["SyncActive"] = isSyncActive.ToString();
                result.Data["SyncHealthy"] = isSyncHealthy.ToString();
                result.Data["LastSyncTime"] = lastSyncTime.ToString("o");

                var timeSinceLastSync = DateTime.UtcNow - lastSyncTime;
                result.Data["TimeSinceLastSyncMinutes"] = timeSinceLastSync.TotalMinutes.ToString("F1");

                // Findings & Evaluation rules
                if (!isSyncHealthy)
                {
                    result.Status = DiagnosticHealthStatus.Degraded;
                    result.Errors.Add("Workstation synchronization service is unhealthy or offline.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "SyncServiceOffline",
                        Value = "Unhealthy",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "Workstation replication synchronization service failed health checks."
                    });
                }
                else if (timeSinceLastSync.TotalHours > 1.0)
                {
                    result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add($"Workstation has not synchronized successfully in {timeSinceLastSync.TotalMinutes:F0} minutes.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "SyncServiceOffline",
                        Value = "StaleSync",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = $"Replication synchronization has not run successfully for over {timeSinceLastSync.TotalMinutes:F0} minutes."
                    });
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Synchronization diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}

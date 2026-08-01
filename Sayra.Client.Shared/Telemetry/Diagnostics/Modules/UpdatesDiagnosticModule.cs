using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class UpdatesDiagnosticModule : IDiagnosticModule
    {
        public string Name => "Updates";
        public string AffectedSubsystem => "Updates";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                string stagingPath = Path.Combine(AppContext.BaseDirectory, "Data", "UpdatesStaging");
                result.Data["StagingDirectoryPath"] = stagingPath;
                result.Data["ScheduledCheckIntervalMinutes"] = "240";
                result.Data["MaintenanceWindowOpen"] = "True";
                result.Data["PendingUpdatesCount"] = "0";

                bool stagingDirExists = Directory.Exists(stagingPath);
                result.Data["StagingDirectoryExists"] = stagingDirExists.ToString();

                string updateHistoryDb = Path.Combine(AppContext.BaseDirectory, "Data", "update_history.db");
                if (File.Exists(updateHistoryDb))
                {
                    var fileInfo = new FileInfo(updateHistoryDb);
                    result.Data["UpdateHistoryDbSizeKb"] = (fileInfo.Length / 1024.0).ToString("F2");
                }
                else
                {
                    result.Data["UpdateHistoryDbSizeKb"] = "0.0";
                }

                if (stagingDirExists)
                {
                    // Scan files (simulation)
                    try
                    {
                        var files = Directory.GetFiles(stagingPath, "*.*", SearchOption.AllDirectories);
                        long totalSize = 0;
                        foreach (var f in files)
                        {
                            try { totalSize += new FileInfo(f).Length; } catch { }
                        }
                        result.Data["StagedFilesSizeMb"] = (totalSize / (1024.0 * 1024.0)).ToString("F2");

                        // Findings & Evaluation rules
                        if (totalSize > 5L * 1024 * 1024 * 1024) // 5GB limit on staging files
                        {
                            result.Status = DiagnosticHealthStatus.Warning;
                            result.Warnings.Add("Update staging directory contains an excessively large volume of temporary staged files.");
                            result.Findings.Add(new DiagnosticFinding
                            {
                                Key = "StagingDirectoryCongested",
                                Value = $"{result.Data["StagedFilesSizeMb"]} MB",
                                Subsystem = AffectedSubsystem,
                                IsAnomaly = true,
                                Details = "Staged files size exceeded maximum temporary threshold of 5GB."
                            });
                        }
                    }
                    catch (Exception scanEx)
                    {
                        result.Warnings.Add($"Failed to scan staging directory: {scanEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Updates diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}

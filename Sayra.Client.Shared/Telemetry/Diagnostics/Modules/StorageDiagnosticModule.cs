using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class StorageDiagnosticModule : IDiagnosticModule
    {
        public string Name => "Storage";
        public string AffectedSubsystem => "Storage";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                string dataPath = Path.Combine(AppContext.BaseDirectory, "Data");
                string tempPath = Path.GetTempPath();

                result.Data["DataDirectoryPath"] = dataPath;
                result.Data["TempDirectoryPath"] = tempPath;

                // 1. Check Directory Existence
                bool dataExists = Directory.Exists(dataPath);
                result.Data["DataDirectoryExists"] = dataExists.ToString();

                // 2. Perform Write and Read Accessibility test on Temp and Data folder
                bool writeTestPassed = false;
                try
                {
                    if (!dataExists)
                    {
                        Directory.CreateDirectory(dataPath);
                    }

                    string testFile = Path.Combine(dataPath, "storage_diag_test.tmp");
                    File.WriteAllText(testFile, "SAYRA_DIAG");
                    string content = File.ReadAllText(testFile);
                    File.Delete(testFile);

                    writeTestPassed = (content == "SAYRA_DIAG");
                }
                catch (Exception writeEx)
                {
                    result.Status = DiagnosticHealthStatus.Critical;
                    result.Errors.Add($"Write accessibility test failed in Data directory: {writeEx.Message}");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "LowWriteAccess",
                        Value = "NoAccess",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = $"Failed write test with error: {writeEx.Message}"
                    });
                }

                result.Data["ReadWriteAccessibility"] = writeTestPassed ? "Passed" : "Failed";

                // 3. SQLite Size tracking
                string queueDb = Path.Combine(dataPath, "offline_queue.db");
                if (File.Exists(queueDb))
                {
                    var fileInfo = new FileInfo(queueDb);
                    result.Data["DatabaseSizeMb"] = (fileInfo.Length / (1024.0 * 1024.0)).ToString("F2");
                }
                else
                {
                    result.Data["DatabaseSizeMb"] = "0.0";
                }

                // 4. Temporary storage size check
                long tempDirSize = 0;
                try
                {
                    if (Directory.Exists(tempPath))
                    {
                        var files = Directory.GetFiles(tempPath, "*.*", SearchOption.TopDirectoryOnly);
                        foreach (var f in files)
                        {
                            try { tempDirSize += new FileInfo(f).Length; } catch { }
                        }
                    }
                }
                catch { }

                result.Data["TempFilesSizeMb"] = (tempDirSize / (1024.0 * 1024.0)).ToString("F2");

                if (tempDirSize > 2L * 1024 * 1024 * 1024) // > 2GB temp files
                {
                    if (result.Status < DiagnosticHealthStatus.Warning) result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add($"System temporary folder is highly congested: {result.Data["TempFilesSizeMb"]} MB.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "TempFolderCongested",
                        Value = $"{result.Data["TempFilesSizeMb"]} MB",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "System temporary folder exceeded 2GB of unpurged space."
                    });
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Storage diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}

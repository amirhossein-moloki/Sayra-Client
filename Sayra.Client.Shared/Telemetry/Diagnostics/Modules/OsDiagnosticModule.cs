using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class OsDiagnosticModule : IDiagnosticModule
    {
        public string Name => "OS";
        public string AffectedSubsystem => "OperatingSystem";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                result.Data["OSVersion"] = Environment.OSVersion.ToString();
                result.Data["Is64BitOperatingSystem"] = Environment.Is64BitOperatingSystem.ToString();
                result.Data["MachineName"] = Environment.MachineName;
                result.Data["ProcessorCount"] = Environment.ProcessorCount.ToString();
                result.Data["UserName"] = Environment.UserName;
                result.Data["SystemPageSize"] = Environment.SystemPageSize.ToString();

                // Simple simulated active sessions
                result.Data["ActiveSessions"] = "1";
                result.Data["SystemLoadIndicator"] = "Normal";

                // Findings & Evaluation rules
                if (!Environment.Is64BitOperatingSystem)
                {
                    result.Status = DiagnosticHealthStatus.Warning;
                    result.Warnings.Add("Workstation is running a 32-bit operating system.");
                    result.Findings.Add(new DiagnosticFinding
                    {
                        Key = "OutdatedOS",
                        Value = "32-bit",
                        Subsystem = AffectedSubsystem,
                        IsAnomaly = true,
                        Details = "OS architecture is 32-bit instead of 64-bit."
                    });
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"OS diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}

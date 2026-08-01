using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class PluginsDiagnosticModule : IDiagnosticModule
    {
        public string Name => "Plugins";
        public string AffectedSubsystem => "Plugins";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                // Core fields
                result.Data["PluginsDirectoryPath"] = Path.Combine(AppContext.BaseDirectory, "Plugins");
                result.Data["LoadedPluginsCount"] = "0";
                result.Data["FailedPluginsCount"] = "0";
                result.Data["IncompatiblePluginsCount"] = "0";

                // Check directory existence
                bool pluginsDirExists = Directory.Exists(result.Data["PluginsDirectoryPath"]);
                result.Data["PluginsDirectoryExists"] = pluginsDirExists.ToString();

                if (!pluginsDirExists)
                {
                    result.Data["Status"] = "NoPluginsConfigured";
                    result.Info.Add("No local plugins directory found. Workstation is operating in vanilla mode.");
                }
                else
                {
                    // Scan files (simulation)
                    try
                    {
                        var files = Directory.GetFiles(result.Data["PluginsDirectoryPath"], "*.dll");
                        result.Data["LoadedPluginsCount"] = files.Length.ToString();

                        string failFlag = Path.Combine(result.Data["PluginsDirectoryPath"], "plugin_failure.log");
                        if (File.Exists(failFlag))
                        {
                            result.Status = DiagnosticHealthStatus.Warning;
                            result.Data["FailedPluginsCount"] = "1";
                            result.Errors.Add("A plugin has failed initialization or crashed during startup.");
                            result.Findings.Add(new DiagnosticFinding
                            {
                                Key = "PluginCrashed",
                                Value = "Crashed",
                                Subsystem = AffectedSubsystem,
                                IsAnomaly = true,
                                Details = "Detected plugin failure log within Plugins directory."
                            });
                        }
                    }
                    catch (Exception scanEx)
                    {
                        result.Warnings.Add($"Failed to scan plugins folder: {scanEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Plugins diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}

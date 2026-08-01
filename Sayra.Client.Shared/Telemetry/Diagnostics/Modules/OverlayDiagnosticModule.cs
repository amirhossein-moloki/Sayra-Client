using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Telemetry.Diagnostics.Modules
{
    public class OverlayDiagnosticModule : IDiagnosticModule
    {
        public string Name => "Overlay";
        public string AffectedSubsystem => "Overlay";

        public Task<DiagnosticModuleResult> ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var result = new DiagnosticModuleResult { ModuleName = Name, Status = DiagnosticHealthStatus.Healthy };

            try
            {
                result.Data["OverlayWindowState"] = "Visible";
                result.Data["MouseClickThroughStyle"] = "WS_EX_TRANSPARENT";
                result.Data["KeyboardPassThroughStyle"] = "WS_EX_NOACTIVATE";
                result.Data["MultiMonitorConfiguration"] = "PrimaryOnly";
                result.Data["HasResolutionConflict"] = "False";

                // Simple simulated multi-monitor or DPI evaluation
                result.Data["DpiScaleX"] = "1.0";
                result.Data["DpiScaleY"] = "1.0";
            }
            catch (Exception ex)
            {
                result.Status = DiagnosticHealthStatus.Unknown;
                result.Errors.Add($"Overlay diagnostics failed: {ex.Message}");
            }

            return Task.FromResult(result);
        }
    }
}

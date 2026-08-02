using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry.Alerts
{
    public class AlertRecoveryProvider : IAlertRecoveryProvider
    {
        private readonly ILiveTelemetryService _liveTelemetryService;

        public AlertRecoveryProvider(ILiveTelemetryService liveTelemetryService)
        {
            _liveTelemetryService = liveTelemetryService ?? throw new ArgumentNullException(nameof(liveTelemetryService));
        }

        public async Task<bool> EvaluateRecoveryAsync(AlertRecord activeAlert, AlertPolicyConfig policy, CancellationToken cancellationToken = default)
        {
            if (policy == null || policy.Recovery == null || !policy.Recovery.AutoResolve)
                return false;

            double currentValue = 0;
            try
            {
                var telemetry = await _liveTelemetryService.CaptureSnapshotAsync(cancellationToken);
                switch (activeAlert.Name)
                {
                    case "CpuThreshold":
                        currentValue = telemetry.CpuUsagePercent;
                        break;
                    case "MemoryThreshold":
                        currentValue = telemetry.RamTotalMb > 0 ? (telemetry.RamUsedMb / telemetry.RamTotalMb) * 100 : 0;
                        break;
                    case "DiskUsage":
                        currentValue = telemetry.FreeSpaceGb;
                        break;
                    default:
                        return true;
                }
            }
            catch
            {
                return true;
            }

            double threshold = policy.Recovery.RecoveryThreshold;
            string op = policy.Recovery.RecoveryOperator;

            return op switch
            {
                "LessThan" => currentValue < threshold,
                "GreaterThan" => currentValue > threshold,
                "Equal" => Math.Abs(currentValue - threshold) < 0.001,
                "NotEqual" => Math.Abs(currentValue - threshold) >= 0.001,
                _ => currentValue < threshold
            };
        }
    }
}

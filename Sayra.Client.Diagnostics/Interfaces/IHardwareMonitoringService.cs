using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Events;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces
{
    public interface IHardwareMonitoringService
    {
        event EventHandler<TelemetryStartedEventArgs> TelemetryStarted;
        event EventHandler<TelemetryStoppedEventArgs> TelemetryStopped;
        event EventHandler<HardwareInitializedEventArgs> HardwareInitialized;
        event EventHandler<HardwareMetricsUpdatedEventArgs> HardwareMetricsUpdated;
        event EventHandler<HardwareValidationFailedEventArgs> HardwareValidationFailed;
        event EventHandler<HardwareChangedEventArgs> HardwareChanged;
        event EventHandler<DisplayChangedEventArgs> DisplayChanged;
        event EventHandler<NetworkChangedEventArgs> NetworkChanged;

        HardwareSpecification? CurrentSpecification { get; }
        HardwareMetrics? CurrentMetrics { get; }

        Task StartMonitoringAsync(CancellationToken cancellationToken = default);
        Task StopMonitoringAsync(CancellationToken cancellationToken = default);
    }
}

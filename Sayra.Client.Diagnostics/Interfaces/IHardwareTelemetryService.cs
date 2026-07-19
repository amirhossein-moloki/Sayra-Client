using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces
{
    public interface IHardwareTelemetryService
    {
        Task<HardwareMetrics> GetLiveMetricsAsync(CancellationToken cancellationToken = default);
    }
}

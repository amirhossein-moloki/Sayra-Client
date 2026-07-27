using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IAlertManager
    {
        Task ProcessMetricAsync(string workstationId, string metricType, double value, CancellationToken ct = default);
        Task ProcessStatusAsync(string workstationId, string statusType, string value, CancellationToken ct = default);
        Task<List<FleetAlert>> GetActiveAlertsAsync(CancellationToken ct = default);
        Task ResolveAlertAsync(string alertId, CancellationToken ct = default);
    }
}

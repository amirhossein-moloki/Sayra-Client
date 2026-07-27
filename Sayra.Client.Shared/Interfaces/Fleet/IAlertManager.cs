using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IAlertManager
    {
        Task ProcessMetricAsync(string machineId, string metricName, string value, CancellationToken cancellationToken = default);
        Task<List<FleetAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);
        Task ResolveAlertAsync(string alertId, CancellationToken cancellationToken = default);
        Task ConfigureRuleAsync(AlertRule rule, CancellationToken cancellationToken = default);
        Task<List<AlertRule>> GetAlertRulesAsync(CancellationToken cancellationToken = default);
    }
}

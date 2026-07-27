using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IEnterpriseOperationService
    {
        Task<Dictionary<string, object>> GetFleetHealthSummaryAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetFleetDiagnosticsSummaryAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetFleetPolicyStatusAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetFleetSecurityStatusAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetFleetVersionSummaryAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetFleetInventorySummaryAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, object>> GetFleetResourceUsageSummaryAsync(CancellationToken cancellationToken = default);
    }
}

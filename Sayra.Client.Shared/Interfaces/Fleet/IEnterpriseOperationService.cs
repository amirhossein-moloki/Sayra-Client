using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IEnterpriseOperationService
    {
        Task<FleetHealthSummary> GetFleetHealthSummaryAsync(CancellationToken ct = default);
        Task<FleetDiagnosticsSummary> GetFleetDiagnosticsSummaryAsync(CancellationToken ct = default);
        Task<FleetPolicyStatus> GetFleetPolicyStatusAsync(CancellationToken ct = default);
        Task<FleetSecurityStatus> GetFleetSecurityStatusAsync(CancellationToken ct = default);
        Task<FleetVersionSummary> GetFleetVersionSummaryAsync(CancellationToken ct = default);
        Task<FleetInventorySummary> GetFleetInventorySummaryAsync(CancellationToken ct = default);
        Task<FleetResourceUsageSummary> GetFleetResourceUsageSummaryAsync(CancellationToken ct = default);
    }
}

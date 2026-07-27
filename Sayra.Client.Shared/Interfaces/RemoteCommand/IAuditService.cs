using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IAuditService
    {
        Task RecordCommandReceivedAsync(string commandId, string action, string correlationId, CancellationToken cancellationToken = default);
        Task RecordSecurityValidationResultAsync(string commandId, bool success, string reason, string correlationId, CancellationToken cancellationToken = default);
        Task RecordExecutionStartedAsync(string commandId, string action, string correlationId, CancellationToken cancellationToken = default);
        Task RecordExecutionCompletedAsync(string commandId, string action, string correlationId, CancellationToken cancellationToken = default);
        Task RecordExecutionFailedAsync(string commandId, string action, string error, string correlationId, CancellationToken cancellationToken = default);
        Task RecordPolicyEventAsync(string policyId, string eventType, string details, string correlationId, CancellationToken cancellationToken = default);

        Task<List<AuditEntry>> GetAuditTrailAsync(CancellationToken cancellationToken = default);
        Task<bool> VerifyAuditChainIntegrityAsync(CancellationToken cancellationToken = default);
    }
}

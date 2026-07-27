using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IBulkOperationService
    {
        Task<string> ExecuteBulkOperationAsync(string action, List<string>? targetGroupIds, string? targetCollectionId, bool targetEntireFleet, string adminId, string signature, CancellationToken ct = default);
        Task<BulkOperation?> GetBulkOperationAsync(string operationId, CancellationToken ct = default);
        Task<List<BulkOperationResult>> GetBulkOperationResultsAsync(string operationId, CancellationToken ct = default);
        Task CancelBulkOperationAsync(string operationId, CancellationToken ct = default);
    }
}

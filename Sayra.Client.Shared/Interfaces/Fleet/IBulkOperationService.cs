using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IBulkOperationService
    {
        Task<BulkOperation> StartBulkOperationAsync(string action, string targetType, string targetValue, string payload, CancellationToken cancellationToken = default);
        Task CancelBulkOperationAsync(string operationId, CancellationToken cancellationToken = default);
        Task<BulkOperation?> GetBulkOperationStatusAsync(string operationId, CancellationToken cancellationToken = default);
        Task<List<BulkOperationResult>> GetBulkOperationResultsAsync(string operationId, CancellationToken cancellationToken = default);
        Task RetryFailedBulkOperationsAsync(string operationId, CancellationToken cancellationToken = default);
    }
}

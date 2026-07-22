using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.OfflineQueue.Models;

namespace Sayra.Client.OfflineQueue;

public interface IOfflineQueueManager
{
    Task AddEventAsync(ClientEvent clientEvent);
    Task<List<QueueItem>> GetPendingEventsAsync(int limit = 100);
    Task MarkCompletedAsync(long id);
    Task RecordFailureAsync(long id, string errorMessage, int maxRetries = 5);
    Task DeleteExpiredEventsAsync(TimeSpan maxAge);
    Task<List<DeadLetterQueueItem>> GetDeadLetterItemsAsync();
    Task<long> GetQueueSizeInBytesAsync();
    Task<int> GetPendingCountAsync();
    Task<bool> VerifyIntegrityAsync();
    Task ForceRecreateDatabaseAsync();
}

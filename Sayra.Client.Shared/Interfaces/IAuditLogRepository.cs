using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IAuditLogRepository
    {
        Task AddLogAsync(EventLogEntry entry);
        Task<List<EventLogEntry>> GetPendingLogsAsync(int limit = 100);
        Task DeleteLogsAsync(List<Guid> eventIds);
        Task<int> GetPendingLogsCountAsync();
    }
}

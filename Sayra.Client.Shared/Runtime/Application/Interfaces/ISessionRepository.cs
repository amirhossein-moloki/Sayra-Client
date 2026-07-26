using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Domain.Entities;

namespace Sayra.Client.Shared.Runtime.Application.Interfaces
{
    public interface ISessionRepository
    {
        Task SaveAsync(RuntimeSession session);
        Task<RuntimeSession?> GetAsync(Guid sessionId);
        Task<IEnumerable<RuntimeSession>> GetActiveSessionsAsync();
        Task DeleteAsync(Guid sessionId);
    }
}

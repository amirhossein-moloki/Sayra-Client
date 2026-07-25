using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Domain.Entities;
using Sayra.Client.Shared.Runtime.Domain.States;

namespace Sayra.Client.Shared.Runtime.Application.Interfaces
{
    public interface IRuntimeSessionManager
    {
        Task<RuntimeSession> CreateAsync();
        Task<RuntimeSession> CreateAsync(string userId, string gameId);
        Task StopAsync(Guid sessionId);
        RuntimeSession? GetSession(Guid sessionId);
        void UpdateSessionState(Guid sessionId, RuntimeState state);
    }
}

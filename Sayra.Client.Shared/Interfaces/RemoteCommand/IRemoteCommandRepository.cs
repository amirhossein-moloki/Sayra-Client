using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IRemoteCommandRepository
    {
        Task SaveCommandAsync(RemoteCommandHistory command, CancellationToken cancellationToken = default);
        Task<RemoteCommandHistory?> GetCommandAsync(string commandId, CancellationToken cancellationToken = default);
        Task<List<RemoteCommandHistory>> GetPendingCommandsAsync(CancellationToken cancellationToken = default);
        Task UpdateStatusAsync(string commandId, string status, string? errorMessage = null, CancellationToken cancellationToken = default);
        Task DeleteCommandAsync(string commandId, CancellationToken cancellationToken = default);
        Task<List<RemoteCommandHistory>> GetHistoryAsync(CancellationToken cancellationToken = default);
    }
}

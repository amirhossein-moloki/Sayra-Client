using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IDeadLetterQueue
    {
        Task MoveToDeadLetterAsync(RemoteCommandHistory command, string failureReason, int retryCount, CancellationToken cancellationToken = default);
        Task<List<DeadLetterCommand>> GetDeadLetterCommandsAsync(CancellationToken cancellationToken = default);
        Task<DeadLetterCommand?> GetDeadLetterCommandAsync(string commandId, CancellationToken cancellationToken = default);
    }
}

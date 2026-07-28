using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Responsible for handling corruption, deleting compromised files, and orchestrating database recreations.
    /// </summary>
    public interface IDatabaseRecoveryService
    {
        Task<bool> RecreateDatabaseAsync(CancellationToken cancellationToken = default);
        Task<bool> RecoverAndRecreateAsync(Exception ex, CancellationToken cancellationToken = default);
    }
}

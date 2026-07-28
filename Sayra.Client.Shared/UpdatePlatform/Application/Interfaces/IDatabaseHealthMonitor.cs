using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Responsible for checking the database file integrity and verifying schema validity.
    /// </summary>
    public interface IDatabaseHealthMonitor
    {
        Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default);
        Task<bool> ValidateSchemaAsync(CancellationToken cancellationToken = default);
    }
}

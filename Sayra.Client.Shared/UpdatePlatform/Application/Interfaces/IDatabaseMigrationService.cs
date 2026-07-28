using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Performs localized schema creation and version-controlled database migrations.
    /// </summary>
    public interface IDatabaseMigrationService
    {
        Task MigrateAsync(CancellationToken cancellationToken = default);
        Task<int> GetCurrentVersionAsync(CancellationToken cancellationToken = default);
    }
}

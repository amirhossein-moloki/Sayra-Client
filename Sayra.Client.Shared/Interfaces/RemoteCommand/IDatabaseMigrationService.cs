using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IDatabaseMigrationService
    {
        Task ApplyMigrationsAsync(DbConnection connection, CancellationToken cancellationToken = default);
    }
}

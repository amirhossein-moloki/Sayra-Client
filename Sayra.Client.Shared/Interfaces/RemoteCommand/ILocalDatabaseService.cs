using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces
{
    public interface ILocalDatabaseService : IDisposable
    {
        string GetConnectionString();
        Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
        DbConnection CreateConnection();
        Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);
        Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default);
        Task CloseSafelyAsync();
    }
}

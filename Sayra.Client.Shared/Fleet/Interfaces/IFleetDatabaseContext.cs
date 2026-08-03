using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Fleet.Interfaces
{
    /// <summary>
    /// Contract for the secure SQLCipher fleet database connection and lifecycle provider.
    /// </summary>
    public interface IFleetDatabaseContext : IDisposable
    {
        /// <summary>
        /// Retrieves the SQLCipher database connection string.
        /// </summary>
        string GetConnectionString();

        /// <summary>
        /// Creates a new open/closed database connection with SQLCipher password applied.
        /// </summary>
        DbConnection CreateConnection();

        /// <summary>
        /// Asynchronously creates a database connection.
        /// </summary>
        Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Initializes the encrypted database, applying migrations.
        /// </summary>
        Task InitializeDatabaseAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Verifies structural and encryption integrity of the database file.
        /// </summary>
        Task<bool> VerifyIntegrityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Safely closes open connections and clears connection pools.
        /// </summary>
        Task CloseSafelyAsync();
    }
}

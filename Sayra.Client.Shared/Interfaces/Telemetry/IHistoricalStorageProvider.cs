using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Telemetry
{
    /// <summary>
    /// Abstracts the underlying historical telemetry database platform (e.g. SQLite, SQL Server, PostgreSQL, Cloud).
    /// </summary>
    public interface IHistoricalStorageProvider
    {
        /// <summary>
        /// Gets the identifying name of the active storage provider.
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Initializes the storage engine, database connection, and tables.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a non-query command (e.g., INSERT, UPDATE, DELETE).
        /// </summary>
        /// <param name="sql">The SQL statement or command text.</param>
        /// <param name="parameters">Parameters to bind to the command.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ExecuteNonQueryAsync(string sql, Dictionary<string, object?> parameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a parameterized query and returns mapped entities.
        /// </summary>
        /// <typeparam name="T">The mapped entity type.</typeparam>
        /// <param name="sql">The SQL query statement.</param>
        /// <param name="parameters">Parameters to bind to the query.</param>
        /// <param name="map">The mapping delegate from IDataRecord to T.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task<List<T>> QueryAsync<T>(string sql, Dictionary<string, object?> parameters, Func<IDataRecord, T> map, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a series of commands within a high-performance database transaction.
        /// </summary>
        /// <param name="sql">The SQL template with parameters.</param>
        /// <param name="batchParameters">An enumerable collection of parameter sets, one set per insert/update.</param>
        /// <param name="cancellationToken">Token to cancel the operation.</param>
        Task ExecuteBatchAsync(string sql, IEnumerable<Dictionary<string, object?>> batchParameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current database file size in bytes, if applicable.
        /// </summary>
        long GetStorageSizeBytes();
    }
}

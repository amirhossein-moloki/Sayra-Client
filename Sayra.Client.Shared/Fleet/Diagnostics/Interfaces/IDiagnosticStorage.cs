using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Fleet.Diagnostics.Interfaces
{
    /// <summary>
    /// Storage abstraction governing temporary, local, and archived diagnostics package files,
    /// complete with retention cleanups and capacity boundary enforcement.
    /// </summary>
    public interface IDiagnosticStorage
    {
        /// <summary>
        /// Saves a generated diagnostics compressed package to local/archive storage.
        /// </summary>
        Task SavePackageAsync(string packageId, byte[] packageData, string fileName, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the raw byte contents of a saved diagnostics package.
        /// </summary>
        Task<byte[]?> GetPackageAsync(string packageId, CancellationToken ct = default);

        /// <summary>
        /// Retrieves the absolute physical file system path for a saved diagnostics package.
        /// </summary>
        Task<string> GetPackagePathAsync(string packageId, CancellationToken ct = default);

        /// <summary>
        /// Deletes a diagnostics package from storage.
        /// </summary>
        Task DeletePackageAsync(string packageId, CancellationToken ct = default);

        /// <summary>
        /// Runs a storage-limit cleanup policy, pruning oldest archives when allocated capacity limits are exceeded.
        /// </summary>
        Task EnforceCleanupPolicyAsync(CancellationToken ct = default);

        /// <summary>
        /// Deletes packages that are older than the specified retention age.
        /// </summary>
        Task ClearExpiredPackagesAsync(TimeSpan expiration, CancellationToken ct = default);
    }
}

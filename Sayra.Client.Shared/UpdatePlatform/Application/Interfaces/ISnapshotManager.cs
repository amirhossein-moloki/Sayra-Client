using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Manages the pre-installation snapshot lifecycle of binaries, configurations, databases, and metadata.
    /// </summary>
    public interface ISnapshotManager
    {
        /// <summary>
        /// Creates a complete snapshot of the system state.
        /// </summary>
        Task<BackupSnapshot> CreateSnapshotAsync(string snapshotId, string sourceBinariesDir, string sourceConfigDir, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates snapshot integrity before use.
        /// </summary>
        Task<bool> ValidateSnapshotAsync(BackupSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>
        /// Restores target directories to a previously saved snapshot.
        /// </summary>
        Task<bool> RestoreSnapshotAsync(BackupSnapshot snapshot, string targetBinariesDir, string targetConfigDir, CancellationToken cancellationToken = default);
    }
}

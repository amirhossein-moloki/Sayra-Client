using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Phase9;

namespace Sayra.Client.Shared.Fleet.Services
{
    /// <summary>
    /// Implements IRemoteFileService to act as the top-level remote file operations gateway.
    /// </summary>
    public class RemoteFileManagementEngine : IRemoteFileService
    {
        private readonly IFileOperationCoordinator _coordinator;

        /// <summary>
        /// Initializes a new instance of RemoteFileManagementEngine.
        /// </summary>
        public RemoteFileManagementEngine(IFileOperationCoordinator coordinator)
        {
            _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        /// <summary>
        /// Lists files and subdirectories on a remote workstation.
        /// </summary>
        public async Task<IReadOnlyList<string>> ListFilesAsync(string machineId, string path, CancellationToken ct = default)
        {
            var directoryEntry = await _coordinator.ListDirectoryAsync("admin-system", path, ct).ConfigureAwait(false);
            var pathsList = new List<string>();

            foreach (var sub in directoryEntry.SubDirectories)
            {
                pathsList.Add(sub.FullPath);
            }

            foreach (var file in directoryEntry.Files)
            {
                pathsList.Add(file.FullPath);
            }

            return pathsList;
        }

        /// <summary>
        /// Deletes a file on a target remote workstation.
        /// </summary>
        public async Task<bool> DeleteFileAsync(string machineId, string filePath, CancellationToken ct = default)
        {
            return await _coordinator.DeleteFileAsync("admin-system", filePath, ct).ConfigureAwait(false);
        }
    }
}

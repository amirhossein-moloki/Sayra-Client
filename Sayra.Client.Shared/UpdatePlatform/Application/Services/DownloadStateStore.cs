using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Transaction-safe implementation of local storage to persist and retrieve download job states.
    /// </summary>
    public class DownloadStateStore : IDownloadStateStore
    {
        private readonly string _storeDirectory;
        private readonly object _fileLock = new object();

        public DownloadStateStore(string? customStoreDirectory = null)
        {
            _storeDirectory = customStoreDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "SAYRA_Client", "Downloads");
        }

        private string GetFilePath(Guid packageId)
        {
            return Path.Combine(_storeDirectory, $"{packageId:D}.json");
        }

        public Task SaveJobAsync(DownloadJob job, CancellationToken cancellationToken = default)
        {
            if (job == null) throw new ArgumentNullException(nameof(job));

            lock (_fileLock)
            {
                if (!Directory.Exists(_storeDirectory))
                {
                    Directory.CreateDirectory(_storeDirectory);
                }

                string filePath = GetFilePath(job.PackageId);
                string tempFilePath = filePath + ".tmp";

                try
                {
                    string json = JsonSerializer.Serialize(job, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(tempFilePath, json);

                    // Atomic write transaction replacement
                    if (File.Exists(filePath))
                    {
                        File.Replace(tempFilePath, filePath, null);
                    }
                    else
                    {
                        File.Move(tempFilePath, filePath);
                    }
                }
                catch (Exception ex)
                {
                    if (File.Exists(tempFilePath))
                    {
                        try { File.Delete(tempFilePath); } catch { /* Ignore */ }
                    }
                    throw new IOException($"Failed to save download state atomically for package {job.PackageId}", ex);
                }
            }

            return Task.CompletedTask;
        }

        public Task<DownloadJob?> LoadJobAsync(Guid packageId, CancellationToken cancellationToken = default)
        {
            lock (_fileLock)
            {
                string filePath = GetFilePath(packageId);
                if (!File.Exists(filePath))
                {
                    return Task.FromResult<DownloadJob?>(null);
                }

                try
                {
                    string json = File.ReadAllText(filePath);
                    var job = JsonSerializer.Deserialize<DownloadJob>(json);
                    return Task.FromResult(job);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Failed to load download state for package {packageId}", ex);
                }
            }
        }

        public Task DeleteJobAsync(Guid packageId, CancellationToken cancellationToken = default)
        {
            lock (_fileLock)
            {
                string filePath = GetFilePath(packageId);
                if (File.Exists(filePath))
                {
                    try
                    {
                        File.Delete(filePath);
                    }
                    catch (Exception ex)
                    {
                        throw new IOException($"Failed to delete download state for package {packageId}", ex);
                    }
                }
            }

            return Task.CompletedTask;
        }
    }
}

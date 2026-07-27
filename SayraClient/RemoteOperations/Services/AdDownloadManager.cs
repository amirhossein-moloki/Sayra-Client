using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class AdDownloadManager : IAdDownloadManager
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AdDownloadManager> _logger;
        private long _diskQuotaLimitBytes = 500 * 1024 * 1024; // Default 500MB
        private readonly SemaphoreSlim _lock = new(1, 1);

        public AdDownloadManager(HttpClient httpClient, ILogger<AdDownloadManager> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SetDiskQuotaLimitAsync(long bytesLimit)
        {
            await _lock.WaitAsync();
            try
            {
                _diskQuotaLimitBytes = bytesLimit;
                _logger.LogInformation("AdDownloadManager disk quota limit set to {LimitMB} MB", bytesLimit / (1024 * 1024));
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<long> GetDiskQuotaUsageAsync()
        {
            await _lock.WaitAsync();
            try
            {
                return CalculateFolderSize(GetDownloadDirectory());
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> DownloadMediaAsync(AdCampaign campaign, CancellationToken cancellationToken = default)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));

            _logger.LogInformation("Starting download for campaign media '{CampaignId}' from '{Url}'", campaign.CampaignId, campaign.MediaUrl);

            var localPath = campaign.MediaLocalPath;
            if (string.IsNullOrEmpty(localPath))
            {
                localPath = Path.Combine(GetDownloadDirectory(), $"{campaign.CampaignId}_{Path.GetFileName(campaign.MediaUrl)}");
                campaign.MediaLocalPath = localPath;
            }

            var directory = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = localPath + ".tmp";

            // Check if file already exists with same checksum
            if (File.Exists(localPath))
            {
                var currentChecksum = CalculateFileSha256(localPath);
                if (currentChecksum.Equals(campaign.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("File already exists and is valid for campaign '{CampaignId}'", campaign.CampaignId);
                    campaign.IsDownloaded = true;
                    return true;
                }
                else
                {
                    _logger.LogWarning("File exists but checksum mismatch. Deleting and re-downloading.");
                    File.Delete(localPath);
                }
            }

            // Retry policy
            int retries = 3;
            int delayMs = 1000;
            for (int attempt = 1; attempt <= retries; attempt++)
            {
                try
                {
                    bool success = await DownloadInternalAsync(campaign.MediaUrl, tempPath, cancellationToken);
                    if (success)
                    {
                        // Verify checksum
                        var fileChecksum = CalculateFileSha256(tempPath);
                        if (!fileChecksum.Equals(campaign.Checksum, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogError("Checksum verification failed for '{CampaignId}'. Expected: {Expected}, Actual: {Actual}", campaign.CampaignId, campaign.Checksum, fileChecksum);
                            if (File.Exists(tempPath)) File.Delete(tempPath);
                            throw new InvalidDataException("Checksum verification failed");
                        }

                        // Check disk quota first
                        var mediaSize = new FileInfo(tempPath).Length;
                        await _lock.WaitAsync(cancellationToken);
                        try
                        {
                            var currentUsage = CalculateFolderSize(GetDownloadDirectory());
                            if (currentUsage + mediaSize > _diskQuotaLimitBytes)
                            {
                                _logger.LogWarning("Insufficient disk quota. Current: {Current}, Limit: {Limit}, Requested: {Requested}", currentUsage, _diskQuotaLimitBytes, mediaSize);
                                if (File.Exists(tempPath)) File.Delete(tempPath);
                                return false; // Quota exceeded
                            }
                        }
                        finally
                        {
                            _lock.Release();
                        }

                        // Move temp to destination
                        if (File.Exists(localPath)) File.Delete(localPath);
                        File.Move(tempPath, localPath);

                        campaign.IsDownloaded = true;
                        campaign.MediaSize = mediaSize;
                        _logger.LogInformation("Successfully downloaded media for campaign '{CampaignId}'", campaign.CampaignId);
                        return true;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Download canceled for campaign '{CampaignId}'", campaign.CampaignId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Download attempt {Attempt} failed for campaign '{CampaignId}'", attempt, campaign.CampaignId);
                    if (attempt == retries)
                    {
                        _logger.LogError(ex, "All download attempts failed for campaign '{CampaignId}'", campaign.CampaignId);
                        return false;
                    }
                    await Task.Delay(delayMs * attempt, cancellationToken);
                }
            }

            return false;
        }

        public async Task<bool> ResumeDownloadAsync(AdCampaign campaign, string tempPath, CancellationToken cancellationToken = default)
        {
            if (campaign == null) throw new ArgumentNullException(nameof(campaign));
            if (string.IsNullOrEmpty(tempPath)) throw new ArgumentException("Temp path cannot be empty", nameof(tempPath));

            _logger.LogInformation("Resuming download for campaign media '{CampaignId}' from '{Url}' onto temp path '{TempPath}'", campaign.CampaignId, campaign.MediaUrl, tempPath);

            long existingLength = 0;
            if (File.Exists(tempPath))
            {
                existingLength = new FileInfo(tempPath).Length;
                _logger.LogInformation("Resuming from {Offset} bytes", existingLength);
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, campaign.MediaUrl);
                if (existingLength > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(existingLength, null);
                }

                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                bool isPartial = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(tempPath, isPartial ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

                var buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                }

                fileStream.Close();

                // Validate and finalize
                var fileChecksum = CalculateFileSha256(tempPath);
                if (!fileChecksum.Equals(campaign.Checksum, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError("Resumed download checksum verification failed for '{CampaignId}'. Expected: {Expected}, Actual: {Actual}", campaign.CampaignId, campaign.Checksum, fileChecksum);
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    return false;
                }

                var localPath = campaign.MediaLocalPath;
                if (File.Exists(localPath)) File.Delete(localPath);
                File.Move(tempPath, localPath);

                campaign.IsDownloaded = true;
                campaign.MediaSize = new FileInfo(localPath).Length;
                _logger.LogInformation("Successfully completed resumed download for '{CampaignId}'", campaign.CampaignId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resume download for campaign '{CampaignId}'", campaign.CampaignId);
                return false;
            }
        }

        public Task CleanupOrphanDownloadsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cleaning up orphan downloads...");
            var dir = GetDownloadDirectory();
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.tmp");
                foreach (var file in files)
                {
                    try
                    {
                        // Clean temp files older than 1 day
                        if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-1))
                        {
                            File.Delete(file);
                            _logger.LogInformation("Deleted old temporary download file '{File}'", file);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old temporary file '{File}'", file);
                    }
                }
            }
            return Task.CompletedTask;
        }

        private async Task<bool> DownloadInternalAsync(string url, string tempPath, CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true);

            var buffer = new byte[8192];
            int bytesRead;
            while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
            }

            return true;
        }

        private string CalculateFileSha256(string filepath)
        {
            using var sha256 = SHA256.Create();
            using var fileStream = File.OpenRead(filepath);
            var hashBytes = sha256.ComputeHash(fileStream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }

        private long CalculateFolderSize(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return 0;
            long size = 0;
            var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch { }
            }
            return size;
        }

        private string GetDownloadDirectory()
        {
            var basePath = AppContext.BaseDirectory;
            if (OperatingSystem.IsWindows())
            {
                basePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Sayra", "AdCache");
            }
            else
            {
                basePath = Path.Combine(basePath, "AdCache");
            }

            if (!Directory.Exists(basePath))
            {
                Directory.CreateDirectory(basePath);
            }
            return basePath;
        }
    }
}

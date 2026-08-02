using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;

namespace Sayra.Client.Shared.Telemetry.Historical
{
    /// <summary>
    /// File-based implementation of the Historical Archive Provider.
    /// Uses serialized containers with SHA-256 integrity signatures for robust validation.
    /// </summary>
    public class FileHistoricalArchiveProvider : IHistoricalArchiveProvider
    {
        private readonly ILogger<FileHistoricalArchiveProvider> _logger;

        public string ProviderName => "Local File Archive Provider";

        public FileHistoricalArchiveProvider(ILogger<FileHistoricalArchiveProvider> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private class ArchiveContainer
        {
            public Dictionary<string, string> Metadata { get; set; } = new();
            public string Data { get; set; } = string.Empty;
        }

        public async Task ArchiveAsync(string archiveFilePath, IReadOnlyCollection<HistoricalMetric> metrics, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(archiveFilePath)) throw new ArgumentException("Archive file path cannot be null or empty.", nameof(archiveFilePath));
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));

            _logger.LogInformation("Archiving {Count} historical metrics to: {Path}", metrics.Count, archiveFilePath);

            try
            {
                var dir = Path.GetDirectoryName(archiveFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var serializedData = JsonSerializer.Serialize(metrics);
                var checksum = CalculateSha256(serializedData);

                // Analyze date range in the collection
                DateTime minDate = DateTime.MaxValue;
                DateTime maxDate = DateTime.MinValue;
                foreach (var m in metrics)
                {
                    if (m.Timestamp < minDate) minDate = m.Timestamp;
                    if (m.Timestamp > maxDate) maxDate = m.Timestamp;
                }

                var metadata = new Dictionary<string, string>
                {
                    ["ArchiveVersion"] = "1.0",
                    ["MachineId"] = Environment.MachineName,
                    ["CreatedUtc"] = DateTime.UtcNow.ToString("O"),
                    ["MetricCount"] = metrics.Count.ToString(),
                    ["StartDateUtc"] = metrics.Count > 0 ? minDate.ToString("O") : DateTime.MinValue.ToString("O"),
                    ["EndDateUtc"] = metrics.Count > 0 ? maxDate.ToString("O") : DateTime.MinValue.ToString("O"),
                    ["Sha256Checksum"] = checksum
                };

                var container = new ArchiveContainer
                {
                    Metadata = metadata,
                    Data = serializedData
                };

                var serializedContainer = JsonSerializer.Serialize(container, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(archiveFilePath, serializedContainer, Encoding.UTF8, cancellationToken);

                _logger.LogInformation("Archive created successfully. Checksum: {Checksum}", checksum);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create historical archive at: {Path}", archiveFilePath);
                throw new HistoricalStorageException($"Archive creation failed.", ex);
            }
        }

        public async Task<IReadOnlyCollection<HistoricalMetric>> RestoreAsync(string archiveFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(archiveFilePath)) throw new ArgumentException("Archive file path cannot be null or empty.", nameof(archiveFilePath));

            _logger.LogInformation("Restoring historical metrics from: {Path}", archiveFilePath);

            try
            {
                if (!File.Exists(archiveFilePath))
                {
                    throw new FileNotFoundException("Archive file not found.", archiveFilePath);
                }

                var serializedContainer = await File.ReadAllTextAsync(archiveFilePath, Encoding.UTF8, cancellationToken);
                var container = JsonSerializer.Deserialize<ArchiveContainer>(serializedContainer);

                if (container == null)
                {
                    throw new HistoricalStorageException("Archive container is corrupt or cannot be parsed.");
                }

                // Integrity Validation
                if (!container.Metadata.TryGetValue("Sha256Checksum", out var expectedChecksum))
                {
                    throw new HistoricalStorageException("Archive metadata does not contain an integrity checksum.");
                }

                var actualChecksum = CalculateSha256(container.Data);
                if (!string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase))
                {
                    throw new HistoricalStorageException("Archive integrity verification failed! Data has been tampered with or corrupted.");
                }

                var metrics = JsonSerializer.Deserialize<List<HistoricalMetric>>(container.Data);
                return metrics ?? new List<HistoricalMetric>();
            }
            catch (Exception ex) when (!(ex is HistoricalStorageException))
            {
                _logger.LogError(ex, "Failed to restore historical archive from: {Path}", archiveFilePath);
                throw new HistoricalStorageException($"Archive restore failed.", ex);
            }
        }

        public async Task<bool> ValidateArchiveAsync(string archiveFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(archiveFilePath)) return false;

            try
            {
                if (!File.Exists(archiveFilePath)) return false;

                var serializedContainer = await File.ReadAllTextAsync(archiveFilePath, Encoding.UTF8, cancellationToken);
                var container = JsonSerializer.Deserialize<ArchiveContainer>(serializedContainer);

                if (container == null) return false;

                if (!container.Metadata.TryGetValue("Sha256Checksum", out var expectedChecksum)) return false;

                var actualChecksum = CalculateSha256(container.Data);
                return string.Equals(expectedChecksum, actualChecksum, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Archive validation failed for: {Path}", archiveFilePath);
                return false;
            }
        }

        public async Task<Dictionary<string, string>> GetArchiveMetadataAsync(string archiveFilePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(archiveFilePath)) throw new ArgumentException("Archive file path cannot be null or empty.", nameof(archiveFilePath));

            try
            {
                if (!File.Exists(archiveFilePath))
                {
                    throw new FileNotFoundException("Archive file not found.", archiveFilePath);
                }

                var serializedContainer = await File.ReadAllTextAsync(archiveFilePath, Encoding.UTF8, cancellationToken);
                var container = JsonSerializer.Deserialize<ArchiveContainer>(serializedContainer);

                if (container == null)
                {
                    throw new HistoricalStorageException("Archive container could not be read.");
                }

                return container.Metadata;
            }
            catch (Exception ex) when (!(ex is HistoricalStorageException))
            {
                _logger.LogError(ex, "Failed to retrieve archive metadata from: {Path}", archiveFilePath);
                throw new HistoricalStorageException($"Failed to retrieve archive metadata.", ex);
            }
        }

        private static string CalculateSha256(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = SHA256.HashData(bytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}

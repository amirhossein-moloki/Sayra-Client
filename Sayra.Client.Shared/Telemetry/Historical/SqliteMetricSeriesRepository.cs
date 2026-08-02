using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry.Historical
{
    /// <summary>
    /// SQLite implementation of the Metric Series repository with versioned format,
    /// backward compatibility, and transparent configurable GZip compression.
    /// </summary>
    public class SqliteMetricSeriesRepository : IMetricSeriesRepository
    {
        private readonly IHistoricalStorageProvider _storageProvider;
        private readonly HistoricalStorageOptions _options;
        private readonly ILogger<SqliteMetricSeriesRepository> _logger;

        private static readonly byte[] MagicPrefix = { 0x53, 0x4D, 0x43 }; // 'S', 'M', 'C'
        private const byte FormatVersion = 1;
        private const byte CompressionTypeNone = 0;
        private const byte CompressionTypeGZip = 1;

        public SqliteMetricSeriesRepository(
            IHistoricalStorageProvider storageProvider,
            IOptions<HistoricalStorageOptions> options,
            ILogger<SqliteMetricSeriesRepository> logger)
        {
            _storageProvider = storageProvider ?? throw new ArgumentNullException(nameof(storageProvider));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task SaveSeriesAsync(MetricSeries series, CancellationToken cancellationToken = default)
        {
            if (series == null) throw new ArgumentNullException(nameof(series));

            var serializedPoints = JsonSerializer.Serialize(series.Points);
            byte[] processedBytes;

            if (_options.UseCompression)
            {
                processedBytes = CompressPoints(serializedPoints);
            }
            else
            {
                processedBytes = PackUncompressedPoints(serializedPoints);
            }

            var sql = @"
                INSERT OR REPLACE INTO MetricSeries (
                    MetricName, Category, Unit, Points
                ) VALUES (
                    $MetricName, $Category, $Unit, $Points
                );";

            var parameters = new Dictionary<string, object?>
            {
                ["$MetricName"] = series.MetricName,
                ["$Category"] = (int)series.Category,
                ["$Unit"] = (int)series.Unit,
                ["$Points"] = processedBytes
            };

            await _storageProvider.ExecuteNonQueryAsync(sql, parameters, cancellationToken);
        }

        public async Task<MetricSeries?> GetSeriesAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("Metric name cannot be null or empty.", nameof(name));

            var sql = "SELECT * FROM MetricSeries WHERE MetricName = $MetricName;";
            var parameters = new Dictionary<string, object?> { ["$MetricName"] = name };

            var results = await _storageProvider.QueryAsync(sql, parameters, ReadRecord, cancellationToken);
            return results.Count > 0 ? results[0] : null;
        }

        public async Task<MetricSeries?> QuerySeriesAsync(string name, DateTime start, DateTime end, CancellationToken cancellationToken = default)
        {
            var series = await GetSeriesAsync(name, cancellationToken);
            if (series == null) return null;

            // Filter points in memory to return range-specific points
            var filteredPoints = new List<MetricPoint>();
            foreach (var pt in series.Points)
            {
                if (pt.Timestamp >= start && pt.Timestamp <= end)
                {
                    filteredPoints.Add(pt);
                }
            }

            return series with { Points = filteredPoints };
        }

        public async Task DeleteAsync(DateTime beforeUtc, CancellationToken cancellationToken = default)
        {
            // For MetricSeries, individual points are stored inside a single serialized blob per metric.
            // To clean up expired series data, we fetch all series, prune points older than cutoff, and update or delete.
            var sql = "SELECT * FROM MetricSeries;";
            var allSeries = await _storageProvider.QueryAsync(sql, new Dictionary<string, object?>(), ReadRecord, cancellationToken);

            foreach (var series in allSeries)
            {
                var freshPoints = new List<MetricPoint>();
                foreach (var pt in series.Points)
                {
                    if (pt.Timestamp >= beforeUtc)
                    {
                        freshPoints.Add(pt);
                    }
                }

                if (freshPoints.Count == 0)
                {
                    var delSql = "DELETE FROM MetricSeries WHERE MetricName = $Name;";
                    await _storageProvider.ExecuteNonQueryAsync(delSql, new Dictionary<string, object?> { ["$Name"] = series.MetricName }, cancellationToken);
                }
                else if (freshPoints.Count < series.Points.Count)
                {
                    await SaveSeriesAsync(series with { Points = freshPoints }, cancellationToken);
                }
            }
        }

        private MetricSeries ReadRecord(IDataRecord r)
        {
            var name = r.GetString(r.GetOrdinal("MetricName"));
            var category = (MetricCategory)r.GetInt32(r.GetOrdinal("Category"));
            var unit = (MetricUnit)r.GetInt32(r.GetOrdinal("Unit"));

            byte[] blob;
            // Since IDataRecord does not have GetBytes/GetBlob directly in an easy non-allocating way, we can cast or use GetValue
            var obj = r.GetValue(r.GetOrdinal("Points"));
            if (obj is byte[] bytes)
            {
                blob = bytes;
            }
            else
            {
                _logger.LogWarning("Serialized points field is not a byte array. Attempting fallback.");
                blob = Array.Empty<byte>();
            }

            var points = DeserializePoints(blob);

            return new MetricSeries
            {
                MetricName = name,
                Category = category,
                Unit = unit,
                Points = points
            };
        }

        private byte[] CompressPoints(string serializedJson)
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(serializedJson);
            using var ms = new MemoryStream();

            // Write SMC magic prefix and header
            ms.Write(MagicPrefix, 0, MagicPrefix.Length);
            ms.WriteByte(FormatVersion);
            ms.WriteByte(CompressionTypeGZip);

            using (var gzip = new GZipStream(ms, CompressionMode.Compress, leaveOpen: true))
            {
                gzip.Write(utf8Bytes, 0, utf8Bytes.Length);
            }

            return ms.ToArray();
        }

        private byte[] PackUncompressedPoints(string serializedJson)
        {
            var utf8Bytes = Encoding.UTF8.GetBytes(serializedJson);
            var result = new byte[MagicPrefix.Length + 2 + utf8Bytes.Length];

            Buffer.BlockCopy(MagicPrefix, 0, result, 0, MagicPrefix.Length);
            result[MagicPrefix.Length] = FormatVersion;
            result[MagicPrefix.Length + 1] = CompressionTypeNone;
            Buffer.BlockCopy(utf8Bytes, 0, result, MagicPrefix.Length + 2, utf8Bytes.Length);

            return result;
        }

        private IReadOnlyCollection<MetricPoint> DeserializePoints(byte[] blob)
        {
            if (blob == null || blob.Length == 0)
            {
                return Array.Empty<MetricPoint>();
            }

            // Check for Sayra Metric Compression magic prefix
            bool hasMagic = blob.Length >= 5 &&
                             blob[0] == MagicPrefix[0] &&
                             blob[1] == MagicPrefix[1] &&
                             blob[2] == MagicPrefix[2];

            if (hasMagic)
            {
                byte version = blob[3];
                byte compressionType = blob[4];

                if (version == FormatVersion)
                {
                    if (compressionType == CompressionTypeGZip)
                    {
                        using var ms = new MemoryStream(blob, 5, blob.Length - 5);
                        using var gzip = new GZipStream(ms, CompressionMode.Decompress);
                        using var reader = new StreamReader(gzip, Encoding.UTF8);
                        var json = reader.ReadToEnd();
                        return JsonSerializer.Deserialize<List<MetricPoint>>(json) ?? new List<MetricPoint>();
                    }
                    else if (compressionType == CompressionTypeNone)
                    {
                        var json = Encoding.UTF8.GetString(blob, 5, blob.Length - 5);
                        return JsonSerializer.Deserialize<List<MetricPoint>>(json) ?? new List<MetricPoint>();
                    }
                }

                throw new HistoricalStorageException($"Unsupported serialization format version: {version}");
            }

            // Backward compatibility fallback: read the entire blob as raw JSON UTF-8
            try
            {
                var rawJson = Encoding.UTF8.GetString(blob);
                return JsonSerializer.Deserialize<List<MetricPoint>>(rawJson) ?? new List<MetricPoint>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize legacy backward-compatible points payload.");
                throw new HistoricalStorageException("Failed to decode metrics points blob.", ex);
            }
        }
    }
}

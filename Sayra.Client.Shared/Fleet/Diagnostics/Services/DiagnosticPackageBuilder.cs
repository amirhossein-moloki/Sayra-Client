using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Phase9;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Fleet.Diagnostics.Interfaces;

namespace Sayra.Client.Shared.Fleet.Diagnostics.Services
{
    /// <summary>
    /// Thread-safe in-memory registry allowing reports to be temporarily stored and looked up during packaging.
    /// </summary>
    public interface IDiagnosticReportRegistry
    {
        /// <summary>
        /// Registers a compiled diagnostic report.
        /// </summary>
        void RegisterReport(DiagnosticReport report);

        /// <summary>
        /// Retrieves a diagnostic report by ID.
        /// </summary>
        DiagnosticReport? GetReport(string reportId);
    }

    /// <summary>
    /// In-memory implementation of <see cref="IDiagnosticReportRegistry"/>.
    /// </summary>
    public class DiagnosticReportRegistry : IDiagnosticReportRegistry
    {
        private readonly ConcurrentDictionary<string, DiagnosticReport> _cache = new();

        /// <inheritdoc />
        public void RegisterReport(DiagnosticReport report)
        {
            if (report == null) return;
            _cache[report.ReportId] = report;
        }

        /// <inheritdoc />
        public DiagnosticReport? GetReport(string reportId)
        {
            if (string.IsNullOrWhiteSpace(reportId)) return null;
            _cache.TryGetValue(reportId, out var report);
            return report;
        }
    }

    /// <summary>
    /// Compression and serialization utility implementing <see cref="IDiagnosticPackageBuilder"/>,
    /// creating secure, hashed, and versioned diagnostic containers.
    /// </summary>
    public class DiagnosticPackageBuilder : IDiagnosticPackageBuilder
    {
        private readonly IDiagnosticStorage _storage;
        private readonly IDiagnosticReportRegistry _registry;
        private readonly ILogger<DiagnosticPackageBuilder> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="DiagnosticPackageBuilder"/> class.
        /// </summary>
        public DiagnosticPackageBuilder(
            IDiagnosticStorage storage,
            IDiagnosticReportRegistry registry,
            ILogger<DiagnosticPackageBuilder> logger)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public async Task<DiagnosticPackage> BuildPackageAsync(string machineId, IEnumerable<string> reportIds, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(machineId)) throw new ArgumentException("Machine ID cannot be null or empty.", nameof(machineId));
            if (reportIds == null) throw new ArgumentNullException(nameof(reportIds));

            _logger.LogInformation("Building diagnostic package for machine {MachineId}...", machineId);
            ct.ThrowIfCancellationRequested();

            var reportsToPack = new List<DiagnosticReport>();
            foreach (var reportId in reportIds)
            {
                var report = _registry.GetReport(reportId);
                if (report != null)
                {
                    reportsToPack.Add(report);
                }
                else
                {
                    _logger.LogWarning("Report ID {ReportId} was not found in registry. Skipping from package.", reportId);
                }
            }

            if (reportsToPack.Count == 0)
            {
                throw new InvalidOperationException("No valid diagnostic reports found to pack.");
            }

            byte[] compressedBytes;
            using (var memoryStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress, leaveOpen: true))
                {
                    using (var writer = new StreamWriter(gzipStream, Encoding.UTF8))
                    {
                        var options = new JsonSerializerOptions { WriteIndented = false };
                        await JsonSerializer.SerializeAsync(gzipStream, reportsToPack, options, ct);
                        await gzipStream.FlushAsync(ct);
                    }
                }
                compressedBytes = memoryStream.ToArray();
            }

            string hash;
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(compressedBytes);
                hash = Convert.ToHexString(hashBytes).ToLower();
            }

            var packageId = Guid.NewGuid().ToString();
            var fileName = $"diag_package_{DateTime.UtcNow:yyyyMMddHHmmss}.zip";

            await _storage.SavePackageAsync(packageId, compressedBytes, fileName, ct);

            var package = new DiagnosticPackage
            {
                PackageId = packageId,
                ArchiveFileName = $"{packageId}_{fileName}",
                SizeBytes = compressedBytes.Length,
                IntegrityHash = hash,
                SourceMachineId = machineId,
                GeneratedAtUtc = DateTime.UtcNow
            };

            _logger.LogInformation("Diagnostic package {PackageId} compiled successfully. Size: {Size} bytes. Hash: {Hash}",
                packageId, package.SizeBytes, hash);

            return package;
        }
    }
}

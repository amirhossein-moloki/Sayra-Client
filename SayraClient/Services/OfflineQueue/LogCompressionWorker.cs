using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services.OfflineQueue
{
    public class LogCompressionWorker : SupervisedBackgroundService
    {
        private readonly string _logsDir;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

        public LogCompressionWorker(ILogger<LogCompressionWorker> logger, IServiceHealthMonitor healthMonitor)
            : base(logger, healthMonitor, "LogCompressionWorker")
        {
            _logsDir = Path.Combine(AppContext.BaseDirectory, "logs");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("LogCompressionWorker started. Watching directory: {Dir}", _logsDir);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    CompressAndPruneLogs();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during log compression and pruning cycle.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("LogCompressionWorker stopped.");
        }

        public void CompressAndPruneLogs()
        {
            if (!Directory.Exists(_logsDir)) return;

            var files = Directory.GetFiles(_logsDir, "client*.log");
            var activeFile = Path.Combine(_logsDir, "client.log");

            foreach (var file in files)
            {
                // Do not compress the active log file
                if (string.Equals(file, activeFile, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    var gzPath = file + ".gz";
                    _logger.LogInformation("Compressing rotated log file: {File} -> {GzPath}", file, gzPath);

                    using (var originalFileStream = File.OpenRead(file))
                    using (var compressedFileStream = File.Create(gzPath))
                    using (var compressionStream = new GZipStream(compressedFileStream, CompressionMode.Compress))
                    {
                        originalFileStream.CopyTo(compressionStream);
                    }

                    File.Delete(file);
                    _logger.LogInformation("Rotated log file compressed and original deleted: {File}", file);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to compress rotated log file: {File}", file);
                }
            }

            // Maintain maximum of 5 files (active + compressed ones)
            var gzFiles = Directory.GetFiles(_logsDir, "client*.log.gz")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            if (gzFiles.Count > 5)
            {
                var filesToDelete = gzFiles.Skip(5).ToList();
                foreach (var f in filesToDelete)
                {
                    try
                    {
                        _logger.LogInformation("Deleting old compressed log file to satisfy retention limit of 5: {File}", f.FullName);
                        f.Delete();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to delete old compressed log file: {File}", f.FullName);
                    }
                }
            }
        }
    }
}

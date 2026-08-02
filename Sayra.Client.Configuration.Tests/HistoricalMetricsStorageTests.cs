using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Telemetry.Historical;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    public class HistoricalMetricsStorageTests : IDisposable
    {
        private readonly string _testDbPath;
        private readonly string _testArchiveDir;
        private readonly ServiceProvider _serviceProvider;

        public HistoricalMetricsStorageTests()
        {
            var uniqueId = Guid.NewGuid().ToString("N");
            _testDbPath = Path.Combine(AppContext.BaseDirectory, "Data", $"test_historical_metrics_{uniqueId}.db");
            _testArchiveDir = Path.Combine(AppContext.BaseDirectory, "Data", $"Archive_Test_{uniqueId}");

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddConsole());

            // Bind Options
            services.Configure<HistoricalStorageOptions>(options =>
            {
                options.DatabasePath = _testDbPath;
                options.UseCompression = true;
                options.PageSize = 4096;
                options.BatchSize = 100;
                options.MaxStorageSizeBytes = 104857600; // 100 MB
                options.ArchiveDirectory = _testArchiveDir;
                options.CustomRetentionHours = null;
            });

            services.Configure<RetentionOptions>(options =>
            {
                options.RetentionDays = 30;
                options.PolicyType = RetentionPolicyType.Daily;
            });

            // Register Services
            services.AddSingleton<IHistoricalStorageProvider, SqliteHistoricalStorageProvider>();
            services.AddSingleton<IHistoricalArchiveProvider, FileHistoricalArchiveProvider>();
            services.AddSingleton<IHistoricalMetricRepository, SqliteHistoricalMetricRepository>();
            services.AddSingleton<IMetricSeriesRepository, SqliteMetricSeriesRepository>();
            services.AddSingleton<IPerformanceSnapshotRepository, SqlitePerformanceSnapshotRepository>();
            services.AddSingleton<IAuditMetricRepository, SqliteAuditMetricRepository>();
            services.AddSingleton<IHistoricalMetricsService, HistoricalMetricsService>();

            _serviceProvider = services.BuildServiceProvider();
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
            try
            {
                if (File.Exists(_testDbPath))
                {
                    File.Delete(_testDbPath);
                }
                var journal = _testDbPath + "-journal";
                if (File.Exists(journal)) File.Delete(journal);
                var wal = _testDbPath + "-wal";
                if (File.Exists(wal)) File.Delete(wal);
                var shm = _testDbPath + "-shm";
                if (File.Exists(shm)) File.Delete(shm);

                if (Directory.Exists(_testArchiveDir))
                {
                    Directory.Delete(_testArchiveDir, true);
                }
            }
            catch
            {
                // Suppress clean up errors in tests
            }
        }

        [Fact]
        public async Task Test_Storage_Initialization_Creates_Database_And_Tables()
        {
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            Assert.True(File.Exists(_testDbPath));
            Assert.True(provider.GetStorageSizeBytes() > 0);
        }

        [Fact]
        public async Task Test_HistoricalMetricRepository_CRUD_And_Batch_Operations()
        {
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            var repo = _serviceProvider.GetRequiredService<IHistoricalMetricRepository>();

            var now = DateTime.UtcNow;
            var metric1 = new HistoricalMetric
            {
                Timestamp = now.AddDays(-5),
                MetricName = "CPU_Usage",
                Category = MetricCategory.Cpu,
                Unit = MetricUnit.Percent,
                AverageValue = 45.5,
                MinValue = 10.0,
                MaxValue = 99.0,
                Count = 500,
                Interval = CollectionInterval.Historical
            };

            await repo.InsertAsync(metric1);

            // Fetch and check
            var queried = await repo.QueryAsync("CPU_Usage", now.AddDays(-10), now);
            Assert.Single(queried);
            var record = queried.First();
            Assert.Equal("CPU_Usage", record.MetricName);
            Assert.Equal(45.5, record.AverageValue);

            // Batch insert
            var metricsBatch = new List<HistoricalMetric>
            {
                metric1 with { Timestamp = now.AddDays(-3), AverageValue = 50.0 },
                metric1 with { Timestamp = now.AddDays(-1), AverageValue = 60.0 }
            };

            await repo.BatchInsertAsync(metricsBatch);

            var queriedAll = await repo.QueryAsync("CPU_Usage", now.AddDays(-10), now);
            Assert.Equal(3, queriedAll.Count);

            // Expired & delete test
            var expired = await repo.GetExpiredAsync(now.AddDays(-4));
            Assert.Single(expired); // Only the -5 day record

            await repo.DeleteAsync(now.AddDays(-4));
            var remaining = await repo.QueryAsync("CPU_Usage", now.AddDays(-10), now);
            Assert.Equal(2, remaining.Count);
        }

        [Fact]
        public async Task Test_MetricSeriesRepository_With_Compression_And_Decompression()
        {
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            var repo = _serviceProvider.GetRequiredService<IMetricSeriesRepository>();

            var points = new List<MetricPoint>
            {
                new() { Timestamp = DateTime.UtcNow.AddMinutes(-10), Value = 12.5 },
                new() { Timestamp = DateTime.UtcNow.AddMinutes(-5), Value = 14.2 }
            };

            var series = new MetricSeries
            {
                MetricName = "Active_Sessions",
                Category = MetricCategory.Session,
                Unit = MetricUnit.Count,
                Points = points
            };

            await repo.SaveSeriesAsync(series);

            // Transparent decompression on Get
            var restored = await repo.GetSeriesAsync("Active_Sessions");
            Assert.NotNull(restored);
            Assert.Equal("Active_Sessions", restored.MetricName);
            Assert.Equal(2, restored.Points.Count);
            Assert.Equal(12.5, restored.Points.First().Value);

            // Test QuerySeries
            var rangeResult = await repo.QuerySeriesAsync("Active_Sessions", DateTime.UtcNow.AddMinutes(-8), DateTime.UtcNow);
            Assert.NotNull(rangeResult);
            Assert.Single(rangeResult.Points);
            Assert.Equal(14.2, rangeResult.Points.First().Value);
        }

        [Fact]
        public async Task Test_MetricSeriesRepository_Decompression_Backward_Compatibility()
        {
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            // Manually insert legacy unversioned UTF-8 JSON blob directly to database bypassing compression to simulate old data
            var rawJson = "[{\"Timestamp\":\"2024-01-01T12:00:00Z\",\"Value\":42.0,\"Tags\":{}}]";
            var rawBytes = System.Text.Encoding.UTF8.GetBytes(rawJson);

            var sql = "INSERT INTO MetricSeries (MetricName, Category, Unit, Points) VALUES ($Name, $Cat, $Unit, $Points);";
            var parameters = new Dictionary<string, object?>
            {
                ["$Name"] = "Legacy_Metric",
                ["$Cat"] = (int)MetricCategory.Cpu,
                ["$Unit"] = (int)MetricUnit.Percent,
                ["$Points"] = rawBytes
            };

            await provider.ExecuteNonQueryAsync(sql, parameters);

            // Query using repository (it should detect legacy unversioned/uncompressed BLOB and transparently fall back!)
            var repo = _serviceProvider.GetRequiredService<IMetricSeriesRepository>();
            var series = await repo.GetSeriesAsync("Legacy_Metric");

            Assert.NotNull(series);
            Assert.Equal("Legacy_Metric", series.MetricName);
            Assert.Single(series.Points);
            Assert.Equal(42.0, series.Points.First().Value);
        }

        [Fact]
        public async Task Test_PerformanceSnapshotRepository_CRUD_Operations()
        {
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            var repo = _serviceProvider.GetRequiredService<IPerformanceSnapshotRepository>();

            var snapshot = new PerformanceSnapshot
            {
                Timestamp = DateTime.UtcNow.AddMinutes(-20),
                StartupTime = TimeSpan.FromSeconds(2.5),
                AuthenticationTime = TimeSpan.FromMilliseconds(450),
                DatabaseLatency = TimeSpan.FromMilliseconds(5),
                IpcLatency = TimeSpan.FromMilliseconds(2),
                TcpLatency = TimeSpan.FromMilliseconds(80),
                DownloadSpeed = 10485760.0,
                UploadSpeed = 5242880.0,
                DiskLatency = TimeSpan.FromMilliseconds(12),
                CacheHitRatio = 0.95,
                QueueLength = 1,
                WorkerExecutionTime = TimeSpan.FromMilliseconds(15),
                GarbageCollectionCount = 12,
                ThreadPoolThreads = 4,
                AsyncOperationsCount = 10,
                MachineId = "WORKSTATION_01",
                Subsystem = "Telemetry",
                Operation = "Collect",
                Status = "Success",
                TraceId = "trace_01",
                CorrelationId = "corr_01",
                Duration = TimeSpan.FromMilliseconds(50)
            };

            await repo.InsertAsync(snapshot);

            // Range Query
            var results = await repo.QueryAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow, "Telemetry", "WORKSTATION_01", "corr_01");
            Assert.Single(results);
            var rec = results.First();
            Assert.Equal("trace_01", rec.TraceId);
            Assert.Equal(12, rec.GarbageCollectionCount);
            Assert.Equal(2.5, rec.StartupTime.TotalSeconds);
        }

        [Fact]
        public async Task Test_AuditMetricRepository_CRUD_Operations()
        {
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            var repo = _serviceProvider.GetRequiredService<IAuditMetricRepository>();

            var audit = new AuditMetric
            {
                AuditId = "audit_unique_01",
                Timestamp = DateTime.UtcNow.AddHours(-12),
                Name = "GameLaunch",
                MachineId = "HOST-X",
                SessionId = "session_99",
                UserId = "user_abc",
                OperatorId = "admin_01",
                Details = "{ \"GameId\": \"CSGO\", \"Action\": \"Launch\" }",
                Count = 1,
                Duration = TimeSpan.FromSeconds(3)
            };

            await repo.InsertAsync(audit);

            var queried = await repo.QueryAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, "GameLaunch", "HOST-X", "session_99");
            Assert.Single(queried);
            Assert.Equal("user_abc", queried.First().UserId);
            Assert.Equal("admin_01", queried.First().OperatorId);
        }

        [Fact]
        public async Task Test_HistoricalArchiveProvider_Archive_Restore_Validation()
        {
            var archiveProvider = _serviceProvider.GetRequiredService<IHistoricalArchiveProvider>();

            var metrics = new List<HistoricalMetric>
            {
                new() { Timestamp = DateTime.UtcNow.AddDays(-1), MetricName = "RAM_Usage", AverageValue = 8.4 },
                new() { Timestamp = DateTime.UtcNow, MetricName = "RAM_Usage", AverageValue = 12.1 }
            };

            var archivePath = Path.Combine(_testArchiveDir, "test_archive.json");

            // Archive
            await archiveProvider.ArchiveAsync(archivePath, metrics);
            Assert.True(File.Exists(archivePath));

            // Validate
            bool isValid = await archiveProvider.ValidateArchiveAsync(archivePath);
            Assert.True(isValid);

            // Metadata
            var metadata = await archiveProvider.GetArchiveMetadataAsync(archivePath);
            Assert.Equal("1.0", metadata["ArchiveVersion"]);
            Assert.Equal("2", metadata["MetricCount"]);
            Assert.True(metadata.ContainsKey("Sha256Checksum"));

            // Restore
            var restored = await archiveProvider.RestoreAsync(archivePath);
            Assert.Equal(2, restored.Count);
            Assert.Equal(8.4, restored.First().AverageValue);

            // Tampering test - modify file contents to force integrity failure
            var text = File.ReadAllText(archivePath);
            var tamperedText = text.Replace("12.1", "999.9");
            File.WriteAllText(archivePath, tamperedText);

            // Validation should fail
            bool isValidAfterTamper = await archiveProvider.ValidateArchiveAsync(archivePath);
            Assert.False(isValidAfterTamper);

            // Restore should throw due to integrity violation
            await Assert.ThrowsAsync<HistoricalStorageException>(() => archiveProvider.RestoreAsync(archivePath));
        }

        [Fact]
        public async Task Test_HistoricalMetricsService_RetentionPolicies_Prunes_Database_And_Archives()
        {
            var service = _serviceProvider.GetRequiredService<IHistoricalMetricsService>();
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            var metricRepo = _serviceProvider.GetRequiredService<IHistoricalMetricRepository>();
            var now = DateTime.UtcNow;

            // Save active records and expired records
            var fresh = new HistoricalMetric { Timestamp = now.AddDays(-1), MetricName = "HDD_Space", AverageValue = 90.0, Count = 1 };
            var expired = new HistoricalMetric { Timestamp = now.AddDays(-40), MetricName = "HDD_Space", AverageValue = 30.0, Count = 1 };

            await metricRepo.InsertAsync(fresh);
            await metricRepo.InsertAsync(expired);

            // Run retention (Daily policy, 30 days retention cutoff)
            await ((HistoricalMetricsService)service).ExecuteRetentionPoliciesAsync();

            // Verify expired was archived and pruned, while fresh remains
            var queryRemaining = await metricRepo.QueryAsync("HDD_Space", now.AddDays(-100), now);
            Assert.Single(queryRemaining);
            Assert.Equal(90.0, queryRemaining.First().AverageValue);

            // Verify archive file was generated in the test directory
            var archiveFiles = Directory.GetFiles(_testArchiveDir, "*.json");
            Assert.Single(archiveFiles);
        }

        [Fact]
        public async Task Test_HistoricalMetricsService_LinearRegression_CapacityForecasting()
        {
            var service = _serviceProvider.GetRequiredService<IHistoricalMetricsService>();
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            var metricRepo = _serviceProvider.GetRequiredService<IHistoricalMetricRepository>();
            var now = DateTime.UtcNow;

            // Inject 5 days of steadily increasing memory usage metrics to construct a clear linear trend line
            for (int i = 5; i >= 1; i--)
            {
                var metric = new HistoricalMetric
                {
                    Timestamp = now.AddDays(-i),
                    MetricName = "Memory_Usage",
                    Category = MetricCategory.Memory,
                    Unit = MetricUnit.Percent,
                    AverageValue = 50.0 + (5 - i) * 5.0, // 50.0, 55.0, 60.0, 65.0, 70.0
                    Count = 1
                };
                await metricRepo.InsertAsync(metric);
            }

            // Forecast 10 days into the future
            var forecast = await service.ForecastCapacityAsync("Memory_Usage", 10);

            Assert.NotNull(forecast);
            Assert.Equal("Memory_Usage", forecast.MetricName);
            Assert.Equal(70.0, forecast.CurrentUsage);
            // Projection should continue the upward trend of +5 per day
            Assert.True(forecast.ForecastedUsage > forecast.CurrentUsage);
            Assert.Equal(0.85, forecast.ConfidenceLevel);
            Assert.Contains("Memory_Usage", forecast.Recommendation);
        }

        [Fact]
        public async Task Test_HistoricalMetricsStorage_Concurrency_Stress_Safety()
        {
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            var metricRepo = _serviceProvider.GetRequiredService<IHistoricalMetricRepository>();

            var tasks = new List<Task>();
            int threadCount = 10;
            int insertsPerThread = 20;

            for (int t = 0; t < threadCount; t++)
            {
                var threadId = t;
                tasks.Add(Task.Run(async () =>
                {
                    for (int i = 0; i < insertsPerThread; i++)
                    {
                        var metric = new HistoricalMetric
                        {
                            Timestamp = DateTime.UtcNow,
                            MetricName = $"ThreadMetric_{threadId}",
                            Category = MetricCategory.Cpu,
                            Unit = MetricUnit.Count,
                            AverageValue = i,
                            Count = 1
                        };
                        await metricRepo.InsertAsync(metric);
                    }
                }));
            }

            // Wait for all concurrent inserts to complete. If writer locking fails, SQLite will raise lock-contention exceptions.
            await Task.WhenAll(tasks);

            // Assert everything was saved successfully
            for (int t = 0; t < threadCount; t++)
            {
                var list = await metricRepo.QueryAsync($"ThreadMetric_{t}", DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow);
                Assert.Equal(insertsPerThread, list.Count);
            }
        }

        [Fact]
        public async Task Test_HistoricalMetricsStorage_Cancellation_Token_Handling()
        {
            var provider = _serviceProvider.GetRequiredService<IHistoricalStorageProvider>();
            await provider.InitializeAsync();

            var repo = _serviceProvider.GetRequiredService<IHistoricalMetricRepository>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var metric = new HistoricalMetric
            {
                Timestamp = DateTime.UtcNow,
                MetricName = "CancellationMetric",
                Count = 1
            };

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.InsertAsync(metric, cts.Token));
        }
    }
}

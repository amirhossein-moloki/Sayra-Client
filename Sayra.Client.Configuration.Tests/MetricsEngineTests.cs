using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Exceptions;
using Sayra.Client.Shared.Models.Telemetry.Options;
using Sayra.Client.Shared.Telemetry;
using Sayra.Client.Shared.Telemetry.Metrics;
using Xunit;

namespace Sayra.Client.Configuration.Tests
{
    /// <summary>
    /// High-rigor test suite validating all components of Phase 8 Stage 3 (Enterprise Metrics Engine).
    /// Covers Counters, Gauges, Histograms, Timers, Rates, percentiles, downsampling, moving averages,
    /// validation, concurrency, thread-safety, and DI.
    /// </summary>
    public class MetricsEngineTests
    {
        private readonly NullLogger<TelemetryPipeline> _nullPipelineLogger = NullLogger<TelemetryPipeline>.Instance;
        private readonly NullLogger<MetricsAggregator> _nullAggregatorLogger = NullLogger<MetricsAggregator>.Instance;

        [Fact]
        public void MetricsMath_CalculatesBasicStatistics_Correctly()
        {
            var values = new List<double> { 10.0, 20.0, 30.0, 40.0, 50.0 };

            Assert.Equal(10.0, MetricsMath.CalculateMin(values));
            Assert.Equal(50.0, MetricsMath.CalculateMax(values));
            Assert.Equal(150.0, MetricsMath.CalculateSum(values));
            Assert.Equal(30.0, MetricsMath.CalculateAverage(values));
            Assert.Equal(250.0, MetricsMath.CalculateVariance(values, isSample: true));
            Assert.Equal(Math.Sqrt(250.0), MetricsMath.CalculateStandardDeviation(values, isSample: true));
        }

        [Fact]
        public void MetricsMath_CalculatesPercentiles_Correctly()
        {
            // Odd number of elements
            var values = new List<double> { 15.0, 20.0, 35.0, 40.0, 50.0 };

            Assert.Equal(35.0, MetricsMath.CalculatePercentile(values, 50.0)); // Median
            Assert.Equal(15.0, MetricsMath.CalculatePercentile(values, 0.0));
            Assert.Equal(50.0, MetricsMath.CalculatePercentile(values, 100.0));

            // Precise interpolation verification
            // Rank for 90th percentile of 5 items is 0.9 * 4 = 3.6
            // Lower index 3 (value 40.0), Upper index 4 (value 50.0)
            // Interpolated value: 40.0 + 0.6 * (50.0 - 40.0) = 46.0
            Assert.Equal(46.0, MetricsMath.CalculatePercentile(values, 90.0));
        }

        [Fact]
        public void MetricsMath_CalculatesRollingAverages_Correctly()
        {
            var values = new List<double> { 10.0, 20.0, 30.0, 40.0, 50.0 };
            var sma = MetricsMath.CalculateRollingAverages(values, 3);

            Assert.Equal(5, sma.Count);
            Assert.Equal(10.0, sma[0]); // Avg of [10]
            Assert.Equal(15.0, sma[1]); // Avg of [10, 20]
            Assert.Equal(20.0, sma[2]); // Avg of [10, 20, 30]
            Assert.Equal(30.0, sma[3]); // Avg of [20, 30, 40]
            Assert.Equal(40.0, sma[4]); // Avg of [30, 40, 50]
        }

        [Fact]
        public void MetricsMath_CalculatesExponentialMovingAverages_Correctly()
        {
            var values = new List<double> { 10.0, 20.0, 30.0 };
            var ema = MetricsMath.CalculateExponentialMovingAverages(values, 0.2);

            Assert.Equal(3, ema.Count);
            Assert.Equal(10.0, ema[0]); // First is original
            Assert.Equal(0.2 * 20.0 + 0.8 * 10.0, ema[1]); // 12.0
            Assert.Equal(0.2 * 30.0 + 0.8 * 12.0, ema[2]); // 15.6
        }

        [Fact]
        public void MetricValidator_RejectsInvalidSamples_WithHighRigor()
        {
            var validator = new MetricValidator(NullLogger<MetricValidator>.Instance);

            // 1. Invalid characters in Metric Name
            var r1 = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                MetricName = "cpu usage; DROP TABLE Users;",
                Value = 10.0,
                Category = MetricCategory.Cpu,
                Unit = MetricUnit.Percent
            };
            Assert.False(validator.Validate(r1, out _));

            // 2. Out of range percentage
            var r2 = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                MetricName = "system.cpu.usage",
                Value = 112.5,
                Category = MetricCategory.Cpu,
                Unit = MetricUnit.Percent
            };
            Assert.False(validator.Validate(r2, out _));

            // 3. Negative memory speed
            var r3 = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow,
                MetricName = "app.download.speed",
                Value = -500.0,
                Category = MetricCategory.Download,
                Unit = MetricUnit.Bytes
            };
            Assert.False(validator.Validate(r3, out _));

            // 4. Future timestamp
            var r4 = new TelemetryRecord
            {
                Timestamp = DateTime.UtcNow.AddDays(2),
                MetricName = "system.cpu.usage",
                Value = 45.0,
                Category = MetricCategory.Cpu,
                Unit = MetricUnit.Percent
            };
            Assert.False(validator.Validate(r4, out _));
        }

        [Fact]
        public void MetricValidator_RejectsDuplicateSamples()
        {
            var validator = new MetricValidator(NullLogger<MetricValidator>.Instance);
            var timestamp = DateTime.UtcNow;

            var r1 = new TelemetryRecord
            {
                Timestamp = timestamp,
                MetricName = "system.cpu.usage",
                Value = 45.0,
                Category = MetricCategory.Cpu,
                Unit = MetricUnit.Percent
            };

            var r2 = r1 with { Value = 55.0 }; // Duplicate timestamp and name

            var batch = new List<TelemetryRecord> { r1, r2 };
            var cleaned = validator.FilterAndCleanBatch(batch);

            Assert.Single(cleaned);
            Assert.Equal(45.0, cleaned[0].Value);
        }

        [Fact]
        public void MetricDownsampler_ConsolidatesPoints_Correctly()
        {
            var points = new List<MetricPoint>
            {
                new() { Timestamp = DateTime.UtcNow, Value = 10.0 },
                new() { Timestamp = DateTime.UtcNow, Value = 30.0 },
                new() { Timestamp = DateTime.UtcNow, Value = 20.0 }
            };

            var consolidatedMin = MetricDownsampler.Downsample(points, "Minimum", DateTime.UtcNow);
            Assert.Equal(10.0, consolidatedMin.Value);

            var consolidatedMax = MetricDownsampler.Downsample(points, "Maximum", DateTime.UtcNow);
            Assert.Equal(30.0, consolidatedMax.Value);

            var consolidatedSum = MetricDownsampler.Downsample(points, "Sum", DateTime.UtcNow);
            Assert.Equal(60.0, consolidatedSum.Value);

            var consolidatedLast = MetricDownsampler.Downsample(points, "LastValue", DateTime.UtcNow);
            Assert.Equal(20.0, consolidatedLast.Value);

            var consolidatedAvg = MetricDownsampler.Downsample(points, "Average", DateTime.UtcNow);
            Assert.Equal(20.0, consolidatedAvg.Value);
        }

        [Fact]
        public async Task MetricsAggregator_AggregatesAllTypes_Correctly()
        {
            var pipeline = new TelemetryPipeline(_nullPipelineLogger);
            var options = Options.Create(new MetricsOptions
            {
                AggregationWindowSeconds = 5,
                ConfiguredWindowsSeconds = new List<int> { 5 },
                EnableMovingAverages = true
            });

            var aggregator = new MetricsAggregator(pipeline, options, _nullAggregatorLogger);

            // Seed raw telemetry records belonging to the same 5-second window
            var now = DateTime.UtcNow;
            long unixSeconds = ((DateTimeOffset)now).ToUnixTimeSeconds();
            long roundedUnix = (unixSeconds / 5) * 5;
            DateTime windowStart = DateTimeOffset.FromUnixTimeSeconds(roundedUnix).UtcDateTime;

            // 1. Gauge Type
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(1),
                MetricName = "system.cpu.usage",
                Category = MetricCategory.Cpu,
                Value = 40.0,
                Unit = MetricUnit.Percent
            });
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(3),
                MetricName = "system.cpu.usage",
                Category = MetricCategory.Cpu,
                Value = 60.0,
                Unit = MetricUnit.Percent
            });

            // 2. Counter Type
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(1),
                MetricName = "app.error.count",
                Category = MetricCategory.Database,
                Value = 5.0,
                Unit = MetricUnit.Count
            });
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(3),
                MetricName = "app.error.count",
                Category = MetricCategory.Database,
                Value = 12.0,
                Unit = MetricUnit.Count
            });

            // 3. Timer Type
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(1),
                MetricName = "app.database.latency",
                Category = MetricCategory.Database,
                Value = 20.0,
                Unit = MetricUnit.Milliseconds
            });
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(2),
                MetricName = "app.database.latency",
                Category = MetricCategory.Database,
                Value = 40.0,
                Unit = MetricUnit.Milliseconds
            });

            // 4. Rate Type
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(1),
                MetricName = "app.download.speed",
                Category = MetricCategory.Download,
                Value = 1000.0,
                Unit = MetricUnit.Bytes
            });
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(2),
                MetricName = "app.download.speed",
                Category = MetricCategory.Download,
                Value = 3000.0,
                Unit = MetricUnit.Bytes
            });

            // 5. Histogram Type
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(1),
                MetricName = "custom.distribution",
                Category = MetricCategory.Process,
                Value = 10.0,
                Unit = MetricUnit.Count
            });
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(2),
                MetricName = "custom.distribution",
                Category = MetricCategory.Process,
                Value = 20.0,
                Unit = MetricUnit.Count
            });
            pipeline.ProcessAndQueue(new TelemetryRecord
            {
                Timestamp = windowStart.AddSeconds(3),
                MetricName = "custom.distribution",
                Category = MetricCategory.Process,
                Value = 30.0,
                Unit = MetricUnit.Count
            });

            // Execute aggregation cycle
            await aggregator.AggregateMetricsAsync(CancellationToken.None);

            // Retrieve aggregated series
            var cpuSeries = await aggregator.GetAggregatedSeriesAsync("system.cpu.usage");
            var errorSeries = await aggregator.GetAggregatedSeriesAsync("app.error.count");
            var latencySeries = await aggregator.GetAggregatedSeriesAsync("app.database.latency");
            var downloadSeries = await aggregator.GetAggregatedSeriesAsync("app.download.speed");
            var distSeries = await aggregator.GetAggregatedSeriesAsync("custom.distribution");

            // Verify Gauges: latest value
            Assert.NotEmpty(cpuSeries.Points);
            var cpuPoint = cpuSeries.Points.First();
            Assert.Equal(60.0, cpuPoint.Value);
            Assert.Equal("60.00", cpuPoint.Tags["max"]);
            Assert.Equal("40.00", cpuPoint.Tags["min"]);

            // Verify Counters: sum
            Assert.NotEmpty(errorSeries.Points);
            var errorPoint = errorSeries.Points.First();
            Assert.Equal(17.0, errorPoint.Value); // 5 + 12 = 17

            // Verify Timers: average
            Assert.NotEmpty(latencySeries.Points);
            var latencyPoint = latencySeries.Points.First();
            Assert.Equal(30.0, latencyPoint.Value); // (20 + 40)/2 = 30
            Assert.Equal("30.00", latencyPoint.Tags["p50_ms"]);

            // Verify Rates: throughput per second
            Assert.NotEmpty(downloadSeries.Points);
            var downloadPoint = downloadSeries.Points.First();
            // sum = 4000.0, window = 5 seconds -> rate = 4000/5 = 800.0 bytes/sec
            Assert.Equal(800.0, downloadPoint.Value);

            // Verify Histograms: percentiles and average
            Assert.NotEmpty(distSeries.Points);
            var distPoint = distSeries.Points.First();
            Assert.Equal(20.0, distPoint.Value); // Average
            Assert.Equal("20.00", distPoint.Tags["p50"]); // Median of [10, 20, 30] is 20
            Assert.Equal("28.00", distPoint.Tags["p90"]); // Linear interpolation of 90th percentile of [10, 20, 30]
        }

        [Fact]
        public async Task MetricsAggregator_ThreadSafetyAndHighConcurrency_Succeeds()
        {
            var pipeline = new TelemetryPipeline(_nullPipelineLogger);
            var options = Options.Create(new MetricsOptions
            {
                AggregationWindowSeconds = 5,
                ConfiguredWindowsSeconds = new List<int> { 5 },
                EnableMovingAverages = true
            });

            var aggregator = new MetricsAggregator(pipeline, options, _nullAggregatorLogger);
            var timestamp = DateTime.UtcNow;

            int tasksCount = 10;
            int recordsPerTask = 50;

            var tasks = new List<Task>();
            for (int t = 0; t < tasksCount; t++)
            {
                int taskId = t;
                tasks.Add(Task.Run(() =>
                {
                    for (int i = 0; i < recordsPerTask; i++)
                    {
                        pipeline.ProcessAndQueue(new TelemetryRecord
                        {
                            Timestamp = timestamp.AddMilliseconds(taskId * 10 + i),
                            MetricName = "concurrency.test.metric",
                            Category = MetricCategory.Cpu,
                            Value = 1.0,
                            Unit = MetricUnit.Count
                        });
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Execute aggregation
            await aggregator.AggregateMetricsAsync(CancellationToken.None);

            var series = await aggregator.GetAggregatedSeriesAsync("concurrency.test.metric");
            Assert.NotEmpty(series.Points);
            Assert.True(series.Points.First().Value > 0.0);
        }

        [Fact]
        public void DependencyInjection_RegistersMetricsAggregatorCorrectly()
        {
            var services = new ServiceCollection();
            var configData = new Dictionary<string, string?>
            {
                { "Observability:Metrics:AggregationWindowSeconds", "15" },
                { "Observability:Metrics:DownsamplingStrategy", "Maximum" }
            };

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData)
                .Build();

            services.AddObservabilityServices(configuration);
            services.AddLogging();

            var provider = services.BuildServiceProvider();

            var aggregator = provider.GetService<IMetricsAggregator>();
            var strategies = provider.GetServices<IMetricAggregatorStrategy>().ToList();

            Assert.NotNull(aggregator);
            Assert.True(aggregator is MetricsAggregator);
            Assert.Equal(5, strategies.Count);
            Assert.Contains(strategies, s => s is CounterAggregatorStrategy);
            Assert.Contains(strategies, s => s is GaugeAggregatorStrategy);
            Assert.Contains(strategies, s => s is HistogramAggregatorStrategy);
            Assert.Contains(strategies, s => s is TimerAggregatorStrategy);
            Assert.Contains(strategies, s => s is RateAggregatorStrategy);
        }
    }
}

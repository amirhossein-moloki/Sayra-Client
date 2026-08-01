using System;
using Sayra.Client.Shared.Interfaces.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Performance
{
    /// <summary>
    /// Monitors application startup performance.
    /// Tracks application startup duration, service initialization,
    /// Dependency Injection (DI) initialization, background worker startup, and WPF shell startup.
    /// </summary>
    public class StartupPerformanceMonitor
    {
        private readonly IPerformanceMonitor _performanceMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartupPerformanceMonitor"/> class.
        /// </summary>
        public StartupPerformanceMonitor(IPerformanceMonitor performanceMonitor)
        {
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Starts a measurement for tracking a specific startup stage.
        /// </summary>
        /// <param name="stageName">The name of the startup stage (e.g. "Application", "ServiceInitialization", "DependencyInjection", "BackgroundWorker", "WpfShell").</param>
        /// <returns>A performance measurement scope.</returns>
        public IPerformanceMeasurement TrackStage(string stageName)
        {
            if (string.IsNullOrWhiteSpace(stageName))
            {
                throw new ArgumentException("Stage name cannot be null or whitespace.", nameof(stageName));
            }
            return _performanceMonitor.StartMeasurement($"Startup.{stageName}");
        }

        /// <summary>
        /// Records a specific startup stage duration directly.
        /// </summary>
        /// <param name="stageName">The name of the startup stage.</param>
        /// <param name="duration">The duration taken by the stage.</param>
        public void RecordStage(string stageName, TimeSpan duration)
        {
            if (string.IsNullOrWhiteSpace(stageName)) return;

            if (_performanceMonitor is PerformanceMonitor pm)
            {
                pm.RecordStartupStage(stageName, duration);
            }
        }
    }
}

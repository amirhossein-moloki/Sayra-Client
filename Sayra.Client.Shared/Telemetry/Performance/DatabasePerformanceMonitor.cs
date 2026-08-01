using System;
using Sayra.Client.Shared.Interfaces.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Performance
{
    /// <summary>
    /// Performance monitoring wrapper for database operations.
    /// Observes query execution duration, connection latency, transaction duration, and failures.
    /// </summary>
    public class DatabasePerformanceMonitor
    {
        private readonly IPerformanceMonitor _performanceMonitor;

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabasePerformanceMonitor"/> class.
        /// </summary>
        public DatabasePerformanceMonitor(IPerformanceMonitor performanceMonitor)
        {
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        }

        /// <summary>
        /// Starts a measurement for tracking database query execution duration.
        /// </summary>
        public IPerformanceMeasurement TrackQuery(string queryName)
        {
            return _performanceMonitor.StartMeasurement($"Database.Query:{queryName}");
        }

        /// <summary>
        /// Starts a measurement for tracking database connection establishment latency.
        /// </summary>
        public IPerformanceMeasurement TrackConnection()
        {
            return _performanceMonitor.StartMeasurement("Database.Connection");
        }

        /// <summary>
        /// Starts a measurement for tracking database transaction execution duration.
        /// </summary>
        public IPerformanceMeasurement TrackTransaction()
        {
            return _performanceMonitor.StartMeasurement("Database.Transaction");
        }
    }
}

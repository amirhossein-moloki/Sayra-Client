using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;

namespace Sayra.Client.Shared.Telemetry.Metrics
{
    /// <summary>
    /// Validates raw telemetry records to ensure mathematical correctness,
    /// bounded value ranges, realistic timestamps, and compliance with naming conventions.
    /// </summary>
    public class MetricValidator
    {
        private static readonly Regex MetricNameRegex = new(@"^[a-zA-Z0-9_\-\.]+$", RegexOptions.Compiled);
        private readonly ILogger _logger;

        public MetricValidator(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Validates a single telemetry record against enterprise metric rules.
        /// </summary>
        /// <param name="record">The record to validate.</param>
        /// <param name="errorMessage">The error message if validation fails.</param>
        /// <returns>True if valid; otherwise false.</returns>
        public bool Validate(TelemetryRecord record, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (record == null)
            {
                errorMessage = "Record is null";
                return false;
            }

            // 1. Validate Metric Name
            if (string.IsNullOrWhiteSpace(record.MetricName))
            {
                errorMessage = "Metric name is empty or whitespace";
                return false;
            }

            if (!MetricNameRegex.IsMatch(record.MetricName))
            {
                errorMessage = $"Metric name '{record.MetricName}' contains illegal characters. Only alphanumeric, dots, underscores, and dashes are allowed.";
                return false;
            }

            // 2. Validate Timestamp
            if (record.Timestamp == default || record.Timestamp == DateTime.MinValue)
            {
                errorMessage = "Timestamp is default or unassigned";
                return false;
            }

            var now = DateTime.UtcNow;
            if (record.Timestamp > now.AddDays(1))
            {
                errorMessage = $"Timestamp '{record.Timestamp}' is too far in the future";
                return false;
            }

            if (record.Timestamp < now.AddDays(-30))
            {
                errorMessage = $"Timestamp '{record.Timestamp}' is too far in the past";
                return false;
            }

            // 3. Validate Missing / Infinite values
            if (double.IsNaN(record.Value) || double.IsInfinity(record.Value))
            {
                errorMessage = "Value is missing, NaN, or infinite";
                return false;
            }

            // 4. Validate Enums
            if (!Enum.IsDefined(typeof(MetricCategory), record.Category))
            {
                errorMessage = $"Invalid metric category enum: {record.Category}";
                return false;
            }

            if (!Enum.IsDefined(typeof(MetricUnit), record.Unit))
            {
                errorMessage = $"Invalid metric unit enum: {record.Unit}";
                return false;
            }

            // 5. Validate Value Ranges based on unit and name
            string lowerName = record.MetricName.ToLowerInvariant();

            // Percentage boundaries
            if (record.Unit == MetricUnit.Percent || lowerName.Contains("usage") || lowerName.Contains("percent"))
            {
                if (record.Value < 0.0 || record.Value > 100.0)
                {
                    errorMessage = $"Percentage value '{record.Value}' is out of range [0, 100]";
                    return false;
                }
            }

            // Non-negativity constraint for counts, durations, and rates
            if (record.Unit == MetricUnit.Count || record.Unit == MetricUnit.Milliseconds ||
                record.Unit == MetricUnit.Seconds || record.Unit == MetricUnit.Rate ||
                record.Unit == MetricUnit.Bytes || record.Unit == MetricUnit.Megabytes ||
                record.Unit == MetricUnit.Gigabytes)
            {
                if (record.Value < 0.0)
                {
                    errorMessage = $"Metric value '{record.Value}' under unit '{record.Unit}' cannot be negative";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Filters a batch of telemetry records, checking for duplicates based on name, timestamp, and values.
        /// </summary>
        /// <param name="records">The batch of records to filter.</param>
        /// <returns>A clean list of validated, non-duplicate records.</returns>
        public IReadOnlyList<TelemetryRecord> FilterAndCleanBatch(IEnumerable<TelemetryRecord> records)
        {
            var cleanBatch = new List<TelemetryRecord>();
            var seenSamples = new HashSet<(string Name, DateTime Timestamp)>();

            foreach (var record in records)
            {
                if (!Validate(record, out string error))
                {
                    _logger.LogWarning("Record rejected during metrics validation. Error: {Error}. Record: {@Record}", error, record);
                    continue;
                }

                // Check for duplicate samples (identical name and timestamp)
                var key = (record.MetricName.ToLowerInvariant(), record.Timestamp);
                if (seenSamples.Contains(key))
                {
                    _logger.LogWarning("Duplicate sample detected and rejected: Metric={MetricName}, Timestamp={Timestamp}, Value={Value}",
                        record.MetricName, record.Timestamp, record.Value);
                    continue;
                }

                seenSamples.Add(key);
                cleanBatch.Add(record);
            }

            return cleanBatch;
        }
    }
}

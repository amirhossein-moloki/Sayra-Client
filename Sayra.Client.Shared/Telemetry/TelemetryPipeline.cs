using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.ValueObjects;

namespace Sayra.Client.Shared.Telemetry
{
    /// <summary>
    /// Thread-safe, high-frequency pipeline that processes, validates, normalizes,
    /// and enriches workstation telemetry records before routing to a Channel.
    /// </summary>
    public class TelemetryPipeline
    {
        private readonly ILogger<TelemetryPipeline> _logger;
        private readonly Channel<TelemetryRecord> _outputChannel;

        /// <summary>
        /// Gets the reader side of the processed telemetry output channel.
        /// </summary>
        public ChannelReader<TelemetryRecord> Reader => _outputChannel.Reader;

        public TelemetryPipeline(ILogger<TelemetryPipeline> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Use an unbounded channel with SingleReader option for high-performance non-blocking writes
            _outputChannel = Channel.CreateUnbounded<TelemetryRecord>(new UnboundedChannelOptions
            {
                SingleReader = true,
                AllowSynchronousContinuations = false
            });
        }

        /// <summary>
        /// Processes a record through validation, normalization, and tag enrichment.
        /// If valid, the record is routed to the output channel.
        /// </summary>
        /// <param name="record">The incoming telemetry record.</param>
        /// <returns>True if the record was successfully processed and queued; false if rejected.</returns>
        public bool ProcessAndQueue(TelemetryRecord record)
        {
            if (record == null) return false;

            // 1. Validation
            if (!ValidateRecord(record, out string validationError))
            {
                _logger.LogWarning("Telemetry record rejected by validation: {Error}. Record: {@Record}", validationError, record);
                return false;
            }

            // 2. Normalization & 3. Tag Enrichment
            TelemetryRecord processedRecord = NormalizeAndEnrich(record);

            // 4. Output Channel
            if (_outputChannel.Writer.TryWrite(processedRecord))
            {
                return true;
            }

            _logger.LogError("Failed to write record to the telemetry channel.");
            return false;
        }

        private bool ValidateRecord(TelemetryRecord record, out string errorMessage)
        {
            errorMessage = string.Empty;

            if (record.Timestamp == default || record.Timestamp == DateTime.MinValue)
            {
                errorMessage = "Invalid timestamp";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.MachineId))
            {
                errorMessage = "MachineId cannot be null or empty";
                return false;
            }

            if (string.IsNullOrWhiteSpace(record.MetricName))
            {
                errorMessage = "MetricName cannot be null or empty";
                return false;
            }

            if (double.IsNaN(record.Value) || double.IsInfinity(record.Value))
            {
                errorMessage = "Value must be a valid finite number";
                return false;
            }

            // Ensure enums are within valid defined bounds
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

            if (!Enum.IsDefined(typeof(MetricSeverity), record.Severity))
            {
                errorMessage = $"Invalid metric severity enum: {record.Severity}";
                return false;
            }

            return true;
        }

        private TelemetryRecord NormalizeAndEnrich(TelemetryRecord original)
        {
            // Normalize values
            string normalizedMetricName = original.MetricName.Trim().ToLowerInvariant();
            string normalizedMachineId = original.MachineId.Trim().ToUpperInvariant();
            double normalizedValue = Math.Round(original.Value, 2);

            // Tag Enrichment: Combine existing tags with global environmental metadata tags
            var enrichedTags = new Dictionary<string, string>(original.Tags ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase)
            {
                ["env"] = "Production",
                ["os_platform"] = OperatingSystem.IsWindows() ? "Windows" : "Unix",
                ["app_version"] = "2.0.0"
            };

            // Enforce a CorrelationId if missing
            CorrelationId finalCorrelationId = original.CorrelationId ?? new CorrelationId();

            return original with
            {
                MetricName = normalizedMetricName,
                MachineId = normalizedMachineId,
                Value = normalizedValue,
                Tags = enrichedTags,
                CorrelationId = finalCorrelationId
            };
        }
    }
}

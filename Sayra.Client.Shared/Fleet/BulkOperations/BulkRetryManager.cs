using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.BulkOperations
{
    /// <summary>
    /// Retry Policy Engine and failure classifier for enterprise bulk operations.
    /// Supports both automatic retries with exponential backoff and manual operator retry triggers.
    /// </summary>
    public class BulkRetryManager
    {
        private readonly ConcurrentDictionary<string, int> _manualRetriesCount = new();

        /// <summary>
        /// Classifies an execution failure into standard BulkFailureType categories.
        /// </summary>
        public BulkFailureType ClassifyFailure(OperationResult result, string errorMessage)
        {
            if (result == OperationResult.Timeout)
            {
                return BulkFailureType.Timeout;
            }

            if (result == OperationResult.SecurityError || result == OperationResult.ValidationError)
            {
                return BulkFailureType.PermissionFailure;
            }

            var msg = errorMessage ?? string.Empty;
            if (msg.Contains("offline", StringComparison.OrdinalIgnoreCase) || msg.Contains("unreachable", StringComparison.OrdinalIgnoreCase))
            {
                return BulkFailureType.MachineOffline;
            }

            if (msg.Contains("network", StringComparison.OrdinalIgnoreCase) || msg.Contains("socket", StringComparison.OrdinalIgnoreCase) || msg.Contains("connection", StringComparison.OrdinalIgnoreCase))
            {
                return BulkFailureType.NetworkFailure;
            }

            return BulkFailureType.UnknownFailure;
        }

        /// <summary>
        /// Evaluates whether a given failure type qualifies for automatic retries.
        /// </summary>
        public bool IsTransient(BulkFailureType failureType)
        {
            return failureType == BulkFailureType.NetworkFailure ||
                   failureType == BulkFailureType.Timeout ||
                   failureType == BulkFailureType.UnknownFailure;
        }

        /// <summary>
        /// Calculates exponential backoff delay based on the attempt number.
        /// </summary>
        public TimeSpan CalculateBackoff(int attempt, TimeSpan baseDelay)
        {
            if (attempt <= 0) return TimeSpan.Zero;
            var factor = Math.Pow(2, attempt - 1);
            var delayMs = baseDelay.TotalMilliseconds * factor;
            // Cap backoff at 15 seconds to keep execution responsive
            return TimeSpan.FromMilliseconds(Math.Min(15000, delayMs));
        }

        /// <summary>
        /// Records a manual retry trigger by an operator for a specific machine.
        /// </summary>
        public bool TriggerManualRetry(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return false;
            _manualRetriesCount.AddOrUpdate(machineId, 1, (_, current) => current + 1);
            return true;
        }

        /// <summary>
        /// Gets the count of manual retries triggered for a machine.
        /// </summary>
        public int GetManualRetryCount(string machineId)
        {
            return _manualRetriesCount.TryGetValue(machineId, out var count) ? count : 0;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.BulkOperations
{
    using TargetCommandStatus = Sayra.Client.Shared.Models.Phase9.Enums.CommandStatus;

    /// <summary>
    /// Core tracking service for calculating aggregated bulk execution metrics, percentage, and dynamic estimated remaining time (ETA).
    /// </summary>
    public class BulkProgressTracker
    {
        private readonly string _bulkOperationId;
        private readonly int _totalTargets;
        private readonly DateTime _startTimeUtc;

        private readonly ConcurrentDictionary<string, BulkOperationExecution> _executions = new();
        private double _emaThroughput = -1; // Exponential Moving Average of machines completed per second
        private const double EmaAlpha = 0.2; // Smoothing factor for EMA

        /// <summary>
        /// Initializes a new instance of BulkProgressTracker.
        /// </summary>
        public BulkProgressTracker(string bulkOperationId, int totalTargets)
        {
            _bulkOperationId = bulkOperationId ?? throw new ArgumentNullException(nameof(bulkOperationId));
            _totalTargets = totalTargets;
            _startTimeUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Tracks or updates the execution status for an individual machine.
        /// </summary>
        public void UpdateMachineState(string machineId, TargetCommandStatus status, int attempt = 1)
        {
            _executions.AddOrUpdate(machineId,
                _ => new BulkOperationExecution
                {
                    MachineId = machineId,
                    Status = status,
                    StartedAtUtc = DateTime.UtcNow,
                    AttemptNumber = attempt
                },
                (_, existing) => existing with
                {
                    Status = status,
                    CompletedAtUtc = (status == TargetCommandStatus.Succeeded || status == TargetCommandStatus.Failed || status == TargetCommandStatus.Cancelled || status == TargetCommandStatus.Expired) ? DateTime.UtcNow : existing.CompletedAtUtc,
                    AttemptNumber = attempt
                });

            // Calculate moving throughput on completion
            if (status == TargetCommandStatus.Succeeded || status == TargetCommandStatus.Failed || status == TargetCommandStatus.Cancelled || status == TargetCommandStatus.Expired)
            {
                var elapsedTotal = DateTime.UtcNow.Subtract(_startTimeUtc).TotalSeconds;
                var completedSoFar = GetCompletedCount();
                if (elapsedTotal > 0.1 && completedSoFar > 0)
                {
                    double instantThroughput = completedSoFar / elapsedTotal;
                    if (_emaThroughput < 0)
                    {
                        _emaThroughput = instantThroughput;
                    }
                    else
                    {
                        _emaThroughput = (EmaAlpha * instantThroughput) + ((1 - EmaAlpha) * _emaThroughput);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the list of current target executions.
        /// </summary>
        public IReadOnlyList<BulkOperationExecution> GetExecutions()
        {
            return _executions.Values.ToList();
        }

        private int GetCompletedCount()
        {
            return _executions.Values.Count(e =>
                e.Status == TargetCommandStatus.Succeeded ||
                e.Status == TargetCommandStatus.Failed ||
                e.Status == TargetCommandStatus.Cancelled ||
                e.Status == TargetCommandStatus.Expired);
        }

        /// <summary>
        /// Aggregates state and computes the latest progress tracking metrics.
        /// </summary>
        public BulkOperationProgress ComputeProgress()
        {
            int running = _executions.Values.Count(e => e.Status == TargetCommandStatus.Executing);
            int succeeded = _executions.Values.Count(e => e.Status == TargetCommandStatus.Succeeded);
            int failed = _executions.Values.Count(e => e.Status == TargetCommandStatus.Failed);
            int cancelled = _executions.Values.Count(e => e.Status == TargetCommandStatus.Cancelled);
            int expired = _executions.Values.Count(e => e.Status == TargetCommandStatus.Expired);

            int completed = succeeded + failed + cancelled + expired;

            // Compute ETA based on EMA throughput
            TimeSpan eta = TimeSpan.Zero;
            int remaining = _totalTargets - completed;
            if (remaining > 0)
            {
                if (_emaThroughput > 0.001)
                {
                    double remainingSeconds = remaining / _emaThroughput;
                    eta = TimeSpan.FromSeconds(remainingSeconds);
                }
                else
                {
                    // Fallback to simple linear estimation
                    var totalElapsed = DateTime.UtcNow.Subtract(_startTimeUtc).TotalSeconds;
                    if (completed > 0 && totalElapsed > 0.5)
                    {
                        double avgSecondsPerMachine = totalElapsed / completed;
                        eta = TimeSpan.FromSeconds(avgSecondsPerMachine * remaining);
                    }
                }
            }

            // Cap ETA to maximum realistic window
            if (eta.TotalDays > 7)
            {
                eta = TimeSpan.FromDays(7);
            }

            return new BulkOperationProgress
            {
                ActiveStatus = (completed >= _totalTargets) ? OperationStatus.Completed : OperationStatus.Running,
                TotalTargets = _totalTargets,
                CompletedCount = completed,
                SucceededCount = succeeded,
                FailedCount = failed + expired,
                RunningCount = running,
                CancelledCount = cancelled,
                SkippedCount = expired,
                EstimatedRemainingTime = eta
            };
        }

        /// <summary>
        /// Rich progress summary containing skipped and cancelled statistics.
        /// </summary>
        public BulkOperationSummary GenerateSummary(string operatorId)
        {
            var progress = ComputeProgress();

            return new BulkOperationSummary
            {
                BulkOperationId = _bulkOperationId,
                TotalCount = _totalTargets,
                SucceededCount = progress.SucceededCount,
                FailedCount = progress.FailedCount,
                SkippedCount = progress.SkippedCount,
                CombinedDuration = DateTime.UtcNow.Subtract(_startTimeUtc),
                OperatorId = operatorId
            };
        }
    }
}

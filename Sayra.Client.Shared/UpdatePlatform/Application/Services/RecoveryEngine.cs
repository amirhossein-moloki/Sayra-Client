using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;
using Sayra.Client.Shared.UpdatePlatform.Domain.Enums;
using Sayra.Client.Shared.UpdatePlatform.Domain.Exceptions;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Services
{
    /// <summary>
    /// Implements automated recovery coordinating the state machine, rollback triggers, and validation.
    /// Tolerates interrupted installations, corruption, and partial file modifications to restore sanity.
    /// Incorporates strict concurrent recovery prevention.
    /// </summary>
    public class RecoveryEngine : IRecoveryEngine
    {
        private readonly ILogger<RecoveryEngine> _logger;
        private readonly IRollbackEngine _rollbackEngine;
        private readonly IRecoveryValidator _recoveryValidator;
        private readonly IRecoveryStateMachine _stateMachine;
        private readonly SemaphoreSlim _recoverySemaphore = new SemaphoreSlim(1, 1);

        public RecoveryEngine(
            ILogger<RecoveryEngine> logger,
            IRollbackEngine rollbackEngine,
            IRecoveryValidator recoveryValidator,
            IRecoveryStateMachine stateMachine)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _rollbackEngine = rollbackEngine ?? throw new ArgumentNullException(nameof(rollbackEngine));
            _recoveryValidator = recoveryValidator ?? throw new ArgumentNullException(nameof(recoveryValidator));
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        }

        public async Task<RecoveryReport> RecoverAsync(RecoveryContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("Initiating system recovery workflow for failed target version '{TargetVersion}'...", context.TargetVersion);

            var report = new RecoveryReport
            {
                FailedVersion = context.TargetVersion,
                RestoredVersion = context.SourceVersion,
                Timestamp = DateTime.UtcNow
            };

            bool semaphoreAcquired = false;
            try
            {
                // Strict concurrent recovery prevention (inside try block for complete exception safety & cancellation handling)
                if (!await _recoverySemaphore.WaitAsync(0, cancellationToken))
                {
                    _logger.LogWarning("Concurrent recovery request rejected: A recovery operation is already in progress.");
                    return new RecoveryReport
                    {
                        Succeeded = false,
                        ErrorMessage = "Recovery already in progress",
                        FinalState = _stateMachine.CurrentState,
                        FailedVersion = context.TargetVersion,
                        RestoredVersion = context.SourceVersion,
                        Timestamp = DateTime.UtcNow
                    };
                }
                semaphoreAcquired = true;

                if (_stateMachine.CurrentState != RecoveryState.RecoveryRequired)
                {
                    _stateMachine.TransitionTo(RecoveryState.RecoveryRequired);
                }

                // Transition to RollingBack
                _stateMachine.TransitionTo(RecoveryState.RollingBack);

                _logger.LogInformation("Executing Rollback restore operation to last known good version '{SourceVersion}'...", context.SourceVersion);
                bool rollbackOk = await _rollbackEngine.ExecuteRollbackAsync(context.SourceVersion, $"System health failure on {context.TargetVersion}", cancellationToken);

                if (!rollbackOk)
                {
                    throw new RecoveryFailedException($"Rollback engine failed to restore snapshot '{context.SourceVersion}'.");
                }

                // Transition to Restoring
                _stateMachine.TransitionTo(RecoveryState.Restoring);

                // Transition to Verifying
                _stateMachine.TransitionTo(RecoveryState.Verifying);

                _logger.LogInformation("Verifying system health after restoration...");
                var validationResult = await _recoveryValidator.ValidateHealthAsync(context, cancellationToken);

                // In case of rollback validation, we check the source state directories rather than target,
                // but the validator will verify the directory contents. If directories are healthy, we're good.
                if (!validationResult.IsHealthy)
                {
                    throw new RecoveryFailedException("Restored system failed post-rollback health checks.");
                }

                _stateMachine.TransitionTo(RecoveryState.Completed);

                report.Succeeded = true;
                report.FinalState = RecoveryState.Completed;
                _logger.LogInformation("System recovery COMPLETED successfully. Rolled back from '{Target}' to '{Source}'.", context.TargetVersion, context.SourceVersion);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "System recovery FAILED.");

                try
                {
                    _stateMachine.TransitionTo(RecoveryState.Failed);
                }
                catch { }

                report.Succeeded = false;
                report.FinalState = RecoveryState.Failed;
                report.ErrorMessage = ex.Message;
                return report;
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    _recoverySemaphore.Release();
                }
            }
        }

        public async Task<bool> DetectAndTriggerRecoveryIfNeededAsync(RecoveryContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Checking system deployment health...");

            try
            {
                var health = await _recoveryValidator.ValidateHealthAsync(context, cancellationToken);
                if (!health.IsHealthy)
                {
                    _logger.LogWarning("System health check failed. Corruption or partial file replacement detected!");

                    _stateMachine.TransitionTo(RecoveryState.RecoveryRequired);
                    var report = await RecoverAsync(context, cancellationToken);

                    return report.Succeeded;
                }

                _logger.LogInformation("System deployment is healthy. No recovery required.");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception encountered during automatic recovery detection. Triggering emergency recovery.");

                try
                {
                    _stateMachine.TransitionTo(RecoveryState.RecoveryRequired);
                    var report = await RecoverAsync(context, cancellationToken);
                    return report.Succeeded;
                }
                catch (Exception recEx)
                {
                    _logger.LogCritical(recEx, "Emergency recovery execution failed.");
                    _stateMachine.TransitionTo(RecoveryState.Failed);
                    return false;
                }
            }
        }
    }
}

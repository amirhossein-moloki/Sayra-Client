using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;
using Sayra.Client.Shared.Models.Telemetry.Enums;
using Sayra.Client.Shared.Models.Telemetry.Options;

namespace Sayra.Client.Shared.Telemetry.Alerts
{
    public class AlertEngine : IAlertEngine
    {
        private readonly ConcurrentDictionary<string, AlertRecord> _alerts = new();
        private readonly IAlertRuleProvider _ruleProvider;
        private readonly IAlertPolicyProvider _policyProvider;
        private readonly IAlertDeduplicationProvider _deduplicationProvider;
        private readonly IAlertRecoveryProvider _recoveryProvider;
        private readonly IAlertSuppressionProvider _suppressionProvider;
        private readonly IAlertEscalationProvider _escalationProvider;

        public AlertEngine(
            IAlertRuleProvider ruleProvider,
            IAlertPolicyProvider policyProvider,
            IAlertDeduplicationProvider deduplicationProvider,
            IAlertRecoveryProvider recoveryProvider,
            IAlertSuppressionProvider suppressionProvider,
            IAlertEscalationProvider escalationProvider)
        {
            _ruleProvider = ruleProvider ?? throw new ArgumentNullException(nameof(ruleProvider));
            _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
            _deduplicationProvider = deduplicationProvider ?? throw new ArgumentNullException(nameof(deduplicationProvider));
            _recoveryProvider = recoveryProvider ?? throw new ArgumentNullException(nameof(recoveryProvider));
            _suppressionProvider = suppressionProvider ?? throw new ArgumentNullException(nameof(suppressionProvider));
            _escalationProvider = escalationProvider ?? throw new ArgumentNullException(nameof(escalationProvider));
        }

        public IReadOnlyCollection<AlertRecord> GetAllAlerts() => _alerts.Values.ToList();

        public async Task EvaluateRulesAsync(CancellationToken cancellationToken = default)
        {
            var evaluators = await _ruleProvider.GetRuleEvaluatorsAsync(cancellationToken);

            var tasks = evaluators.Select(async evaluator =>
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var policy = await _policyProvider.GetPolicyAsync(evaluator.RuleName, cancellationToken);
                    if (policy != null && policy.Evaluation != null && !policy.Evaluation.Enabled)
                        return;

                    var alert = await evaluator.EvaluateAsync(cancellationToken);
                    if (alert != null)
                    {
                        var enrichedAlert = alert with
                        {
                            Status = AlertStatus.Created,
                            CreatedAt = DateTime.UtcNow
                        };

                        await ProcessAlertAsync(enrichedAlert, cancellationToken);
                    }
                    else
                    {
                        await CheckAndRecoverAlertAsync(evaluator.RuleName, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception)
                {
                    // Isolate failures
                }
            });

            await Task.WhenAll(tasks);
        }

        public async Task ProcessAlertAsync(AlertRecord alert, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var policy = await _policyProvider.GetPolicyAsync(alert.Name, cancellationToken);

            if (_suppressionProvider.IsSuppressed(alert, policy, DateTime.UtcNow))
            {
                var suppressedAlert = alert with
                {
                    Status = AlertStatus.Suppressed,
                    SuppressedAt = DateTime.UtcNow
                };
                _alerts[suppressedAlert.AlertId] = suppressedAlert;
                return;
            }

            var existingAlert = _alerts.Values.FirstOrDefault(existing =>
                _deduplicationProvider.IsDuplicate(existing, alert, TimeSpan.FromSeconds(policy.RateLimit.WindowSeconds)));

            if (existingAlert != null)
            {
                var updatedAlert = existingAlert with
                {
                    Value = alert.Value,
                    Timestamp = DateTime.UtcNow,
                    Message = string.IsNullOrWhiteSpace(existingAlert.Message) ? alert.Message : $"{existingAlert.Message} | Recurred: {alert.Message}"
                };

                var escalatedAlert = await _escalationProvider.CheckAndEscalateAsync(updatedAlert, policy, cancellationToken);
                _alerts[existingAlert.AlertId] = escalatedAlert ?? updatedAlert;
            }
            else
            {
                var activeAlert = alert with
                {
                    Status = AlertStatus.Active,
                    CreatedAt = alert.CreatedAt ?? DateTime.UtcNow
                };
                _alerts[activeAlert.AlertId] = activeAlert;
            }
        }

        public Task<IReadOnlyCollection<AlertRecord>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyCollection<AlertRecord> active = _alerts.Values
                .Where(a => a.Status == AlertStatus.Active || a.Status == AlertStatus.Created || a.Status == AlertStatus.Escalated)
                .ToList();

            return Task.FromResult(active);
        }

        public Task AcknowledgeAlertAsync(string alertId, string operatorId, string? comment = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_alerts.TryGetValue(alertId, out var alert))
            {
                var ackedAlert = alert with
                {
                    Acknowledged = true,
                    AcknowledgedBy = operatorId,
                    AcknowledgedAt = DateTime.UtcNow,
                    AcknowledgementComment = comment,
                    Status = AlertStatus.Acknowledged
                };
                _alerts[alertId] = ackedAlert;
            }

            return Task.CompletedTask;
        }

        public Task ResolveAlertAsync(string alertId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_alerts.TryGetValue(alertId, out var alert))
            {
                var resolvedAlert = alert with
                {
                    Resolved = true,
                    ResolvedAt = DateTime.UtcNow,
                    Status = AlertStatus.Resolved
                };
                _alerts[alertId] = resolvedAlert;
            }

            return Task.CompletedTask;
        }

        private async Task CheckAndRecoverAlertAsync(string ruleName, CancellationToken cancellationToken)
        {
            var activeAlerts = _alerts.Values.Where(a => a.Name == ruleName && !a.Resolved).ToList();

            foreach (var alert in activeAlerts)
            {
                var policy = await _policyProvider.GetPolicyAsync(ruleName, cancellationToken);
                bool recovered = await _recoveryProvider.EvaluateRecoveryAsync(alert, policy, cancellationToken);

                if (recovered)
                {
                    var recoveredAlert = alert with
                    {
                        Resolved = true,
                        ResolvedAt = DateTime.UtcNow,
                        RecoveredAt = DateTime.UtcNow,
                        Status = AlertStatus.Recovered,
                        Message = $"{alert.Message} (Automatically Recovered)"
                    };
                    _alerts[alert.AlertId] = recoveredAlert;
                }
            }
        }
    }
}

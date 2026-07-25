using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Domain.Events;

namespace Sayra.Client.Shared.Security.GameProtection.Application.Services;

public class ThreatReporter : IThreatReporter
{
    private readonly ILogger<ThreatReporter> _logger;
    private readonly IAuditLogger _auditLogger;
    private readonly IEventDispatcher _eventDispatcher;

    public ThreatReporter(
        ILogger<ThreatReporter> logger,
        IAuditLogger auditLogger,
        IEventDispatcher eventDispatcher)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public void ReportThreat(SecurityThreatEventBase threatEvent)
    {
        if (threatEvent == null) throw new ArgumentNullException(nameof(threatEvent));

        _logger.LogWarning("Threat reported! Type: {Type}, ProcessName: {Process}, PID: {Pid}, Severity: {Severity}, Reason: {Reason}",
            threatEvent.GetType().Name, threatEvent.ProcessName, threatEvent.ProcessId, threatEvent.Severity, threatEvent.Reason);

        // 1. Dispatch event locally to any in-memory handlers
        try
        {
            _eventDispatcher.Dispatch(threatEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dispatch threat event locally.");
        }

        // 2. Log security critical details using the primary Audit Logger (Serilog + SQLCipher persistence)
        try
        {
            var properties = new Dictionary<string, object>
            {
                { "EventType", threatEvent.GetType().Name },
                { "ProcessName", threatEvent.ProcessName },
                { "ProcessId", threatEvent.ProcessId },
                { "Severity", threatEvent.Severity },
                { "Reason", threatEvent.Reason },
                { "Timestamp", threatEvent.Timestamp }
            };

            if (threatEvent is UnauthorizedProcessDetectedEvent unauthEvent)
            {
                properties.Add("ExecutablePath", unauthEvent.ExecutablePath);
            }
            else if (threatEvent is IntegrityCheckFailedEvent integrityEvent)
            {
                properties.Add("FilePath", integrityEvent.FilePath);
                properties.Add("ExpectedHash", integrityEvent.ExpectedHash);
                properties.Add("ActualHash", integrityEvent.ActualHash);
            }
            else if (threatEvent is BlockedApplicationDetectedEvent blockedEvent)
            {
                properties.Add("RulePatternMatched", blockedEvent.RulePatternMatched);
            }
            else if (threatEvent is TamperingDetectedEvent tamperingEvent)
            {
                properties.Add("TargetComponent", tamperingEvent.TargetComponent);
            }

            _auditLogger.LogSecurity($"[Security] Threat detected: {threatEvent.Reason}", properties);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log threat event to IAuditLogger.");
        }
    }
}

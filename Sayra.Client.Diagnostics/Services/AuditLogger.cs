using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;
using Sayra.Client.Shared.Logging;

namespace Sayra.Client.Diagnostics.Services
{
    public class AuditLogger : IAuditLogger
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly IAuditLogRepository _repository;
        private readonly ILogger<AuditLogger> _logger;

        public AuditLogger(
            IServiceProvider serviceProvider,
            IEventDispatcher eventDispatcher,
            IAuditLogRepository repository,
            ILogger<AuditLogger> logger)
        {
            _serviceProvider = serviceProvider;
            _eventDispatcher = eventDispatcher;
            _repository = repository;
            _logger = logger;
        }

        public void LogSecurity(string messageTemplate, Dictionary<string, object>? properties = null)
        {
            Log(messageTemplate, "SECURITY", "FATAL", properties);
        }

        public void LogAudit(string messageTemplate, Dictionary<string, object>? properties = null)
        {
            Log(messageTemplate, "AUDIT", "INFO", properties);
        }

        public void LogOperational(string messageTemplate, Dictionary<string, object>? properties = null)
        {
            Log(messageTemplate, "OPERATIONAL", "INFO", properties);
        }

        public void LogPerformance(string messageTemplate, Dictionary<string, object>? properties = null)
        {
            Log(messageTemplate, "PERFORMANCE", "DEBUG", properties);
        }

        public void LogEvent(EventLogEntry entry)
        {
            if (entry == null) return;

            // Persist to SQLite synchronously to guarantee zero-loss auditing and prevent race conditions
            try
            {
                _repository.AddLogAsync(entry).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to persist event log entry to SQLite.");
            }

            // Dispatch in-memory
            try
            {
                _eventDispatcher.Dispatch(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching event in-memory.");
            }

            // Write to Serilog with structured properties
            var enricher = Serilog.Log.ForContext("EventId", entry.EventId)
                .ForContext("CorrelationId", entry.CorrelationId)
                .ForContext("SessionId", entry.SessionId)
                .ForContext("TraceId", entry.TraceId)
                .ForContext("Category", entry.Category)
                .ForContext("Severity", entry.Severity);

            if (entry.PayloadFields != null)
            {
                foreach (var kvp in entry.PayloadFields)
                {
                    enricher = enricher.ForContext(kvp.Key, kvp.Value, destructureObjects: true);
                }
            }

            switch (entry.Severity.ToUpperInvariant())
            {
                case "DEBUG":
                    enricher.Debug(entry.MessageTemplate);
                    break;
                case "INFO":
                    enricher.Information(entry.MessageTemplate);
                    break;
                case "WARNING":
                    enricher.Warning(entry.MessageTemplate);
                    break;
                case "ERROR":
                    enricher.Error(entry.MessageTemplate);
                    break;
                case "FATAL":
                    enricher.Fatal(entry.MessageTemplate);
                    break;
                default:
                    enricher.Information(entry.MessageTemplate);
                    break;
            }
        }

        private void Log(string messageTemplate, string category, string severity, Dictionary<string, object>? properties)
        {
            var correlationId = TracingContext.CorrelationId ?? Guid.NewGuid().ToString();
            var traceId = TracingContext.TraceId ?? Guid.NewGuid().ToString();

            // Fetch SessionId dynamically from SessionContextProvider to avoid circular dependencies
            string? sessionId = null;
            try
            {
                var contextProvider = _serviceProvider.GetService<ISessionContextProvider>();
                if (contextProvider != null)
                {
                    sessionId = contextProvider.CurrentSessionId;
                }
            }
            catch
            {
                // Fallback if provider is not available
            }

            var entry = new EventLogEntry
            {
                EventId = Guid.NewGuid(),
                CorrelationId = correlationId,
                SessionId = sessionId,
                TraceId = traceId,
                Category = category,
                Severity = severity,
                MessageTemplate = messageTemplate,
                PayloadFields = properties ?? new(),
                Timestamp = DateTime.UtcNow
            };

            LogEvent(entry);
        }
    }
}

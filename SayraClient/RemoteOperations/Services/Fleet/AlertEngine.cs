using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services.Fleet
{
    public class AlertEngine : IAlertManager
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly IAuditService _auditService;
        private readonly ILogger<AlertEngine> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ConcurrentDictionary<string, DateTime> _cooldownCache = new();

        public AlertEngine(
            ILocalDatabaseService databaseService,
            IAuditService auditService,
            ILogger<AlertEngine> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task ProcessMetricAsync(string machineId, string metricName, string value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(machineId)) throw new ArgumentNullException(nameof(machineId));
            if (string.IsNullOrEmpty(metricName)) throw new ArgumentNullException(nameof(metricName));

            var rules = await GetAlertRulesAsync(cancellationToken);
            foreach (var rule in rules)
            {
                if (!string.Equals(rule.MetricName, metricName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isViolated = EvaluateThreshold(rule.Operator, value, rule.Threshold);
                string cacheKey = $"{machineId}:{rule.RuleId}";

                if (isViolated)
                {
                    if (_cooldownCache.TryGetValue(cacheKey, out var lastTriggered))
                    {
                        if ((DateTime.UtcNow - lastTriggered).TotalSeconds < rule.CooldownSeconds)
                        {
                            _logger.LogDebug("Alert suppressed due to active cooldown: {Key}", cacheKey);
                            continue;
                        }
                    }

                    bool hasActive = await HasActiveAlertAsync(machineId, rule.RuleId, cancellationToken);
                    if (hasActive)
                    {
                        var activeAlert = await GetActiveAlertAsync(machineId, rule.RuleId, cancellationToken);
                        if (activeAlert != null)
                        {
                            var activeTime = (DateTime.UtcNow - activeAlert.TriggeredAt).TotalSeconds;
                            if (activeTime >= rule.EscalationTimeoutSeconds && activeAlert.EscalationLevel == 0)
                            {
                                await EscalateAlertAsync(activeAlert, rule, cancellationToken);
                            }
                        }
                        continue;
                    }

                    _cooldownCache[cacheKey] = DateTime.UtcNow;

                    var alert = new FleetAlert
                    {
                        AlertId = Guid.NewGuid().ToString(),
                        MachineId = machineId,
                        RuleId = rule.RuleId,
                        MetricName = metricName,
                        Value = value,
                        Threshold = rule.Threshold,
                        Severity = rule.Severity,
                        CooldownSeconds = rule.CooldownSeconds,
                        TriggeredAt = DateTime.UtcNow,
                        Status = "Active",
                        EscalationLevel = 0
                    };

                    await SaveAlertAsync(alert, cancellationToken);

                    await _auditService.RecordPolicyEventAsync(alert.AlertId, "ALERT_GENERATED", $"Alert triggered: {metricName} value '{value}' violated threshold '{rule.Threshold}'.", Guid.NewGuid().ToString(), cancellationToken);
                }
                else
                {
                    if (rule.AutoResolve)
                    {
                        var activeAlert = await GetActiveAlertAsync(machineId, rule.RuleId, cancellationToken);
                        if (activeAlert != null)
                        {
                            await ResolveAlertInternalAsync(activeAlert.AlertId, cancellationToken);
                        }
                    }
                }
            }
        }

        public async Task<List<FleetAlert>> GetActiveAlertsAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AlertId, MachineId, RuleId, MetricName, Value, Threshold, Severity, CooldownSeconds, TriggeredAt, Status, ResolvedAt, EscalationLevel
                FROM FleetAlerts
                WHERE Status = 'Active';";

            var list = new List<FleetAlert>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new FleetAlert
                {
                    AlertId = reader.GetString(0),
                    MachineId = reader.GetString(1),
                    RuleId = reader.GetString(2),
                    MetricName = reader.GetString(3),
                    Value = reader.GetString(4),
                    Threshold = reader.GetString(5),
                    Severity = reader.GetString(6),
                    CooldownSeconds = reader.GetInt32(7),
                    TriggeredAt = DateTime.Parse(reader.GetString(8)).ToUniversalTime(),
                    Status = reader.GetString(9),
                    ResolvedAt = reader.IsDBNull(10) ? default(DateTime?) : DateTime.Parse(reader.GetString(10)).ToUniversalTime(),
                    EscalationLevel = reader.GetInt32(11)
                });
            }
            return list;
        }

        public async Task ResolveAlertAsync(string alertId, CancellationToken cancellationToken = default)
        {
            await ResolveAlertInternalAsync(alertId, cancellationToken);
        }

        private async Task ResolveAlertInternalAsync(string alertId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(alertId)) throw new ArgumentNullException(nameof(alertId));

            _logger.LogInformation("Resolving alert '{Id}'...", alertId);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE FleetAlerts SET Status = 'Resolved', ResolvedAt = $now WHERE AlertId = $id AND Status = 'Active';";
            cmd.Parameters.Add(CreateParam(cmd, "$id", alertId));
            cmd.Parameters.Add(CreateParam(cmd, "$now", DateTime.UtcNow.ToString("O")));

            int rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (rows > 0)
            {
                await _auditService.RecordPolicyEventAsync(alertId, "ALERT_RESOLVED", "Alert successfully resolved.", Guid.NewGuid().ToString(), cancellationToken);
            }
        }

        public async Task ConfigureRuleAsync(AlertRule rule, CancellationToken cancellationToken = default)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO AlertRules (RuleId, MetricName, Operator, Threshold, Severity, CooldownSeconds, EscalationTimeoutSeconds, AutoResolve, EscalationPath)
                VALUES ($id, $metric, $op, $threshold, $severity, $cooldown, $escalationTimeout, $autoResolve, $path)
                ON CONFLICT(RuleId) DO UPDATE SET
                    MetricName = excluded.MetricName,
                    Operator = excluded.Operator,
                    Threshold = excluded.Threshold,
                    Severity = excluded.Severity,
                    CooldownSeconds = excluded.CooldownSeconds,
                    EscalationTimeoutSeconds = excluded.EscalationTimeoutSeconds,
                    AutoResolve = excluded.AutoResolve,
                    EscalationPath = excluded.EscalationPath;";

            cmd.Parameters.Add(CreateParam(cmd, "$id", rule.RuleId));
            cmd.Parameters.Add(CreateParam(cmd, "$metric", rule.MetricName));
            cmd.Parameters.Add(CreateParam(cmd, "$op", rule.Operator));
            cmd.Parameters.Add(CreateParam(cmd, "$threshold", rule.Threshold));
            cmd.Parameters.Add(CreateParam(cmd, "$severity", rule.Severity));
            cmd.Parameters.Add(CreateParam(cmd, "$cooldown", rule.CooldownSeconds));
            cmd.Parameters.Add(CreateParam(cmd, "$escalationTimeout", rule.EscalationTimeoutSeconds));
            cmd.Parameters.Add(CreateParam(cmd, "$autoResolve", rule.AutoResolve ? 1 : 0));
            cmd.Parameters.Add(CreateParam(cmd, "$path", rule.EscalationPath));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<List<AlertRule>> GetAlertRulesAsync(CancellationToken cancellationToken = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT RuleId, MetricName, Operator, Threshold, Severity, CooldownSeconds, EscalationTimeoutSeconds, AutoResolve, EscalationPath FROM AlertRules;";

            var list = new List<AlertRule>();
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                list.Add(new AlertRule
                {
                    RuleId = reader.GetString(0),
                    MetricName = reader.GetString(1),
                    Operator = reader.GetString(2),
                    Threshold = reader.GetString(3),
                    Severity = reader.GetString(4),
                    CooldownSeconds = reader.GetInt32(5),
                    EscalationTimeoutSeconds = reader.GetInt32(6),
                    AutoResolve = reader.GetInt32(7) == 1,
                    EscalationPath = reader.GetString(8)
                });
            }
            return list;
        }

        private async Task EscalateAlertAsync(FleetAlert alert, AlertRule rule, CancellationToken cancellationToken)
        {
            _logger.LogWarning("Escalating active alert '{AlertId}' (Escalation level 0 -> 1) via path: {Path}", alert.AlertId, rule.EscalationPath);

            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE FleetAlerts SET EscalationLevel = 1, Severity = 'Critical' WHERE AlertId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", alert.AlertId));

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            await _auditService.RecordPolicyEventAsync(alert.AlertId, "ALERT_GENERATED", $"Alert escalated. Severity increased to Critical. Escalation path: {rule.EscalationPath}.", Guid.NewGuid().ToString(), cancellationToken);
        }

        private async Task SaveAlertAsync(FleetAlert alert, CancellationToken cancellationToken)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO FleetAlerts (AlertId, MachineId, RuleId, MetricName, Value, Threshold, Severity, CooldownSeconds, TriggeredAt, Status, EscalationLevel)
                VALUES ($id, $machineId, $ruleId, $metric, $val, $threshold, $severity, $cooldown, $triggeredAt, $status, $level);";

            cmd.Parameters.Add(CreateParam(cmd, "$id", alert.AlertId));
            cmd.Parameters.Add(CreateParam(cmd, "$machineId", alert.MachineId));
            cmd.Parameters.Add(CreateParam(cmd, "$ruleId", alert.RuleId));
            cmd.Parameters.Add(CreateParam(cmd, "$metric", alert.MetricName));
            cmd.Parameters.Add(CreateParam(cmd, "$val", alert.Value));
            cmd.Parameters.Add(CreateParam(cmd, "$threshold", alert.Threshold));
            cmd.Parameters.Add(CreateParam(cmd, "$severity", alert.Severity));
            cmd.Parameters.Add(CreateParam(cmd, "$cooldown", alert.CooldownSeconds));
            cmd.Parameters.Add(CreateParam(cmd, "$triggeredAt", alert.TriggeredAt.ToString("O")));
            cmd.Parameters.Add(CreateParam(cmd, "$status", alert.Status));
            cmd.Parameters.Add(CreateParam(cmd, "$level", alert.EscalationLevel));

            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        private async Task<bool> HasActiveAlertAsync(string machineId, string ruleId, CancellationToken cancellationToken)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM FleetAlerts WHERE MachineId = $mId AND RuleId = $rId AND Status = 'Active';";
            cmd.Parameters.Add(CreateParam(cmd, "$mId", machineId));
            cmd.Parameters.Add(CreateParam(cmd, "$rId", ruleId));

            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
            return count > 0;
        }

        private async Task<FleetAlert?> GetActiveAlertAsync(string machineId, string ruleId, CancellationToken cancellationToken)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AlertId, MachineId, RuleId, MetricName, Value, Threshold, Severity, CooldownSeconds, TriggeredAt, Status, ResolvedAt, EscalationLevel
                FROM FleetAlerts
                WHERE MachineId = $mId AND RuleId = $rId AND Status = 'Active'
                LIMIT 1;";
            cmd.Parameters.Add(CreateParam(cmd, "$mId", machineId));
            cmd.Parameters.Add(CreateParam(cmd, "$rId", ruleId));

            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new FleetAlert
                {
                    AlertId = reader.GetString(0),
                    MachineId = reader.GetString(1),
                    RuleId = reader.GetString(2),
                    MetricName = reader.GetString(3),
                    Value = reader.GetString(4),
                    Threshold = reader.GetString(5),
                    Severity = reader.GetString(6),
                    CooldownSeconds = reader.GetInt32(7),
                    TriggeredAt = DateTime.Parse(reader.GetString(8)).ToUniversalTime(),
                    Status = reader.GetString(9),
                    ResolvedAt = reader.IsDBNull(10) ? default(DateTime?) : DateTime.Parse(reader.GetString(10)).ToUniversalTime(),
                    EscalationLevel = reader.GetInt32(11)
                };
            }
            return null;
        }

        private bool EvaluateThreshold(string op, string rawValue, string threshold)
        {
            if (double.TryParse(rawValue, out double val) && double.TryParse(threshold, out double limit))
            {
                switch (op)
                {
                    case "==": return val == limit;
                    case "!=": return val != limit;
                    case ">": return val > limit;
                    case ">=": return val >= limit;
                    case "<": return val < limit;
                    case "<=": return val <= limit;
                    default: return false;
                }
            }
            else
            {
                switch (op)
                {
                    case "==": return string.Equals(rawValue, threshold, StringComparison.OrdinalIgnoreCase);
                    case "!=": return !string.Equals(rawValue, threshold, StringComparison.OrdinalIgnoreCase);
                    default: return false;
                }
            }
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }
    }
}

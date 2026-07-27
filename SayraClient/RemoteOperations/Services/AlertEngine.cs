using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace SayraClient.RemoteOperations.Services
{
    public class AlertEngine : IAlertManager
    {
        private readonly ILocalDatabaseService _databaseService;
        private readonly IAuditService _auditService;
        private readonly ILogger<AlertEngine> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private readonly List<AlertRule> _configuredRules = new();

        public AlertEngine(
            ILocalDatabaseService databaseService,
            IAuditService _audit,
            ILogger<AlertEngine> logger)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _auditService = _audit ?? throw new ArgumentNullException(nameof(_audit));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Load default configurable rules
            _configuredRules.Add(new AlertRule
            {
                RuleId = "rule-gpu-temp",
                AlertType = "GPU_TEMP",
                ThresholdExpression = "> 90",
                CooldownMinutes = 5,
                Severity = "Warning",
                AutoResolve = true,
                EscalationEnabled = true,
                EscalationThresholdMinutes = 15
            });

            _configuredRules.Add(new AlertRule
            {
                RuleId = "rule-cpu-temp",
                AlertType = "CPU_TEMP",
                ThresholdExpression = "> 85",
                CooldownMinutes = 5,
                Severity = "Warning",
                AutoResolve = true,
                EscalationEnabled = true,
                EscalationThresholdMinutes = 15
            });

            _configuredRules.Add(new AlertRule
            {
                RuleId = "rule-disk-full",
                AlertType = "DISK_FULL",
                ThresholdExpression = "> 95",
                CooldownMinutes = 15,
                Severity = "Warning",
                AutoResolve = true,
                EscalationEnabled = false
            });

            _configuredRules.Add(new AlertRule
            {
                RuleId = "rule-net-lost",
                AlertType = "NETWORK_LOST",
                ThresholdExpression = "== 1",
                CooldownMinutes = 2,
                Severity = "Critical",
                AutoResolve = true,
                EscalationEnabled = true,
                EscalationThresholdMinutes = 5
            });
        }

        public async Task ProcessMetricAsync(string workstationId, string metricType, double value, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(workstationId)) throw new ArgumentException("Workstation ID cannot be empty", nameof(workstationId));
            if (string.IsNullOrEmpty(metricType)) throw new ArgumentException("Metric type cannot be empty", nameof(metricType));

            await _lock.WaitAsync(ct);
            try
            {
                var rule = _configuredRules.Find(r => r.AlertType.Equals(metricType, StringComparison.OrdinalIgnoreCase));
                if (rule == null) return;

                bool violates = EvaluateThreshold(value, rule.ThresholdExpression);
                await HandleAlertEvaluationAsync(workstationId, metricType, violates, rule, $"{metricType} is currently {value} (threshold {rule.ThresholdExpression})", ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task ProcessStatusAsync(string workstationId, string statusType, string value, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(workstationId)) throw new ArgumentException("Workstation ID cannot be empty", nameof(workstationId));
            if (string.IsNullOrEmpty(statusType)) throw new ArgumentException("Status type cannot be empty", nameof(statusType));

            await _lock.WaitAsync(ct);
            try
            {
                var rule = _configuredRules.Find(r => r.AlertType.Equals(statusType, StringComparison.OrdinalIgnoreCase));
                if (rule == null) return;

                bool violates = value.Equals("Offline", StringComparison.OrdinalIgnoreCase) || value.Equals("Critical", StringComparison.OrdinalIgnoreCase) || value.Equals("1", StringComparison.OrdinalIgnoreCase);
                await HandleAlertEvaluationAsync(workstationId, statusType, violates, rule, $"{statusType} is in violation state '{value}'", ct);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<FleetAlert>> GetActiveAlertsAsync(CancellationToken ct = default)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AlertId, WorkstationId, AlertType, Severity, Message, CreatedAt, ResolvedAt, CooldownExpiresAt, Escalated, IsActive
                FROM FleetAlerts
                WHERE IsActive = 1;";

            using var reader = await cmd.ExecuteReaderAsync(ct);
            var list = new List<FleetAlert>();
            while (await reader.ReadAsync(ct))
            {
                list.Add(new FleetAlert
                {
                    AlertId = reader.GetString(0),
                    WorkstationId = reader.GetString(1),
                    AlertType = reader.GetString(2),
                    Severity = reader.GetString(3),
                    Message = reader.GetString(4),
                    CreatedAt = reader.GetString(5),
                    ResolvedAt = reader.GetString(6),
                    CooldownExpiresAt = reader.GetString(7),
                    Escalated = reader.GetInt32(8),
                    IsActive = reader.GetInt32(9)
                });
            }

            return list;
        }

        public async Task ResolveAlertAsync(string alertId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(alertId)) return;

            _logger.LogInformation("Manually resolving alert '{AlertId}'", alertId);

            var alert = await GetAlertByIdAsync(alertId, ct);
            if (alert != null && alert.IsActive == 1)
            {
                alert.IsActive = 0;
                alert.ResolvedAt = DateTime.UtcNow.ToString("O");
                await SaveAlertAsync(alert, ct);

                await _auditService.RecordPolicyEventAsync(alertId, "ALERT_RESOLVED", $"Alert '{alert.AlertType}' resolved manually on workstation '{alert.WorkstationId}'.", alertId, ct);
            }
        }

        #region Evaluation Logic

        private async Task HandleAlertEvaluationAsync(string wsId, string alertType, bool violates, AlertRule rule, string message, CancellationToken ct)
        {
            var activeAlert = await GetActiveAlertAsync(wsId, alertType, ct);

            if (violates)
            {
                if (activeAlert != null)
                {
                    // Alert is already active. Check for Escalation or Cooldown/Suppression.
                    if (DateTime.TryParse(activeAlert.CreatedAt, out var createdAt))
                    {
                        var durationActive = DateTime.UtcNow - createdAt;
                        if (rule.EscalationEnabled && activeAlert.Escalated == 0 && durationActive.TotalMinutes >= rule.EscalationThresholdMinutes)
                        {
                            activeAlert.Escalated = 1;
                            activeAlert.Severity = "Critical"; // Escalate severity
                            activeAlert.Message += " [ESCALATED]";
                            await SaveAlertAsync(activeAlert, ct);

                            _logger.LogWarning("ALERT ESCALATION: Alert '{AlertType}' on workstation '{WorkstationId}' has escalated to Critical", alertType, wsId);
                            await _auditService.RecordPolicyEventAsync(activeAlert.AlertId, "ALERT_ESCALATED", $"Alert '{alertType}' on workstation '{wsId}' has escalated to Critical due to prolonged violation.", activeAlert.AlertId, ct);
                        }
                    }
                    _logger.LogDebug("Duplicate alert suppressed/aggregated for AlertType '{AlertType}' on workstation '{WorkstationId}'", alertType, wsId);
                }
                else
                {
                    // Create new Alert
                    string alertId = Guid.NewGuid().ToString();
                    var alert = new FleetAlert
                    {
                        AlertId = alertId,
                        WorkstationId = wsId,
                        AlertType = alertType,
                        Severity = rule.Severity,
                        Message = message,
                        CreatedAt = DateTime.UtcNow.ToString("O"),
                        ResolvedAt = "",
                        CooldownExpiresAt = DateTime.UtcNow.AddMinutes(rule.CooldownMinutes).ToString("O"),
                        Escalated = 0,
                        IsActive = 1
                    };

                    await SaveAlertAsync(alert, ct);

                    _logger.LogWarning("ALERT GENERATED: Alert '{AlertType}' on workstation '{WorkstationId}' with severity '{Severity}'", alertType, wsId, rule.Severity);
                    await _auditService.RecordPolicyEventAsync(alertId, "ALERT_GENERATED", $"Alert '{alertType}' generated on workstation '{wsId}'. Message: {message}", alertId, ct);
                }
            }
            else
            {
                // No violation. Check if we need to auto-resolve active alert.
                if (activeAlert != null && rule.AutoResolve)
                {
                    activeAlert.IsActive = 0;
                    activeAlert.ResolvedAt = DateTime.UtcNow.ToString("O");
                    await SaveAlertAsync(activeAlert, ct);

                    _logger.LogInformation("ALERT RESOLVED: Alert '{AlertType}' on workstation '{WorkstationId}' resolved automatically", alertType, wsId);
                    await _auditService.RecordPolicyEventAsync(activeAlert.AlertId, "ALERT_RESOLVED", $"Alert '{alertType}' on workstation '{wsId}' resolved automatically.", activeAlert.AlertId, ct);
                }
            }
        }

        private bool EvaluateThreshold(double val, string expr)
        {
            if (string.IsNullOrWhiteSpace(expr)) return false;
            var parts = expr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return false;

            string op = parts[0];
            if (!double.TryParse(parts[1], out double thresholdVal)) return false;

            return op switch
            {
                "==" => Math.Abs(val - thresholdVal) < 0.001,
                "!=" => Math.Abs(val - thresholdVal) >= 0.001,
                ">" => val > thresholdVal,
                "<" => val < thresholdVal,
                ">=" => val >= thresholdVal,
                "<=" => val <= thresholdVal,
                _ => false
            };
        }

        #endregion

        #region DB Persistence Helpers

        private async Task<FleetAlert?> GetActiveAlertAsync(string wsId, string alertType, CancellationToken ct)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AlertId, WorkstationId, AlertType, Severity, Message, CreatedAt, ResolvedAt, CooldownExpiresAt, Escalated, IsActive
                FROM FleetAlerts
                WHERE WorkstationId = $wsId AND AlertType = $type AND IsActive = 1;";
            cmd.Parameters.Add(CreateParam(cmd, "$wsId", wsId));
            cmd.Parameters.Add(CreateParam(cmd, "$type", alertType));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new FleetAlert
                {
                    AlertId = reader.GetString(0),
                    WorkstationId = reader.GetString(1),
                    AlertType = reader.GetString(2),
                    Severity = reader.GetString(3),
                    Message = reader.GetString(4),
                    CreatedAt = reader.GetString(5),
                    ResolvedAt = reader.GetString(6),
                    CooldownExpiresAt = reader.GetString(7),
                    Escalated = reader.GetInt32(8),
                    IsActive = reader.GetInt32(9)
                };
            }

            return null;
        }

        private async Task<FleetAlert?> GetAlertByIdAsync(string alertId, CancellationToken ct)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT AlertId, WorkstationId, AlertType, Severity, Message, CreatedAt, ResolvedAt, CooldownExpiresAt, Escalated, IsActive
                FROM FleetAlerts
                WHERE AlertId = $id;";
            cmd.Parameters.Add(CreateParam(cmd, "$id", alertId));

            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                return new FleetAlert
                {
                    AlertId = reader.GetString(0),
                    WorkstationId = reader.GetString(1),
                    AlertType = reader.GetString(2),
                    Severity = reader.GetString(3),
                    Message = reader.GetString(4),
                    CreatedAt = reader.GetString(5),
                    ResolvedAt = reader.GetString(6),
                    CooldownExpiresAt = reader.GetString(7),
                    Escalated = reader.GetInt32(8),
                    IsActive = reader.GetInt32(9)
                };
            }

            return null;
        }

        private async Task SaveAlertAsync(FleetAlert alert, CancellationToken ct)
        {
            using var connection = _databaseService.CreateConnection();
            await connection.OpenAsync(ct);

            using var transaction = await connection.BeginTransactionAsync(ct);
            try
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT OR REPLACE INTO FleetAlerts (
                        AlertId, WorkstationId, AlertType, Severity, Message,
                        CreatedAt, ResolvedAt, CooldownExpiresAt, Escalated, IsActive
                    ) VALUES (
                        $id, $wsId, $type, $severity, $msg,
                        $created, $resolved, $cooldown, $escalated, $active
                    );";

                cmd.Parameters.Add(CreateParam(cmd, "$id", alert.AlertId));
                cmd.Parameters.Add(CreateParam(cmd, "$wsId", alert.WorkstationId));
                cmd.Parameters.Add(CreateParam(cmd, "$type", alert.AlertType));
                cmd.Parameters.Add(CreateParam(cmd, "$severity", alert.Severity));
                cmd.Parameters.Add(CreateParam(cmd, "$msg", alert.Message));
                cmd.Parameters.Add(CreateParam(cmd, "$created", alert.CreatedAt));
                cmd.Parameters.Add(CreateParam(cmd, "$resolved", alert.ResolvedAt));
                cmd.Parameters.Add(CreateParam(cmd, "$cooldown", alert.CooldownExpiresAt));
                cmd.Parameters.Add(CreateParam(cmd, "$escalated", alert.Escalated));
                cmd.Parameters.Add(CreateParam(cmd, "$active", alert.IsActive));

                await cmd.ExecuteNonQueryAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogError(ex, "Failed to save FleetAlert '{AlertId}'", alert.AlertId);
                throw;
            }
        }

        private static DbParameter CreateParam(DbCommand cmd, string name, object? value)
        {
            var param = cmd.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            return param;
        }

        #endregion
    }
}

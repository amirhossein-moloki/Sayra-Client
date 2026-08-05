using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;
using Sayra.Client.Shared.Models.Phase9.Enums;

namespace Sayra.Client.Shared.Fleet.Administration.Orchestration
{
    public interface IAuditIntegrationService
    {
        Task LogActionAsync(
            string administratorId,
            string? machineId,
            string operation,
            string parameters,
            long durationMs,
            string result,
            string? failureReason,
            string ipAddress,
            string correlationId);

        Task<IReadOnlyList<AuditEntry>> QueryEntriesAsync(
            string? operatorId,
            DateTime? startDate,
            DateTime? endDate,
            int page,
            int pageSize);
    }

    public class AuditIntegrationService : IAuditIntegrationService
    {
        private readonly ConcurrentBag<AuditEntry> _entries = new();
        private long _currentEntryId = 0;

        public Task LogActionAsync(
            string administratorId,
            string? machineId,
            string operation,
            string parameters,
            long durationMs,
            string result,
            string? failureReason,
            string ipAddress,
            string correlationId)
        {
            var id = Interlocked.Increment(ref _currentEntryId);
            var entry = new AuditEntry
            {
                EntryId = id,
                CorrelationId = correlationId,
                ActionType = MapOperationToType(operation),
                Description = $"Op: {operation}, Params: {parameters}, Machine: {machineId ?? "Fleet"}, Duration: {durationMs}ms, Result: {result}, FailReason: {failureReason ?? "None"}, IP: {ipAddress}",
                OperatorId = administratorId,
                ClientIpAddress = ipAddress,
                Outcome = result.Equals("Success", StringComparison.OrdinalIgnoreCase) ? AuditResult.Success : AuditResult.Failure,
                TimestampUtc = DateTime.UtcNow
            };

            _entries.Add(entry);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEntry>> QueryEntriesAsync(
            string? operatorId,
            DateTime? startDate,
            DateTime? endDate,
            int page,
            int pageSize)
        {
            var query = _entries.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(operatorId))
            {
                query = query.Where(e => e.OperatorId.Equals(operatorId, StringComparison.OrdinalIgnoreCase));
            }

            if (startDate.HasValue)
            {
                query = query.Where(e => e.TimestampUtc >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(e => e.TimestampUtc <= endDate.Value);
            }

            var results = query
                .OrderByDescending(e => e.TimestampUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult<IReadOnlyList<AuditEntry>>(results);
        }

        private static AuditOperationType MapOperationToType(string operation)
        {
            if (operation.Contains("COMMAND", StringComparison.OrdinalIgnoreCase))
                return AuditOperationType.RemoteCommandExecution;
            if (operation.Contains("BULK", StringComparison.OrdinalIgnoreCase))
                return AuditOperationType.BulkOperation;
            if (operation.Contains("POLICY", StringComparison.OrdinalIgnoreCase))
                return AuditOperationType.PolicyChange;
            if (operation.Contains("MAINTENANCE", StringComparison.OrdinalIgnoreCase))
                return AuditOperationType.MaintenanceExecution;
            if (operation.Contains("SECURITY", StringComparison.OrdinalIgnoreCase))
                return AuditOperationType.SecurityHardeningChange;
            if (operation.Contains("SUPPORT", StringComparison.OrdinalIgnoreCase))
                return AuditOperationType.RemoteSupportSession;

            return AuditOperationType.FleetConfiguration;
        }
    }

    public interface IAdministrationNotificationService
    {
        Task PublishNotificationAsync(string type, string message);
        IReadOnlyList<NotificationRecord> GetRecentNotifications(int count);
        Task ClearAllAsync();
    }

    public class AdministrationNotificationService : IAdministrationNotificationService
    {
        private readonly ConcurrentQueue<NotificationRecord> _notifications = new();

        public Task PublishNotificationAsync(string type, string message)
        {
            var notification = new NotificationRecord
            {
                NotificationId = Guid.NewGuid().ToString("N"),
                Source = type,
                Message = message,
                Severity = MapTypeToSeverity(type),
                TimestampUtc = DateTime.UtcNow,
                IsAcknowledged = false
            };

            _notifications.Enqueue(notification);

            // Keep sliding buffer of 1000 items
            while (_notifications.Count > 1000)
            {
                _notifications.TryDequeue(out _);
            }

            return Task.CompletedTask;
        }

        public IReadOnlyList<NotificationRecord> GetRecentNotifications(int count)
        {
            return _notifications.Reverse().Take(count).ToList();
        }

        public Task ClearAllAsync()
        {
            _notifications.Clear();
            return Task.CompletedTask;
        }

        private static NotificationSeverity MapTypeToSeverity(string type)
        {
            if (type.Equals("Security Alert", StringComparison.OrdinalIgnoreCase))
                return NotificationSeverity.Emergency;
            if (type.Equals("Operation Failed", StringComparison.OrdinalIgnoreCase) || type.Equals("Policy Violation", StringComparison.OrdinalIgnoreCase))
                return NotificationSeverity.Critical;
            if (type.Equals("Maintenance Alert", StringComparison.OrdinalIgnoreCase) || type.Equals("Machine Offline", StringComparison.OrdinalIgnoreCase))
                return NotificationSeverity.Warning;

            return NotificationSeverity.Info;
        }
    }
}

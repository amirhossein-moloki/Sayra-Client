using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;

namespace SayraClient.Services.Recovery
{
    public class HealthMonitor : IHealthMonitor
    {
        private readonly ILogger<HealthMonitor> _logger;
        private readonly ConcurrentDictionary<string, SubsystemHealthInfo> _subsystems = new(StringComparer.OrdinalIgnoreCase);
        private readonly TimeSpan _heartbeatTimeout = TimeSpan.FromSeconds(30);

        public event Action<string, SubsystemHealthState, SubsystemHealthState>? SubsystemHealthStateChanged;

        public HealthMonitor(ILogger<HealthMonitor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Register known subsystems and their dependencies
            RegisterSubsystem("Database", new List<string>());
            RegisterSubsystem("AuditService", new List<string> { "Database" });
            RegisterSubsystem("RemoteCommandEngine", new List<string> { "Database", "AuditService" });
            RegisterSubsystem("PolicyEngine", new List<string> { "Database", "AuditService" });
            RegisterSubsystem("Telemetry", new List<string>());
            RegisterSubsystem("FleetManager", new List<string> { "Database" });
            RegisterSubsystem("AdvertisementEngine", new List<string> { "Database", "DownloadManager" });
            RegisterSubsystem("DownloadManager", new List<string>());
        }

        public void RegisterSubsystem(string subsystemName, List<string> dependencies)
        {
            if (string.IsNullOrWhiteSpace(subsystemName)) return;

            var info = new SubsystemHealthInfo
            {
                SubsystemName = subsystemName,
                State = SubsystemHealthState.Healthy,
                LastHeartbeat = DateTime.UtcNow,
                Dependencies = dependencies ?? new List<string>(),
                LastMessage = "Subsystem registered."
            };
            info.HealthHistory.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - Registered as Healthy.");

            _subsystems[subsystemName] = info;
            _logger.LogInformation("Registered subsystem '{SubsystemName}' with dependencies: [{Deps}]",
                subsystemName, string.Join(", ", info.Dependencies));
        }

        public Task RegisterSubsystemAsync(string subsystemName, List<string> dependencies, CancellationToken cancellationToken = default)
        {
            RegisterSubsystem(subsystemName, dependencies);
            return Task.CompletedTask;
        }

        public void ReportHeartbeat(string subsystemName)
        {
            if (!_subsystems.TryGetValue(subsystemName, out var info))
            {
                RegisterSubsystem(subsystemName, new List<string>());
                _subsystems.TryGetValue(subsystemName, out info);
            }

            if (info != null)
            {
                var previousState = info.State;
                info.LastHeartbeat = DateTime.UtcNow;

                if (info.State == SubsystemHealthState.Offline)
                {
                    TransitionState(info, SubsystemHealthState.Healthy, "Subsystem came back online via heartbeat.");
                }
            }
        }

        public Task ReportHeartbeatAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            ReportHeartbeat(subsystemName);
            return Task.CompletedTask;
        }

        public void ReportSubsystemState(string subsystemName, SubsystemHealthState state, string message, string? exceptionDetails = null)
        {
            if (!_subsystems.TryGetValue(subsystemName, out var info))
            {
                RegisterSubsystem(subsystemName, new List<string>());
                _subsystems.TryGetValue(subsystemName, out info);
            }

            if (info != null)
            {
                info.LastMessage = message;
                info.LastException = exceptionDetails;
                TransitionState(info, state, message);
            }
        }

        public Task ReportSubsystemStateAsync(string subsystemName, SubsystemHealthState state, string message, string? exceptionDetails = null, CancellationToken cancellationToken = default)
        {
            ReportSubsystemState(subsystemName, state, message, exceptionDetails);
            return Task.CompletedTask;
        }

        public SubsystemHealthState GetSubsystemHealth(string subsystemName)
        {
            EvaluateSubsystemState(subsystemName);
            return _subsystems.TryGetValue(subsystemName, out var info) ? info.State : SubsystemHealthState.Offline;
        }

        public Task<SubsystemHealthState> GetSubsystemHealthAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetSubsystemHealth(subsystemName));
        }

        public IReadOnlyDictionary<string, SubsystemHealthInfo> GetDetailedHealth()
        {
            foreach (var key in _subsystems.Keys)
            {
                EvaluateSubsystemState(key);
            }
            return _subsystems.ToDictionary(k => k.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyDictionary<string, SubsystemHealthInfo>> GetDetailedHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(GetDetailedHealth());
        }

        public bool RunHealthCheck(string subsystemName)
        {
            _logger.LogInformation("Executing health check for subsystem '{SubsystemName}'...", subsystemName);
            if (!_subsystems.TryGetValue(subsystemName, out var info))
            {
                return false;
            }

            // Check heartbeats
            var age = DateTime.UtcNow - info.LastHeartbeat;
            if (age > _heartbeatTimeout && info.State != SubsystemHealthState.Offline)
            {
                ReportSubsystemState(subsystemName, SubsystemHealthState.Warning, $"Subsystem heartbeat is stale. Last heartbeat was {age.TotalSeconds:F1} seconds ago.");
                return false;
            }

            // Check dependencies
            foreach (var dep in info.Dependencies)
            {
                var depState = GetSubsystemHealth(dep);
                if (depState == SubsystemHealthState.Offline || depState == SubsystemHealthState.Critical)
                {
                    ReportSubsystemState(subsystemName, SubsystemHealthState.Critical, $"Critical dependency '{dep}' is unhealthy (State: {depState}).");
                    return false;
                }
            }

            return true;
        }

        public Task<bool> RunHealthCheckAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(RunHealthCheck(subsystemName));
        }

        private void EvaluateSubsystemState(string subsystemName)
        {
            if (!_subsystems.TryGetValue(subsystemName, out var info)) return;

            // Heartbeat check
            var age = DateTime.UtcNow - info.LastHeartbeat;
            if (age > _heartbeatTimeout && info.State == SubsystemHealthState.Healthy)
            {
                TransitionState(info, SubsystemHealthState.Warning, $"Heartbeat expired. Last heartbeat was {age.TotalSeconds:F1}s ago.");
            }

            // Dependency propagation check
            foreach (var dep in info.Dependencies)
            {
                if (_subsystems.TryGetValue(dep, out var depInfo))
                {
                    if (depInfo.State == SubsystemHealthState.Offline && info.State != SubsystemHealthState.Offline)
                    {
                        TransitionState(info, SubsystemHealthState.Critical, $"Dependency '{dep}' is Offline.");
                    }
                    else if (depInfo.State == SubsystemHealthState.Critical && info.State == SubsystemHealthState.Healthy)
                    {
                        TransitionState(info, SubsystemHealthState.Warning, $"Dependency '{dep}' has Critical failure.");
                    }
                }
            }
        }

        private void TransitionState(SubsystemHealthInfo info, SubsystemHealthState newState, string reason)
        {
            var oldState = info.State;
            if (oldState == newState) return;

            info.State = newState;
            var historyEntry = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - State: {oldState} -> {newState}. Reason: {reason}";

            info.AddHistoryEntry(historyEntry);

            _logger.LogWarning("Subsystem '{SubsystemName}' health state transitioned: {OldState} -> {NewState}. Reason: {Reason}",
                info.SubsystemName, oldState, newState, reason);

            try
            {
                SubsystemHealthStateChanged?.Invoke(info.SubsystemName, oldState, newState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred dispatching health state transition event for '{SubsystemName}'", info.SubsystemName);
            }
        }
    }
}

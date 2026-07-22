using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

/// <summary>
/// Production-ready thread-safe implementation of the health monitoring service.
/// </summary>
public class ServiceHealthMonitor : IServiceHealthMonitor
{
    private readonly ILogger<ServiceHealthMonitor> _logger;
    private readonly ConcurrentDictionary<string, ServiceHealthInfo> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _hungHeartbeatTimeout = TimeSpan.FromSeconds(60);

    public event Action<string, ServiceHealthState, ServiceHealthState>? HealthStateChanged;

    public ServiceHealthMonitor(ILogger<ServiceHealthMonitor> logger)
    {
        _logger = logger;
    }

    public void ReportHeartbeat(string serviceName)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return;

        _states.AddOrUpdate(serviceName,
            name =>
            {
                var info = new ServiceHealthInfo
                {
                    ServiceName = name,
                    State = ServiceHealthState.Healthy,
                    LastHeartbeat = DateTime.UtcNow,
                    LastMessage = "Service started and reported first heartbeat."
                };
                _logger.LogInformation("Service {ServiceName} registered as Healthy via heartbeat.", name);
                NotifyStateChanged(name, ServiceHealthState.Starting, ServiceHealthState.Healthy);
                return info;
            },
            (name, existing) =>
            {
                var previousState = existing.State;
                existing.LastHeartbeat = DateTime.UtcNow;

                // Auto-recover if it was degraded/failed/starting
                if (existing.State != ServiceHealthState.Healthy && existing.State != ServiceHealthState.Stopped)
                {
                    existing.State = ServiceHealthState.Recovering;
                    _logger.LogInformation("Service {ServiceName} is recovering to Healthy via heartbeat.", name);
                    NotifyStateChanged(name, previousState, ServiceHealthState.Recovering);
                    existing.State = ServiceHealthState.Healthy;
                    NotifyStateChanged(name, ServiceHealthState.Recovering, ServiceHealthState.Healthy);
                }

                return existing;
            });
    }

    public void ReportState(string serviceName, ServiceHealthState state, string? message = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return;

        _states.AddOrUpdate(serviceName,
            name =>
            {
                var info = new ServiceHealthInfo
                {
                    ServiceName = name,
                    State = state,
                    LastHeartbeat = DateTime.UtcNow,
                    LastMessage = message ?? $"Service state set to {state}."
                };
                _logger.LogInformation("Service {ServiceName} registered with state {State}. Message: {Message}", name, state, info.LastMessage);
                NotifyStateChanged(name, ServiceHealthState.Starting, state);
                return info;
            },
            (name, existing) =>
            {
                var previousState = existing.State;
                if (previousState != state)
                {
                    existing.State = state;
                    existing.LastMessage = message ?? $"Service state changed to {state}.";
                    _logger.LogInformation("Service {ServiceName} changed state: {PreviousState} -> {State}. Message: {Message}", name, previousState, state, existing.LastMessage);
                    NotifyStateChanged(name, previousState, state);
                }
                return existing;
            });
    }

    public void ReportFailure(string serviceName, Exception exception, string? message = null)
    {
        if (string.IsNullOrWhiteSpace(serviceName)) return;

        _states.AddOrUpdate(serviceName,
            name =>
            {
                var info = new ServiceHealthInfo
                {
                    ServiceName = name,
                    State = ServiceHealthState.Failed,
                    LastHeartbeat = DateTime.UtcNow,
                    FailureCount = 1,
                    LastMessage = message ?? $"Failure reported: {exception.Message}",
                    LastException = exception
                };
                _logger.LogError(exception, "Service {ServiceName} registered as Failed on startup/first report.", name);
                NotifyStateChanged(name, ServiceHealthState.Starting, ServiceHealthState.Failed);
                return info;
            },
            (name, existing) =>
            {
                var previousState = existing.State;
                existing.FailureCount++;
                existing.LastMessage = message ?? $"Failure reported: {exception.Message}";
                existing.LastException = exception;
                existing.State = ServiceHealthState.Failed;

                _logger.LogError(exception, "Service {ServiceName} failed. Total crash count: {Count}. Message: {Message}", name, existing.FailureCount, existing.LastMessage);

                if (previousState != ServiceHealthState.Failed)
                {
                    NotifyStateChanged(name, previousState, ServiceHealthState.Failed);
                }
                return existing;
            });
    }

    public ServiceHealthState GetOverallHealth()
    {
        EvaluateHungWorkers();

        if (_states.IsEmpty) return ServiceHealthState.Healthy;

        var states = _states.Values.ToList();

        if (states.Any(s => s.State == ServiceHealthState.Failed))
        {
            return ServiceHealthState.Failed;
        }

        if (states.Any(s => s.State == ServiceHealthState.Degraded))
        {
            return ServiceHealthState.Degraded;
        }

        if (states.Any(s => s.State == ServiceHealthState.Recovering))
        {
            return ServiceHealthState.Recovering;
        }

        if (states.Any(s => s.State == ServiceHealthState.Starting))
        {
            return ServiceHealthState.Starting;
        }

        if (states.All(s => s.State == ServiceHealthState.Stopped))
        {
            return ServiceHealthState.Stopped;
        }

        return ServiceHealthState.Healthy;
    }

    public IReadOnlyDictionary<string, ServiceHealthInfo> GetDetailedHealth()
    {
        EvaluateHungWorkers();
        return _states.ToDictionary(kvp => kvp.Key, kvp => new ServiceHealthInfo
        {
            ServiceName = kvp.Value.ServiceName,
            State = kvp.Value.State,
            LastHeartbeat = kvp.Value.LastHeartbeat,
            FailureCount = kvp.Value.FailureCount,
            LastMessage = kvp.Value.LastMessage,
            LastException = kvp.Value.LastException
        }, StringComparer.OrdinalIgnoreCase);
    }

    private void EvaluateHungWorkers()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _states)
        {
            var info = kvp.Value;
            if (info.State == ServiceHealthState.Healthy && (now - info.LastHeartbeat) > _hungHeartbeatTimeout)
            {
                var previousState = info.State;
                info.State = ServiceHealthState.Degraded;
                info.LastMessage = $"No heartbeat received since {info.LastHeartbeat} (Timeout: {_hungHeartbeatTimeout.TotalSeconds}s). Service is considered hung.";
                _logger.LogWarning("Service {ServiceName} is hung! Last heartbeat was at {LastHeartbeat}.", info.ServiceName, info.LastHeartbeat);
                NotifyStateChanged(info.ServiceName, previousState, ServiceHealthState.Degraded);
            }
        }
    }

    private void NotifyStateChanged(string serviceName, ServiceHealthState oldState, ServiceHealthState newState)
    {
        try
        {
            HealthStateChanged?.Invoke(serviceName, oldState, newState);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred executing HealthStateChanged event handlers for {ServiceName}.", serviceName);
        }
    }
}

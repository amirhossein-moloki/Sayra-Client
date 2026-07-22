using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class HeartbeatManager : IHeartbeatManager, IDisposable
{
    private readonly ILogger<HeartbeatManager> _logger;
    private readonly TcpClientManager _tcpClientManager;
    private readonly ClientStateManager _stateManager;
    private readonly IServiceHealthMonitor _healthMonitor;

    private TimeSpan _interval;
    private readonly HeartbeatStats _stats = new();
    private readonly object _statsLock = new();

    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private bool _isRunning;
    private int _consecutiveMissedLimit = 3;
    private bool _waitingForAck;

    public event Action<DateTime>? HeartbeatSent;
    public event Action<DateTime>? HeartbeatReceived;
    public event Action<string>? HeartbeatFailed;

    public HeartbeatManager(
        ILogger<HeartbeatManager> logger,
        TcpClientManager tcpClientManager,
        ClientStateManager stateManager,
        IServiceHealthMonitor healthMonitor,
        IConfiguration configuration)
    {
        _logger = logger;
        _tcpClientManager = tcpClientManager;
        _stateManager = stateManager;
        _healthMonitor = healthMonitor;

        int seconds = int.Parse(configuration["ServerConfig:HeartbeatIntervalSeconds"] ?? "10");
        _interval = TimeSpan.FromSeconds(seconds);
        _stats.IsConnected = false;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Heartbeat Manager with interval: {Interval}s...", _interval.TotalSeconds);

        lock (_statsLock)
        {
            if (_isRunning) return Task.CompletedTask;
            _isRunning = true;
        }

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = RunHeartbeatLoopAsync(_loopCts.Token);

        _healthMonitor.ReportState("HeartbeatManager", ServiceHealthState.Healthy, "Heartbeat loop started.");

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _logger.LogInformation("Stopping Heartbeat Manager...");

        lock (_statsLock)
        {
            if (!_isRunning) return;
            _isRunning = false;
        }

        if (_loopCts != null)
        {
            _loopCts.Cancel();
            _loopCts.Dispose();
            _loopCts = null;
        }

        if (_loopTask != null)
        {
            try
            {
                await _loopTask;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error stopping heartbeat loop task.");
            }
            _loopTask = null;
        }

        _healthMonitor.ReportState("HeartbeatManager", ServiceHealthState.Stopped, "Heartbeat loop stopped.");
    }

    public void RecordExternalHeartbeatAck()
    {
        lock (_statsLock)
        {
            _waitingForAck = false;
            _stats.ReceivedCount++;
            _stats.LastReceivedTime = DateTime.UtcNow;
            _stats.ConsecutiveMissedCount = 0;
            _stats.IsConnected = true;
            UpdateReliability();
        }

        _logger.LogDebug("External heartbeat ACK received.");
        HeartbeatReceived?.Invoke(DateTime.UtcNow);

        _healthMonitor.ReportHeartbeat("HeartbeatManager");
    }

    public void SetInterval(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero) throw new ArgumentException("Interval must be positive.", nameof(interval));
        _logger.LogInformation("Heartbeat interval updated to: {Interval}s", interval.TotalSeconds);
        lock (_statsLock)
        {
            _interval = interval;
        }
    }

    public TimeSpan GetInterval()
    {
        lock (_statsLock)
        {
            return _interval;
        }
    }

    public HeartbeatStats GetStats()
    {
        lock (_statsLock)
        {
            return new HeartbeatStats
            {
                SentCount = _stats.SentCount,
                ReceivedCount = _stats.ReceivedCount,
                LastSentTime = _stats.LastSentTime,
                LastReceivedTime = _stats.LastReceivedTime,
                ConnectionReliability = _stats.ConnectionReliability,
                IsConnected = _stats.IsConnected,
                ConsecutiveMissedCount = _stats.ConsecutiveMissedCount
            };
        }
    }

    private async Task RunHeartbeatLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Always report internal health so we know the loop is alive
                _healthMonitor.ReportHeartbeat("HeartbeatManager");

                // Evaluate missed heartbeat if we were waiting for an ACK and didn't get it
                EvaluateMissedHeartbeat();

                if (_stateManager.IsReady())
                {
                    _logger.LogDebug("Sending external heartbeat to server.");

                    var heartbeat = new
                    {
                        type = "HEARTBEAT",
                        timestamp = DateTime.UtcNow
                    };

                    lock (_statsLock)
                    {
                        _stats.SentCount++;
                        _stats.LastSentTime = DateTime.UtcNow;
                        _waitingForAck = true;
                    }

                    HeartbeatSent?.Invoke(DateTime.UtcNow);

                    try
                    {
                        await _tcpClientManager.SendMessageAsync(heartbeat, token);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Failed to send heartbeat over TCP transport.");
                        HandleHeartbeatFailure("TCP Transport failed to send message.");
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Exception in heartbeat loop iteration.");
                HandleHeartbeatFailure(ex.Message);
            }

            TimeSpan currentInterval;
            lock (_statsLock)
            {
                currentInterval = _interval;
            }

            try
            {
                await Task.Delay(currentInterval, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void EvaluateMissedHeartbeat()
    {
        lock (_statsLock)
        {
            if (_waitingForAck)
            {
                _stats.ConsecutiveMissedCount++;
                _waitingForAck = false;
                _stats.IsConnected = false;
                UpdateReliability();

                _logger.LogWarning("Missed heartbeat ACK. Consecutive missed: {Count}/{Limit}", _stats.ConsecutiveMissedCount, _consecutiveMissedLimit);
                HeartbeatFailed?.Invoke($"Heartbeat ACK timeout. Missed: {_stats.ConsecutiveMissedCount}");

                if (_stats.ConsecutiveMissedCount >= _consecutiveMissedLimit)
                {
                    _logger.LogError("CRITICAL: Heartbeat timeout exceeded! Connection declared unhealthy.");
                    _healthMonitor.ReportState("HeartbeatManager", ServiceHealthState.Degraded, $"Connection degraded: {_stats.ConsecutiveMissedCount} missed heartbeats.");

                    // Trigger transport-level reconnect or state update
                    _ = Task.Run(() => TriggerConnectionRecovery());
                }
            }
        }
    }

    private void HandleHeartbeatFailure(string reason)
    {
        lock (_statsLock)
        {
            _stats.ConsecutiveMissedCount++;
            _stats.IsConnected = false;
            UpdateReliability();
        }

        _logger.LogWarning("Heartbeat failure: {Reason}. Consecutive missed: {Count}", reason, _stats.ConsecutiveMissedCount);
        HeartbeatFailed?.Invoke(reason);
        _healthMonitor.ReportState("HeartbeatManager", ServiceHealthState.Degraded, $"Heartbeat send failure: {reason}");
    }

    private void TriggerConnectionRecovery()
    {
        try
        {
            _logger.LogWarning("Triggering transport connection recovery...");
            // We can gracefully close current TCP socket so ReconnectManager handles rebuilding it
            _tcpClientManager.Disconnect();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger connection recovery.");
        }
    }

    private void UpdateReliability()
    {
        if (_stats.SentCount == 0)
        {
            _stats.ConnectionReliability = 100.0;
            return;
        }
        _stats.ConnectionReliability = Math.Round(((double)_stats.ReceivedCount / _stats.SentCount) * 100.0, 2);
    }

    public void Dispose()
    {
        _loopCts?.Dispose();
    }
}

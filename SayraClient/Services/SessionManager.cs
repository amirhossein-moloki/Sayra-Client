using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SayraClient.Models;
using System.Text.Json;
using System.Timers;
using Timer = System.Timers.Timer;

namespace SayraClient.Services;

public class SessionManager : IDisposable
{
    private readonly ILogger<SessionManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly KioskManager _kioskManager;
    private SessionModel? _currentSession;
    private readonly Timer _sessionTimer;
    private readonly string _sessionFilePath = Path.Combine(AppContext.BaseDirectory, "session_state.json");
    private readonly object _sessionLock = new();

    public string CurrentStatus => _currentSession?.Status ?? "IDLE";

    public SessionManager(ILogger<SessionManager> logger, IServiceProvider serviceProvider, KioskManager kioskManager)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _kioskManager = kioskManager;
        _sessionTimer = new Timer(1000); // 1 second interval
        _sessionTimer.Elapsed += OnTimerElapsed;

        LoadSessionState();
    }

    public ExecutionResult StartSession(SessionModel session)
    {
        lock (_sessionLock)
        {
            if (_currentSession != null && _currentSession.Status != "ENDED")
            {
                return ExecutionResult.Error("START_SESSION", "A session is already active or paused.", session.PcId);
            }

            _currentSession = session;
            _currentSession.Status = "ACTIVE";
            if (_currentSession.StartTime == default) _currentSession.StartTime = DateTime.UtcNow;

            _kioskManager.Lockdown();
            SaveSessionState();
            _sessionTimer.Start();

            _logger.LogInformation("Session {id} started for PC {pcId}. Duration: {duration} min",
                session.SessionId, session.PcId, session.Duration);

            return ExecutionResult.Success("START_SESSION", "Session started", session.PcId);
        }
    }

    public ExecutionResult StopSession(string pcId)
    {
        lock (_sessionLock)
        {
            if (_currentSession == null)
            {
                return ExecutionResult.Error("STOP_SESSION", "No session is currently active.", pcId);
            }

            _currentSession.Status = "ENDED";
            _currentSession.EndTime = DateTime.UtcNow;
            _sessionTimer.Stop();
            _kioskManager.Unlock();
            SaveSessionState();

            _logger.LogInformation("Session {id} stopped.", _currentSession.SessionId);

            return ExecutionResult.Success("STOP_SESSION", "Session stopped", pcId);
        }
    }

    public ExecutionResult PauseSession(string pcId)
    {
        lock (_sessionLock)
        {
            if (_currentSession == null || _currentSession.Status != "ACTIVE")
            {
                return ExecutionResult.Error("PAUSE_SESSION", "Session is not active.", pcId);
            }

            _currentSession.Status = "PAUSED";
            _sessionTimer.Stop();
            SaveSessionState();

            _logger.LogInformation("Session {id} paused.", _currentSession.SessionId);

            return ExecutionResult.Success("PAUSE_SESSION", "Session paused", pcId);
        }
    }

    public ExecutionResult ResumeSession(string pcId)
    {
        lock (_sessionLock)
        {
            if (_currentSession == null || _currentSession.Status != "PAUSED")
            {
                return ExecutionResult.Error("RESUME_SESSION", "Session is not paused.", pcId);
            }

            _currentSession.Status = "ACTIVE";
            _sessionTimer.Start();
            SaveSessionState();

            _logger.LogInformation("Session {id} resumed.", _currentSession.SessionId);

            return ExecutionResult.Success("RESUME_SESSION", "Session resumed", pcId);
        }
    }

    private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            SessionModel? sessionToProcess = null;
            lock (_sessionLock)
            {
                if (_currentSession != null && _currentSession.Status == "ACTIVE")
                {
                    _currentSession.ElapsedSeconds += 1;
                    sessionToProcess = _currentSession;

                    // Periodically save state (e.g., every 30 seconds) to avoid data loss on crash
                    if (((int)_currentSession.ElapsedSeconds) % 30 == 0)
                    {
                        SaveSessionState();
                    }
                }
            }

            if (sessionToProcess != null)
            {
                double totalAllowedSeconds = sessionToProcess.Duration * 60;
                if (sessionToProcess.ElapsedSeconds >= totalAllowedSeconds)
                {
                    await EndSessionDueToTimeout();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in session timer execution.");
        }
    }

    private async Task EndSessionDueToTimeout()
    {
        _logger.LogInformation("Session timeout reached.");
        StopSession(_currentSession?.PcId ?? "Unknown");

        var timeoutEvent = new
        {
            type = "EVENT",
            @event = "SESSION_ENDED",
            reason = "TIME_EXPIRED",
            pcId = _currentSession?.PcId
        };

        using var scope = _serviceProvider.CreateScope();
        var networkManager = scope.ServiceProvider.GetRequiredService<TcpClientManager>();
        await networkManager.SendMessageAsync(timeoutEvent, CancellationToken.None);
    }

    private void SaveSessionState()
    {
        try
        {
            if (_currentSession == null) return;
            string json = JsonSerializer.Serialize(_currentSession);
            File.WriteAllText(_sessionFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save session state.");
        }
    }

    private void LoadSessionState()
    {
        try
        {
            if (File.Exists(_sessionFilePath))
            {
                string json = File.ReadAllText(_sessionFilePath);
                _currentSession = JsonSerializer.Deserialize<SessionModel>(json);

                if (_currentSession != null && _currentSession.Status == "ACTIVE")
                {
                    _logger.LogInformation("Restoring active session {id}", _currentSession.SessionId);
                    _sessionTimer.Start();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load session state.");
        }
    }

    public SessionModel? GetCurrentSession()
    {
        lock (_sessionLock)
        {
            if (_currentSession == null) return null;
            return new SessionModel
            {
                SessionId = _currentSession.SessionId,
                PcId = _currentSession.PcId,
                SiteId = _currentSession.SiteId,
                StartTime = _currentSession.StartTime,
                EndTime = _currentSession.EndTime,
                Duration = _currentSession.Duration,
                Status = _currentSession.Status,
                CurrentCost = _currentSession.CurrentCost,
                RatePerHour = _currentSession.RatePerHour,
                ElapsedSeconds = _currentSession.ElapsedSeconds
            };
        }
    }

    public void Dispose()
    {
        _sessionTimer.Dispose();
    }
}

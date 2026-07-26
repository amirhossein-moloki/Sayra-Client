using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Domain.States;

namespace Sayra.Client.Shared.Runtime.Application.Services
{
    public class SessionTimerService : ISessionTimerService, IDisposable
    {
        private readonly ILogger<SessionTimerService> _logger;
        private readonly ConcurrentDictionary<Guid, TimerState> _trackedSessions = new();
        private readonly Timer _globalTimer;

        public event Action<SessionWarningEvent>? WarningTriggered;
        public event Action<Guid>? ExpirationTriggered;

        public SessionTimerService(ILogger<SessionTimerService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _globalTimer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        public void StartTracking(Guid sessionId, TimeSpan totalTime, TimeSpan warningThreshold1, TimeSpan warningThreshold2)
        {
            var state = new TimerState(sessionId, totalTime, warningThreshold1, warningThreshold2);
            _trackedSessions[sessionId] = state;
            _logger.LogInformation("Started timer tracking for session {SessionId}. Total Time: {TotalTime}", sessionId, totalTime);
        }

        public void StopTracking(Guid sessionId)
        {
            if (_trackedSessions.TryRemove(sessionId, out _))
            {
                _logger.LogInformation("Stopped timer tracking for session {SessionId}", sessionId);
            }
        }

        public TimeSpan GetRemainingTime(Guid sessionId)
        {
            if (_trackedSessions.TryGetValue(sessionId, out var state))
            {
                var elapsed = DateTime.UtcNow - state.StartTime;
                var remaining = state.TotalTime - elapsed;
                return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
            }
            return TimeSpan.Zero;
        }

        public TimeSpan GetElapsedTime(Guid sessionId)
        {
            if (_trackedSessions.TryGetValue(sessionId, out var state))
            {
                return DateTime.UtcNow - state.StartTime;
            }
            return TimeSpan.Zero;
        }

        private void OnTimerTick(object? state)
        {
            foreach (var kvp in _trackedSessions)
            {
                var sessionId = kvp.Key;
                var timerState = kvp.Value;

                var elapsed = DateTime.UtcNow - timerState.StartTime;
                var remaining = timerState.TotalTime - elapsed;

                if (remaining <= TimeSpan.Zero)
                {
                    if (!timerState.IsExpired)
                    {
                        timerState.IsExpired = true;
                        _logger.LogInformation("Session {SessionId} timer expired.", sessionId);
                        ExpirationTriggered?.Invoke(sessionId);
                    }
                    continue;
                }

                // Check warning thresholds
                if (remaining <= timerState.WarningThreshold2)
                {
                    if (!timerState.HasTriggeredWarning2)
                    {
                        timerState.HasTriggeredWarning2 = true;
                        var warningEvent = new SessionWarningEvent(sessionId, "User", RuntimeState.Warning, "Time remaining warning level 2", remaining, 2);
                        WarningTriggered?.Invoke(warningEvent);
                    }
                }
                else if (remaining <= timerState.WarningThreshold1)
                {
                    if (!timerState.HasTriggeredWarning1)
                    {
                        timerState.HasTriggeredWarning1 = true;
                        var warningEvent = new SessionWarningEvent(sessionId, "User", RuntimeState.Warning, "Time remaining warning level 1", remaining, 1);
                        WarningTriggered?.Invoke(warningEvent);
                    }
                }
            }
        }

        public void Dispose()
        {
            _globalTimer.Dispose();
        }

        public class TimerState
        {
            public Guid SessionId { get; }
            public TimeSpan TotalTime { get; }
            public TimeSpan WarningThreshold1 { get; }
            public TimeSpan WarningThreshold2 { get; }
            public DateTime StartTime { get; set; } = DateTime.UtcNow;

            public bool HasTriggeredWarning1 { get; set; }
            public bool HasTriggeredWarning2 { get; set; }
            public bool IsExpired { get; set; }

            public TimerState(Guid sessionId, TimeSpan totalTime, TimeSpan warningThreshold1, TimeSpan warningThreshold2)
            {
                SessionId = sessionId;
                TotalTime = totalTime;
                WarningThreshold1 = warningThreshold1;
                WarningThreshold2 = warningThreshold2;
            }
        }
    }
}

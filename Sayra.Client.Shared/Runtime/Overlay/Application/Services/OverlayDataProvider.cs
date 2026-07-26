using System;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Runtime.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Domain.Events;
using Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Services
{
    /// <summary>
    /// Translates global billing and session events into view-agnostic OverlayData models.
    /// </summary>
    public class OverlayDataProvider : IOverlayDataProvider
    {
        private readonly ILogger<OverlayDataProvider> _logger;
        private readonly object _lock = new();
        private OverlayData _currentData = new();

        public event Action<OverlayData>? DataUpdated;

        public OverlayData CurrentData
        {
            get
            {
                lock (_lock)
                {
                    return _currentData;
                }
            }
        }

        public OverlayDataProvider(ILogger<OverlayDataProvider> logger, IRuntimeEventPublisher? eventPublisher = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            if (eventPublisher != null)
            {
                _logger.LogInformation("OverlayDataProvider: Subscribing to runtime session events...");
                eventPublisher.Subscribe<SessionStartedEvent>(ev => HandleSessionStarted(ev.SessionId, ev.UserId));
                eventPublisher.Subscribe<SessionWarningEvent>(ev => HandleSessionWarning(ev.SessionId, ev.UserId, ev.RemainingTime, ev.WarningLevel, ev.Details));
                eventPublisher.Subscribe<SessionExpiredEvent>(ev => HandleSessionExpired(ev.SessionId));
                eventPublisher.Subscribe<SessionCompletedEvent>(ev => HandleSessionCompleted(ev.SessionId));
            }
        }

        public void HandleSessionStarted(Guid sessionId, string userId)
        {
            lock (_lock)
            {
                _currentData = new OverlayData
                {
                    SessionId = sessionId,
                    RemainingTime = TimeSpan.FromMinutes(120), // Placeholder value, will be updated dynamically via ticks/warnings
                    SessionState = "Running",
                    WarningLevel = 0,
                    Message = $"Session active for user {userId}",
                    Visibility = true
                };
            }
            _logger.LogInformation("OverlayDataProvider: Processed SessionStartedEvent for SessionId {SessionId}", sessionId);
            NotifyUpdated();
        }

        public void HandleSessionWarning(Guid sessionId, string userId, TimeSpan remainingTime, int warningLevel, string message)
        {
            lock (_lock)
            {
                _currentData = new OverlayData
                {
                    SessionId = sessionId,
                    RemainingTime = remainingTime,
                    SessionState = "Warning",
                    WarningLevel = warningLevel,
                    Message = string.IsNullOrWhiteSpace(message) ? $"Warning: {remainingTime:mm\\:ss} remaining!" : message,
                    Visibility = true
                };
            }
            _logger.LogInformation("OverlayDataProvider: Processed SessionWarningEvent (Level {Level}) for SessionId {SessionId}", warningLevel, sessionId);
            NotifyUpdated();
        }

        public void HandleSessionExpired(Guid sessionId)
        {
            lock (_lock)
            {
                _currentData = new OverlayData
                {
                    SessionId = sessionId,
                    RemainingTime = TimeSpan.Zero,
                    SessionState = "Expired",
                    WarningLevel = 3,
                    Message = "Session expired! Please top up.",
                    Visibility = true
                };
            }
            _logger.LogInformation("OverlayDataProvider: Processed SessionExpiredEvent for SessionId {SessionId}", sessionId);
            NotifyUpdated();
        }

        public void HandleSessionCompleted(Guid sessionId)
        {
            lock (_lock)
            {
                _currentData = new OverlayData
                {
                    SessionId = sessionId,
                    RemainingTime = TimeSpan.Zero,
                    SessionState = "Completed",
                    WarningLevel = 0,
                    Message = "Session ended cleanly.",
                    Visibility = false
                };
            }
            _logger.LogInformation("OverlayDataProvider: Processed SessionCompletedEvent for SessionId {SessionId}", sessionId);
            NotifyUpdated();
        }

        private void NotifyUpdated()
        {
            OverlayData dataCopy;
            lock (_lock)
            {
                dataCopy = new OverlayData
                {
                    SessionId = _currentData.SessionId,
                    RemainingTime = _currentData.RemainingTime,
                    SessionState = _currentData.SessionState,
                    WarningLevel = _currentData.WarningLevel,
                    Message = _currentData.Message,
                    Visibility = _currentData.Visibility
                };
            }
            DataUpdated?.Invoke(dataCopy);
        }
    }
}

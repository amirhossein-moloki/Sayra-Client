using System;
using Sayra.Client.Shared.Runtime.Overlay.Domain.Models;

namespace Sayra.Client.Shared.Runtime.Overlay.Application.Interfaces
{
    /// <summary>
    /// Contract defining the overlay data translator and event listener interface.
    /// </summary>
    public interface IOverlayDataProvider
    {
        event Action<OverlayData>? DataUpdated;
        OverlayData CurrentData { get; }
        void HandleSessionStarted(Guid sessionId, string userId);
        void HandleSessionWarning(Guid sessionId, string userId, TimeSpan remainingTime, int warningLevel, string message);
        void HandleSessionExpired(Guid sessionId);
        void HandleSessionCompleted(Guid sessionId);
    }
}

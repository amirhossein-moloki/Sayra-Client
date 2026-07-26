using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.Domain.Events;

namespace Sayra.Client.Shared.Runtime.Application.Interfaces
{
    public interface ISessionTimerService
    {
        event Action<SessionWarningEvent> WarningTriggered;
        event Action<Guid> ExpirationTriggered;

        void StartTracking(Guid sessionId, TimeSpan totalTime);
        void StartTracking(Guid sessionId, TimeSpan totalTime, TimeSpan warningThreshold1, TimeSpan warningThreshold2);
        void StopTracking(Guid sessionId);
        TimeSpan GetRemainingTime(Guid sessionId);
        TimeSpan GetElapsedTime(Guid sessionId);
    }
}

using System;
using System.Threading.Tasks;

namespace Sayra.Client.Shared.Runtime.Application.Interfaces
{
    public interface IIdleDetectionService
    {
        bool IsIdle { get; }
        TimeSpan IdleDuration { get; }
        event Action<bool> IdleStateChanged;

        void SimulateInactivity(TimeSpan duration);
        void ResetActivity();
    }
}

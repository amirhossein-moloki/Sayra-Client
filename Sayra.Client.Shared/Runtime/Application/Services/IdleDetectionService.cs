using System;
using Sayra.Client.Shared.Runtime.Application.Interfaces;

namespace Sayra.Client.Shared.Runtime.Application.Services
{
    public class IdleDetectionService : IIdleDetectionService
    {
        private bool _isIdle;
        private TimeSpan _idleDuration = TimeSpan.Zero;
        private readonly TimeSpan _idleThreshold = TimeSpan.FromMinutes(10);

        public bool IsIdle => _isIdle;
        public TimeSpan IdleDuration => _idleDuration;

        public event Action<bool>? IdleStateChanged;

        public void SimulateInactivity(TimeSpan duration)
        {
            _idleDuration = duration;
            bool wasIdle = _isIdle;
            _isIdle = _idleDuration >= _idleThreshold;

            if (wasIdle != _isIdle)
            {
                IdleStateChanged?.Invoke(_isIdle);
            }
        }

        public void ResetActivity()
        {
            _idleDuration = TimeSpan.Zero;
            if (_isIdle)
            {
                _isIdle = false;
                IdleStateChanged?.Invoke(false);
            }
        }
    }
}

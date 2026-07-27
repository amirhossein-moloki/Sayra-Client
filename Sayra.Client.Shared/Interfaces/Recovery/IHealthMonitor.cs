using System;
using System.Collections.Generic;
using Sayra.Client.Shared.Models.Recovery;

namespace Sayra.Client.Shared.Interfaces.Recovery
{
    public interface IHealthMonitor
    {
        event Action<string, SubsystemHealthState, SubsystemHealthState>? SubsystemHealthStateChanged;
        void ReportHeartbeat(string subsystemName);
        void ReportSubsystemState(string subsystemName, SubsystemHealthState state, string message, string? exceptionDetails = null);
        SubsystemHealthState GetSubsystemHealth(string subsystemName);
        IReadOnlyDictionary<string, SubsystemHealthInfo> GetDetailedHealth();
        bool RunHealthCheck(string subsystemName);
        void RegisterSubsystem(string subsystemName, List<string> dependencies);
    }
}

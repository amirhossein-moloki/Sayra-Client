using System;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces
{
    public interface IJobObjectManager : IDisposable
    {
        void CreateJob(Guid runtimeId);
        void AssignProcess(Guid runtimeId, int processId);
        void ConfigureLimits(Guid runtimeId, long maxMemoryBytes, ulong cpuAffinityMask);
        void TerminateJob(Guid runtimeId);
    }
}

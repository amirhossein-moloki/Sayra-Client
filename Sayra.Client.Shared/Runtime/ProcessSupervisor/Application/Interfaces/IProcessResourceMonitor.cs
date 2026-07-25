using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces
{
    public interface IProcessResourceMonitor
    {
        Task<ResourceMetrics> MonitorMetricsAsync(int processId);
    }
}

using System;
using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces
{
    public interface IProcessSupervisor
    {
        Task RegisterAsync(ProcessInfo process);
        Task StopAsync(Guid runtimeId);
        Task<ProcessStatus> GetStatusAsync(Guid runtimeId);
    }
}

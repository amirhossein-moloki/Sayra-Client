using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.Shared.Runtime.ProcessSupervisor.Domain.Models;

namespace Sayra.Client.Shared.Runtime.ProcessSupervisor.Application.Interfaces
{
    public interface IProcessTreeMonitor
    {
        event Action<Guid, ProcessNode> UnexpectedProcessDetected;
        Task<IEnumerable<ProcessNode>> GetDescendantsAsync(int rootProcessId);
    }
}

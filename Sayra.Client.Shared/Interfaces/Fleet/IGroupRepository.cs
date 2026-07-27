using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IGroupRepository
    {
        Task CreateGroupAsync(MachineGroup group, CancellationToken ct = default);
        Task DeleteGroupAsync(string groupId, CancellationToken ct = default);
        Task AssignMachineAsync(string workstationId, string groupId, CancellationToken ct = default);
        Task RemoveMachineAsync(string workstationId, string groupId, CancellationToken ct = default);
        Task<MachineGroup?> GetGroupAsync(string groupId, CancellationToken ct = default);
        Task<List<MachineGroup>> GetAllGroupsAsync(CancellationToken ct = default);
        Task<List<Workstation>> GetMachinesAsync(string groupId, CancellationToken ct = default);
    }
}

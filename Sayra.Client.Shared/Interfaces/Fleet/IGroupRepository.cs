using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IGroupRepository
    {
        Task CreateGroupAsync(MachineGroup group, CancellationToken cancellationToken = default);
        Task DeleteGroupAsync(string groupId, CancellationToken cancellationToken = default);
        Task AssignMachineAsync(string machineId, string groupId, CancellationToken cancellationToken = default);
        Task RemoveMachineAsync(string machineId, string groupId, CancellationToken cancellationToken = default);
        Task<MachineGroup?> GetGroupAsync(string groupId, CancellationToken cancellationToken = default);
        Task<List<string>> GetMachinesAsync(string groupId, CancellationToken cancellationToken = default);
        Task<List<MachineGroup>> GetGroupsForMachineAsync(string machineId, CancellationToken cancellationToken = default);
        Task<List<MachineGroup>> GetAllGroupsAsync(CancellationToken cancellationToken = default);
    }
}

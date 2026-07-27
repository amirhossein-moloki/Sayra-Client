using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IFleetManager
    {
        Task RegisterWorkstationAsync(Workstation workstation, CancellationToken ct = default);
        Task UpdateMetadataAsync(string workstationId, string ipAddress, string macAddress, string version, string gpu, int ramGb, string winVer, string policyVer, CancellationToken ct = default);
        Task AssignToGroupsAsync(string workstationId, List<string> groupIds, CancellationToken ct = default);
        Task RemoveWorkstationAsync(string workstationId, CancellationToken ct = default);
        Task<Workstation?> GetWorkstationAsync(string workstationId, CancellationToken ct = default);
        Task<List<Workstation>> GetActiveWorkstationsAsync(CancellationToken ct = default);
    }
}

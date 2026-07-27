using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface IFleetManager
    {
        Task RegisterWorkstationAsync(string machineId, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);
        Task UpdateWorkstationMetadataAsync(string machineId, Dictionary<string, string> metadata, CancellationToken cancellationToken = default);
        Task AssignWorkstationToGroupsAsync(string machineId, List<string> groupIds, CancellationToken cancellationToken = default);
        Task RemoveWorkstationAsync(string machineId, CancellationToken cancellationToken = default);
        Task<string> QueryWorkstationStatusAsync(string machineId, CancellationToken cancellationToken = default);
        Task<Dictionary<string, string>> QueryWorkstationCapabilitiesAsync(string machineId, CancellationToken cancellationToken = default);
    }
}

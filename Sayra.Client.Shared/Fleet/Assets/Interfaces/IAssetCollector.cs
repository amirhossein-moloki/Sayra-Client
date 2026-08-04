using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models.Phase9.Domain;

namespace Sayra.Client.Shared.Fleet.Assets.Interfaces
{
    /// <summary>
    /// Contract representing an independent subsystem asset inventory collector.
    /// </summary>
    public interface IAssetCollector
    {
        /// <summary>
        /// Asynchronously scans the system to collect detailed asset information for a target machine.
        /// </summary>
        Task<IReadOnlyList<AssetRecord>> CollectAssetsAsync(string machineId, CancellationToken ct = default);
    }
}

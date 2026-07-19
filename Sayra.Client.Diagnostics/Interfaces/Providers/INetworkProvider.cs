using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface INetworkProvider
    {
        Task<List<NetworkInformation>> GetNetworksAsync(CancellationToken cancellationToken = default);
        Task<double> GetNetworkUsageAsync(CancellationToken cancellationToken = default);
    }
}

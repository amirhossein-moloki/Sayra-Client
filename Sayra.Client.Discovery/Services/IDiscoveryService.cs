using Sayra.Client.Discovery.Models;

namespace Sayra.Client.Discovery.Services;

public interface IDiscoveryService
{
    Task<DiscoveryResponse?> DiscoverAsync(CancellationToken cancellationToken, bool forceFresh = false);
}

using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Configuration.Models;

namespace Sayra.Client.Configuration.Synchronization;

public class MockConfigurationApiClient : IConfigurationApiClient
{
    public Func<long, CancellationToken, Task<ConfigurationPackage?>>? FetchMockHandler { get; set; }

    public Task<ConfigurationPackage?> FetchLatestPackageAsync(long currentVersion, CancellationToken cancellationToken = default)
    {
        if (FetchMockHandler != null)
        {
            return FetchMockHandler(currentVersion, cancellationToken);
        }

        return Task.FromResult<ConfigurationPackage?>(null);
    }
}

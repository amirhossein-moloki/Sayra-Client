using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Configuration.Models;

namespace Sayra.Client.Configuration.Synchronization;

public interface IConfigurationApiClient
{
    Task<ConfigurationPackage?> FetchLatestPackageAsync(long currentVersion, CancellationToken cancellationToken = default);
}

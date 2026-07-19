using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IStorageProvider : IHardwareProvider
    {
        Task<List<StorageInformation>> GetStorageAsync(CancellationToken cancellationToken = default);
    }
}

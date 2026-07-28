using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.UpdatePlatform.Domain.Models;

namespace Sayra.Client.Shared.UpdatePlatform.Application.Interfaces
{
    /// <summary>
    /// Manages workstation update storage space verification and quotas.
    /// </summary>
    public interface IStorageQuotaManager
    {
        Task<bool> HasEnoughSpaceForPackageAsync(long packageSizeBytes, CancellationToken cancellationToken = default);
        Task<StorageStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);
    }
}

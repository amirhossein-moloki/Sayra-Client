using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IGpuProvider
    {
        Task<List<GpuInformation>> GetGpusAsync(CancellationToken cancellationToken = default);
        Task<double> GetGpuUsageAsync(CancellationToken cancellationToken = default);
        Task<double> GetVramUsageAsync(CancellationToken cancellationToken = default);
    }
}

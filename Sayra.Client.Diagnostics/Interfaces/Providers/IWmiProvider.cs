using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IWmiProvider
    {
        Task<List<Dictionary<string, object>>> QueryAsync(string query, string scope = "root\\CIMV2", CancellationToken cancellationToken = default);
    }
}

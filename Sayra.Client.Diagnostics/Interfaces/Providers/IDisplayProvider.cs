using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Interfaces.Providers
{
    public interface IDisplayProvider
    {
        Task<List<DisplayInformation>> GetDisplaysAsync(CancellationToken cancellationToken = default);
    }
}

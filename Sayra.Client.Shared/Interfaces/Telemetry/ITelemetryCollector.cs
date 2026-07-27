using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface ITelemetryCollector
    {
        Task CollectAsync(LiveTelemetryData data, CancellationToken cancellationToken = default);
    }
}

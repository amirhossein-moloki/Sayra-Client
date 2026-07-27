using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface ITelemetryPublisher
    {
        Task PublishAsync(LiveTelemetryData data, CancellationToken cancellationToken = default);
    }
}

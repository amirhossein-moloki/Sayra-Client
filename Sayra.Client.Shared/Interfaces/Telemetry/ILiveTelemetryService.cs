using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Shared.Interfaces
{
    public interface ILiveTelemetryService
    {
        Task<LiveTelemetryData> CaptureSnapshotAsync(CancellationToken cancellationToken = default);
        IObservable<LiveTelemetryData> GetTelemetryStream(TimeSpan interval);
    }
}

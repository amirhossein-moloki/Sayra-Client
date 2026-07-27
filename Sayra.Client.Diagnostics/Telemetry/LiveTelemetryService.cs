using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class LiveTelemetryService : ILiveTelemetryService
    {
        private readonly IEnumerable<ITelemetryCollector> _collectors;
        private readonly ILogger<LiveTelemetryService> _logger;
        private static readonly Process CurrentProcess = Process.GetCurrentProcess();

        public LiveTelemetryService(
            IEnumerable<ITelemetryCollector> collectors,
            ILogger<LiveTelemetryService> logger)
        {
            _collectors = collectors ?? throw new ArgumentNullException(nameof(collectors));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<LiveTelemetryData> CaptureSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var data = new LiveTelemetryData
            {
                Timestamp = DateTime.UtcNow,
                MachineId = Environment.MachineName,
                UptimeSeconds = Math.Round((DateTime.Now - CurrentProcess.StartTime).TotalSeconds, 1)
            };

            var tasks = _collectors.Select(async collector =>
            {
                try
                {
                    await collector.CollectAsync(data, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Collector {CollectorType} failed during snapshot generation.", collector.GetType().Name);
                }
            });

            await Task.WhenAll(tasks);
            return data;
        }

        public IObservable<LiveTelemetryData> GetTelemetryStream(TimeSpan interval)
        {
            return new TelemetryObservable(this, interval, _logger);
        }

        private class TelemetryObservable : IObservable<LiveTelemetryData>
        {
            private readonly LiveTelemetryService _service;
            private readonly TimeSpan _interval;
            private readonly ILogger _logger;

            public TelemetryObservable(LiveTelemetryService service, TimeSpan interval, ILogger logger)
            {
                _service = service;
                _interval = interval;
                _logger = logger;
            }

            public IDisposable Subscribe(IObserver<LiveTelemetryData> observer)
            {
                if (observer == null) throw new ArgumentNullException(nameof(observer));
                var cts = new CancellationTokenSource();

                Task.Run(async () =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var snapshot = await _service.CaptureSnapshotAsync(cts.Token);
                            observer.OnNext(snapshot);
                            await Task.Delay(_interval, cts.Token);
                        }
                    }
                    catch (OperationCanceledException) { observer.OnCompleted(); }
                    catch (Exception ex) { observer.OnError(ex); }
                }, cts.Token);

                return new Subscription(cts);
            }

            private class Subscription : IDisposable
            {
                private readonly CancellationTokenSource _cts;
                public Subscription(CancellationTokenSource cts) { _cts = cts; }
                public void Dispose() { _cts.Cancel(); _cts.Dispose(); }
            }
        }
    }
}

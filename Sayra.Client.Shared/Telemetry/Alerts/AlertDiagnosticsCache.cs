using System;
using System.Threading;
using System.Threading.Tasks;
using Sayra.Client.Shared.Interfaces.Telemetry;
using Sayra.Client.Shared.Models.Telemetry;

namespace Sayra.Client.Shared.Telemetry.Alerts
{
    public interface IAlertDiagnosticsCache
    {
        Task<DiagnosticReport> GetLatestReportAsync(CancellationToken cancellationToken = default);
    }

    public class AlertDiagnosticsCache : IAlertDiagnosticsCache
    {
        private readonly IDiagnosticsEngine _diagnosticsEngine;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private DiagnosticReport? _report;
        private DateTime _lastFetched = DateTime.MinValue;

        public AlertDiagnosticsCache(IDiagnosticsEngine diagnosticsEngine)
        {
            _diagnosticsEngine = diagnosticsEngine ?? throw new ArgumentNullException(nameof(diagnosticsEngine));
        }

        public async Task<DiagnosticReport> GetLatestReportAsync(CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_report == null || DateTime.UtcNow - _lastFetched > TimeSpan.FromSeconds(5))
                {
                    _report = await _diagnosticsEngine.GenerateDiagnosticsReportAsync(cancellationToken);
                    _lastFetched = DateTime.UtcNow;
                }
                return _report;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}

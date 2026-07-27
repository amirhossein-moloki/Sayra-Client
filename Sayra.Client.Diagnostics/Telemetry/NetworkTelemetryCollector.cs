using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Models;

namespace Sayra.Client.Diagnostics.Telemetry
{
    public class NetworkTelemetryCollector : ITelemetryCollector
    {
        private readonly ILogger<NetworkTelemetryCollector> _logger;
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private DateTime _lastCheck = DateTime.UtcNow;

        public string TargetAddress { get; set; } = "127.0.0.1";
        public int PingTimeoutMs { get; set; } = 1500;

        public NetworkTelemetryCollector(ILogger<NetworkTelemetryCollector> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            try { InitializeInitialStats(); } catch { }
        }

        private void InitializeInitialStats()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    try
                    {
                        var ipStats = ni.GetIPStatistics();
                        _lastBytesReceived += ipStats.BytesReceived;
                        _lastBytesSent += ipStats.BytesSent;
                    }
                    catch { }
                }
            }
        }

        public async Task CollectAsync(LiveTelemetryData data, CancellationToken cancellationToken = default)
        {
            try
            {
                long currentBytesReceived = 0;
                long currentBytesSent = 0;
                var now = DateTime.UtcNow;

                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        try
                        {
                            var ipStats = ni.GetIPStatistics();
                            currentBytesReceived += ipStats.BytesReceived;
                            currentBytesSent += ipStats.BytesSent;
                        }
                        catch { }
                    }
                }

                double elapsedSec = (now - _lastCheck).TotalSeconds;
                if (elapsedSec > 0 && _lastBytesReceived > 0)
                {
                    long deltaReceived = currentBytesReceived - _lastBytesReceived;
                    long deltaSent = currentBytesSent - _lastBytesSent;
                    data.BytesReceivedPerSecond = Math.Round(Math.Max(0.0, deltaReceived / elapsedSec), 1);
                    data.BytesSentPerSecond = Math.Round(Math.Max(0.0, deltaSent / elapsedSec), 1);
                }

                _lastBytesReceived = currentBytesReceived;
                _lastBytesSent = currentBytesSent;
                _lastCheck = now;

                data.PingMs = await MeasurePingAsync(TargetAddress, PingTimeoutMs, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect Network telemetry.");
                data.BytesReceivedPerSecond = 0;
                data.BytesSentPerSecond = 0;
                data.PingMs = 999.0;
            }
        }

        private async Task<double> MeasurePingAsync(string target, int timeoutMs, CancellationToken cancellationToken)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await Task.Run(async () => {
                        try { return await ping.SendPingAsync(target, timeoutMs); } catch { return null; }
                    }, cancellationToken);

                    if (reply != null && reply.Status == IPStatus.Success)
                    {
                        return reply.RoundtripTime;
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { }
            return 0.0;
        }
    }
}

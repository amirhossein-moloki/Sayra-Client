using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Diagnostics.Interfaces.Providers;
using Sayra.Client.Diagnostics.Models;

namespace Sayra.Client.Diagnostics.Providers
{
    public class NetworkProvider : INetworkProvider
    {
        private readonly ILogger<NetworkProvider> _logger;
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private DateTime _lastCheck = DateTime.UtcNow;

        public NetworkProvider(ILogger<NetworkProvider> logger)
        {
            _logger = logger;
            try
            {
                InitializeInitialBytes();
            }
            catch { }
        }

        private void InitializeInitialBytes()
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    var stats = ni.GetIPProperties().GetIPv4Properties(); // throws on non-IP interfaces, let's catch inside
                    var ipStats = ni.GetIPStatistics();
                    _lastBytesReceived += ipStats.BytesReceived;
                    _lastBytesSent += ipStats.BytesSent;
                }
            }
        }

        public Task<List<NetworkInformation>> GetNetworksAsync(CancellationToken cancellationToken = default)
        {
            var list = new List<NetworkInformation>();
            try
            {
                string hostname = Dns.GetHostName();
                string ipv4 = "127.0.0.1";
                string ipv6 = "::1";
                string macAddress = "00:00:00:00:00:00";

                // Get IP addresses
                var hostEntry = Dns.GetHostEntry(hostname);
                foreach (var ip in hostEntry.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        ipv4 = ip.ToString();
                    }
                    else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        ipv6 = ip.ToString();
                    }
                }

                // Get MAC address of primary interface
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        macAddress = ni.GetPhysicalAddress().ToString();
                        // Format MAC address as standard colon-separated string if possible
                        if (macAddress.Length == 12)
                        {
                            var parts = new string[6];
                            for (int i = 0; i < 6; i++)
                            {
                                parts[i] = macAddress.Substring(i * 2, 2);
                            }
                            macAddress = string.Join(":", parts);
                        }
                        break;
                    }
                }

                list.Add(new NetworkInformation(
                    hostname,
                    ipv4,
                    ipv6,
                    macAddress
                ));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fully collect network specs. Returning standard default.");
                list.Add(new NetworkInformation(
                    Dns.GetHostName(),
                    "127.0.0.1",
                    "::1",
                    "00:15:5D:01:23:45"
                ));
            }

            return Task.FromResult(list);
        }

        public Task<double> GetNetworkUsageAsync(CancellationToken cancellationToken = default)
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
                double networkUsageKbps = 0;

                if (elapsedSec > 0 && _lastBytesReceived > 0)
                {
                    long deltaReceived = currentBytesReceived - _lastBytesReceived;
                    long deltaSent = currentBytesSent - _lastBytesSent;
                    // convert to Kbps
                    networkUsageKbps = ((deltaReceived + deltaSent) * 8.0 / 1024.0) / elapsedSec;
                }

                _lastBytesReceived = currentBytesReceived;
                _lastBytesSent = currentBytesSent;
                _lastCheck = now;

                return Task.FromResult(Math.Round(Math.Max(0.0, networkUsageKbps), 2));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to compute network metrics usage.");
                return Task.FromResult(0.0);
            }
        }
    }
}

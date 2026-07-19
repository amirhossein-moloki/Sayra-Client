using System;

namespace Sayra.Client.Diagnostics.Models
{
    public record NetworkInformation
    {
        public string Hostname { get; init; }
        public string IPv4 { get; init; }
        public string IPv6 { get; init; }
        public string MacAddress { get; init; }
        public string AdapterName { get; init; }
        public string AdapterType { get; init; }
        public string Gateway { get; init; }
        public string Dns { get; init; }
        public string ConnectionStatus { get; init; }
        public long LinkSpeed { get; init; }

        public NetworkInformation(
            string hostname,
            string iPv4,
            string iPv6,
            string macAddress,
            string adapterName = "Unknown",
            string adapterType = "Unknown",
            string gateway = "Unknown",
            string dns = "Unknown",
            string connectionStatus = "Disconnected",
            long linkSpeed = 0)
        {
            Hostname = hostname;
            IPv4 = iPv4;
            IPv6 = iPv6;
            MacAddress = macAddress;
            AdapterName = adapterName;
            AdapterType = adapterType;
            Gateway = gateway;
            Dns = dns;
            ConnectionStatus = connectionStatus;
            LinkSpeed = linkSpeed;
        }
    }
}

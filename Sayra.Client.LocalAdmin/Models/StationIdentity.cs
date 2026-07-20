using System;

namespace Sayra.Client.LocalAdmin.Models
{
    public class StationIdentity
    {
        public string MachineName { get; set; } = string.Empty;
        public string? ConfiguredStationName { get; set; }
        public string StationId { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string LocalIPv4 { get; set; } = string.Empty;
        public string CurrentHostname { get; set; } = string.Empty;
        public string EnvironmentInformation { get; set; } = string.Empty;
        public string ResolvedStationName { get; set; } = string.Empty;
    }
}

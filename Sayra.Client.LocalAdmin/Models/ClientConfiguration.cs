using System.Collections.Generic;

namespace Sayra.Client.LocalAdmin.Models
{
    public class ClientConfiguration
    {
        public ServerDiscoverySettings ServerDiscovery { get; set; } = new();
        public GameLibrarySettings GameLibrary { get; set; } = new();
        public List<string> ScannerPaths { get; set; } = new();
        public LocalPreferencesSettings LocalPreferences { get; set; } = new();
    }

    public class ServerDiscoverySettings
    {
        public string ServerIp { get; set; } = "127.0.0.1";
        public int UdpPort { get; set; } = 37020;
        public bool AutoDiscovery { get; set; } = true;
    }

    public class GameLibrarySettings
    {
        public string LibraryPath { get; set; } = "C:\\Program Files\\Sayra\\Games";
        public bool AutoUpdate { get; set; } = true;
    }

    public class LocalPreferencesSettings
    {
        public string Theme { get; set; } = "Dark";
        public string Language { get; set; } = "fa-IR";
        public bool IsKioskMode { get; set; } = true;
    }
}

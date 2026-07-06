namespace Sayra.Client.Discovery.Models;

public class DiscoveryRequest
{
    public string type { get; set; } = "DISCOVER_SAYRA_SERVER";
    public string clientId { get; set; } = string.Empty;
    public string timestamp { get; set; } = string.Empty;
    public string nonce { get; set; } = string.Empty;
}

public class DiscoveryResponse
{
    public string type { get; set; } = string.Empty;
    public string serverId { get; set; } = string.Empty;
    public string ip { get; set; } = string.Empty;
    public int tcpPort { get; set; }
    public int apiPort { get; set; }
    public string version { get; set; } = string.Empty;
    public string timestamp { get; set; } = string.Empty;
    public string signature { get; set; } = string.Empty;
}

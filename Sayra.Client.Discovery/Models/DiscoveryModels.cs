namespace Sayra.Client.Discovery.Models;

public class DiscoveryRequest
{
    public string type { get; set; } = "DISCOVER_SAYRA_SERVER";
    public string clientId { get; set; } = string.Empty;
    public string timestamp { get; set; } = string.Empty;
    public string nonce { get; set; } = string.Empty;
}

public class ServerDiscoveryResponse
{
    public string type { get; set; } = "SAYRA_SERVER_RESPONSE";
    public string serverId { get; set; } = string.Empty;
    public string serverName { get; set; } = string.Empty;
    public string ip { get; set; } = string.Empty;
    public int tcpPort { get; set; }
    public int apiPort { get; set; }
    public string version { get; set; } = string.Empty;
    public string timestamp { get; set; } = string.Empty;
    public string nonce { get; set; } = string.Empty;
    public string signature { get; set; } = string.Empty;

    // Internal use for selection logic
    public long Latency { get; set; }
}

public class DiscoveryResponse : ServerDiscoveryResponse
{
}

public class ServerCache
{
    public string ServerId { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string LastIPAddress { get; set; } = string.Empty;
    public int TcpPort { get; set; }
    public DateTime LastConnected { get; set; }
    public string PublicKeyFingerprint { get; set; } = string.Empty;
}

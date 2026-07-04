# Sayra Client - Configuration Reference

## appsettings.json

| Key | Description | Default |
|-----|-------------|---------|
| `ServerConfig:IpAddress` | IP address of the Sayra Server | `127.0.0.1` |
| `ServerConfig:Port` | TCP port of the Sayra Server | `5000` |
| `ServerConfig:HeartbeatIntervalSeconds` | Frequency of heartbeats | `10` |
| `UpdateConfig:AutoUpdate` | Enable/Disable background update checks | `true` |
| `UpdateConfig:UpdateUrl` | URL to poll for update metadata | - |
| `Logging:LogLevel:Default` | Global logging level | `Information` |

## Environment Variables
- `SAYRA_MASTER_KEY`: The primary secret used for challenge-response authentication.

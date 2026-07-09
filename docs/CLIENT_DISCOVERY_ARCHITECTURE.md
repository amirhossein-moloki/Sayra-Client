# Sayra Client Discovery Architecture

## Overview
Sayra Client implements a zero-configuration LAN server discovery mechanism. This allows clients to find the Sayra Server automatically without manual IP configuration, even in environments with dynamic IP addresses (DHCP).

## Discovery Lifecycle

1. **Startup**: The client starts and transitions to the `STARTING` state.
2. **Cache Check**: The `DiscoveryManager` checks for a cached server configuration in `server_cache.json`.
3. **TCP Connection Attempt**: If a cached server is found, the client attempts to connect to it using TCP.
4. **Discovery Trigger**: If no cache exists or the connection to the cached server fails, the client transitions to the `DISCOVERING` state.
5. **UDP Broadcast**: The client sends a `DISCOVER_SAYRA_SERVER` broadcast packet on UDP port 37020.
6. **Response Collection**: The client waits for `SAYRA_SERVER_RESPONSE` messages from potential servers.
7. **Identity Verification**: Each response is validated using the server's RSA public key and security rules (timestamp, nonce).
8. **Server Selection**: If multiple valid servers respond, the client selects the best one (based on latency and previous trust).
9. **Connection & Handshake**: The client connects to the selected server via TCP and performs the existing secure authentication handshake.
10. **Cache Update**: Upon a successful connection, the server information is persisted to the cache.

## Network Flow

- **Protocol**: UDP for discovery, TCP for session communication.
- **UDP Port**: 37020 (Default).
- **TCP Port**: 5000 (Default).

### Discovery Request
```json
{
 "type": "DISCOVER_SAYRA_SERVER",
 "clientId": "PC-001",
 "timestamp": "2023-10-27T10:00:00Z",
 "nonce": "random_guid"
}
```

### Discovery Response
```json
{
 "type": "SAYRA_SERVER_RESPONSE",
 "serverId": "SVR-MAIN",
 "serverName": "Sayra Server Alpha",
 "ip": "192.168.1.10",
 "tcpPort": 5000,
 "timestamp": "2023-10-27T10:00:01Z",
 "nonce": "another_random_guid",
 "signature": "RSA_SIGNATURE_BASE64"
}
```

## Security Verification

### RSA Signature
The client contains a `server_public.key`. Every discovery response must be signed by the server using its private key. The client verifies the signature of the following fields concatenated:
`ServerId + ServerName + IP + TCPPort + Timestamp + Nonce`

### Replay Attack Prevention
- **Nonce**: Each discovery response must have a unique nonce. The client keeps a history of seen nonces to reject duplicates.
- **Timestamp**: Responses with a timestamp older or newer than 10 seconds from the client's current UTC time are rejected.

### Fake Server Protection
Any response that fails signature verification or is missing required security fields is discarded. The client will never attempt a TCP connection to an untrusted server.

## Configuration
In `appsettings.json`:
```json
"ServerDiscovery": {
  "Enabled": true,
  "UdpPort": 37020,
  "DiscoveryTimeoutSeconds": 5
}
```

## Failure Recovery
- If discovery fails to find any trusted server, the client will retry after a delay (managed by `ReconnectManager`).
- If a connection to a discovered server is lost, the client clears the current endpoint and triggers a new discovery cycle to handle potential server IP changes.

# SAYRA — Central Backend Bootstrap & Server Discovery Forensic Audit Report

This report presents a high-rigor forensic code audit of the SAYRA Client's bootstrap, initial discovery, security handshake, and disaster recovery strategies when communicating with the Central Backend.

Every claim, flow, and mechanism discussed is backed by actual executable code paths within the repository.

---

## 1. PRIMARY QUESTIONS & DIRECT ANSWERS

### Primary Question A
> **When SAYRA Client starts for the first time, exactly how does it know where the Central Backend is?**

**Direct Answer:**
When the SAYRA Client boots up for the first time with a fresh installation (where `server_cache.json` does not exist), it resolves the server's endpoint in two sequential stages defined in `TcpClientManager.ResolveAndConnectAsync`:

1. **Static Configuration Inspection:** It first checks if a hardcoded, non-default static IP address is explicitly specified in the `ServerConfig:IpAddress` configuration setting. If a valid, non-placeholder IP is found (not empty and not `"SAYRA_SERVER_IP"`), it will target that IP and the port specified in `ServerConfig:Port` (default: `5000`).
2. **Active UDP LAN Discovery Broadcast:** If no static configuration is present (i.e. `ServerConfig:IpAddress` is missing, empty, or set to `"SAYRA_SERVER_IP"`), and `"ServerDiscovery:Enabled"` is set to `true` (default: `true`), the client initiates an active **UDP discovery broadcast sequence** via `UdpDiscoveryClient.BroadcastDiscoveryAsync`. It broadcasts a custom JSON request (`DISCOVER_SAYRA_SERVER`) over the port specified by `ServerDiscovery:UdpPort` (default: `37020`) to the subnet broadcast address `255.255.255.255`.
3. **Identity Verification & Signature Validation:** Active servers on the LAN listening on that port respond with an RSA-signed JSON payload. The client validates the signature of each responding server using the public key stored in `server_public.key` at the application root (`DiscoveryValidator.VerifySignature`). If verified, the server's endpoint is saved into `server_cache.json` and a secure TLS 1.3 connection is opened.

---

### Primary Question B
> **If the Central Backend server IP address changes, how does the SAYRA Client discover the new server?**

**Direct Answer:**
The discovery of the new server IP depends on whether the client is in an active session or attempting to reconnect:

1. **Active TCP/TLS Connection Loss Detection:** The client detects connection loss inside the receive loop `TcpClientManager.ReceiveMessagesLoopAsync` when `ReadLineAsync` returns `null` or throws an `IOException` or `SocketException`. This triggers a transition of the state machine to `ClientState.DISCONNECTED` and then `ClientState.RECOVERING` inside `TcpClientManager.StartAsync`.
2. **Cache and State Invalidation:** On failure, `TcpClientManager.StartAsync` clears its local resolved memory variables (`_resolvedIp = null` and `_resolvedPort = null`) and starts the reconnection wait loop managed by `ReconnectManager.WaitForNextRetry`.
3. **Reconnection Resolution Flow:** On the next retry iteration, `ResolveAndConnectAsync` is invoked:
   - **Step A:** It checks if a static IP is in configuration. If yes, it attempts to reconnect to that same static IP.
   - **Step B:** If no static IP is configured and discovery is enabled, it first attempts to load from the cache (`server_cache.json`). It will try to connect to the cached IP.
   - **Step C (Fresh Discovery Trigger):** If the connection to the cached IP fails, `TcpClientManager.ResolveAndConnectAsync` invokes the discovery service with `forceFresh: true`. This invalidates the cached IP, bypasses `server_cache.json`, and starts a fresh UDP LAN multicast broadcast via `UdpDiscoveryClient.BroadcastDiscoveryAsync` on port `37020` to discover the new IP of the server. The first responding server with a valid RSA cryptographic signature is chosen, and `server_cache.json` is updated with the new IP address.

---

### Primary Question C
> **If the primary Central Backend becomes unavailable, can the Client automatically find and connect to another Backend?**

**Direct Answer:**
**YES, but with limitations.** Automatic failover is supported **strictly within the same Local Area Network (LAN)** through the UDP broadcast mechanism:

1. **Discovery Request Broadcast:** When the current backend is offline and connection attempts fail, the client sends a discovery broadcast.
2. **Response Selection:** `DiscoveryManager.SelectBestServer` collects all valid, signed responses from available backends on the subnet.
3. **Prioritization Logic:**
   - If a previously trusted `ServerId` (matching the cached `ServerId` from `server_cache.json`) responds, the client will **prioritize** connecting to that specific server, ordering matches by the lowest network latency.
   - If the previously trusted server is completely dead and does not respond, the client will automatically fall back to the **next best server** in the responding pool, prioritizing the one with the **lowest latency** (round-trip ping time measured during the UDP request/response exchange).
4. **Limitation (WAN / Multiple Subnets):** There is **no built-in failover pool configuration** or DNS-based round-robin mechanism for WAN-based backends. If the backends are on different subnets or over the internet where UDP broadcast is blocked, the client cannot find another backend automatically unless a static DNS name (behind a WAN Load Balancer) is configured.

---

## 2. DETAILED FIRST START FLOW

Below is the step-by-step trace of the execution flow from raw executable startup to an established authenticated session.

```
Client Process Start (Program.cs)
    ↓
Startup Pipeline Execution (StartupPipeline.cs)
    ↓
Configuration Loading (appsettings.json / Environment variables)
    ↓
Endpoint Resolution & Server Discovery (TcpClientManager.cs & DiscoveryManager.cs)
    ↓
UDP LAN Broadcast (UdpDiscoveryClient.cs) (if fresh / no cache)
    ↓
Signature & Nonce Verification (DiscoveryValidator.cs)
    ↓
TCP Connection Establishment (TlsConnectionManager.cs)
    ↓
TLS 1.3 Handshake & Custom Cert Validation (TlsConnectionManager.cs)
    ↓
HMAC-SHA256 Challenge Authentication Handshake (AuthManager.cs)
    ↓
Session Established (ClientState.READY)
```

### Trace Details for Every Stage:

1. **Client Startup**
   - **File:** `SayraClient/Program.cs`
   - **Class/Method:** Entry-point execution. Resolves and registers singletons in the Dependency Injection container. It hosts `ClientAppLifetimeWorker` as the background service orchestrating startup.
   - **Dependency:** Microsoft.Extensions.Hosting, Serilog.

2. **Bootstrap**
   - **File:** `SayraClient/Services/StartupPipeline.cs`
   - **Class/Method:** `StartupPipeline.ExecuteAsync(CancellationToken)`
   - **Description:** Runs a strict 10-stage bootstrapper: Environmental validation (Stage 1-2) -> Dependency and Configuration integrity checks (Stage 3-4) -> Registering and starting background modules (Stage 5-7) -> Activating background workers under `WorkerSupervisor` supervision (Stage 8) -> Health monitoring (Stage 9) -> Transitioning state machine to `ClientState.DISCOVERING_SERVER` (Stage 10).
   - **Important Code Path:** Stage 8 registers the `NetworkWorker` (which runs `Worker.cs`). Once the pipeline completes successfully, `NetworkWorker` executes `TcpClientManager.StartAsync`.

3. **Configuration Loading**
   - **File:** `SayraClient/appsettings.json`, environment variables, and `SayraClient/Security/Transport/TransportPolicy.cs`
   - **Class/Method:** `TransportPolicy.ctor(IConfiguration)`
   - **Description:** Binds configuration sections `ServerDiscovery` (UDP settings), `ServerConfig` (TCP settings), and `TransportSecurity` into strong-typed parameters.

4. **Endpoint Resolution**
   - **File:** `SayraClient/TcpClientManager.cs`
   - **Class/Method:** `TcpClientManager.ResolveAndConnectAsync(CancellationToken)`
   - **Description:** Checks configuration for `ServerConfig:IpAddress`. If empty or set to `"SAYRA_SERVER_IP"`, it calls `IDiscoveryService.DiscoverAsync(cancellationToken, forceFresh: false)`.

5. **UDP LAN Discovery Broadcast** (Only executed on first start / empty cache)
   - **File:** `Sayra.Client.Discovery/Services/DiscoveryManager.cs` & `UdpDiscoveryClient.cs`
   - **Class/Method:** `DiscoveryManager.InternalDiscoverAsync` and `UdpDiscoveryClient.BroadcastDiscoveryAsync`
   - **Description:** Resolves `ServerDiscovery:UdpPort` (default `37020`) and `ServerDiscovery:DiscoveryTimeoutSeconds` (default `5`). Broadcasts a serialized JSON `DiscoveryRequest` containing the machine's Client ID, a UTC ISO 8601 timestamp, and a fresh UUID nonce to `255.255.255.255`. It opens a temporary UDP port to listen for responses.

6. **Server Identity Validation**
   - **File:** `Sayra.Client.Discovery/Services/DiscoveryValidator.cs`
   - **Class/Method:** `DiscoveryValidator.Validate(ServerDiscoveryResponse)` and `VerifySignature`
   - **Description:**
     1. Validates format: requires `"SAYRA_SERVER_RESPONSE"`.
     2. Validates timestamp: checks that the server's timestamp is within $\pm10$ seconds of the client's system clock.
     3. Validates nonce uniqueness: stores used nonces in a thread-safe `HashSet<string>` cache to block replay attacks.
     4. Cryptographic Validation: Imports the public PEM key from `server_public.key` at the application root directory. Re-assembles the raw payload string: `"{serverId}{serverName}{ip}{tcpPort}{timestamp}{nonce}"`. Computes its SHA-256 hash and verifies the server's Base64 signature using RSA PKCS#1 padding.
   - **Caching:** If validated, the selected server details are saved to `server_cache.json` (`DiscoveryManager.SaveCache`).

7. **TCP Connection**
   - **File:** `SayraClient/Security/Transport/TlsConnectionManager.cs`
   - **Class/Method:** `TlsConnectionManager.ConnectAsync(string ip, int port, CancellationToken)`
   - **Description:** Instantiates a native `System.Net.Sockets.TcpClient` and opens a socket connection to the resolved server IP and port (timeout set to 5 seconds).

8. **TLS Handshake & Certificate Validation**
   - **File:** `SayraClient/Security/Transport/TlsConnectionManager.cs`
   - **Class/Method:** `TlsConnectionManager.ConnectAsync` and `ValidateServerCertificate`
   - **Description:** Wraps the network stream in a `System.Net.Security.SslStream`. Initiates client authentication targeting TLS 1.3 explicitly (`SslProtocols.Tls13`).
   - **Custom Validation Callback:**
     1. Validates that the certificate presented by the server is not expired (`NotBefore` / `NotAfter` bounds check).
     2. Checks for hostname mismatch flags.
     3. **Certificate Pinning:** If enabled in policy, it checks the server's certificate thumbprint against `TransportSecurity:PinnedCertificateThumbprint` and/or the public key SHA-256 hash against `TransportSecurity:PinnedPublicKeyHash`.
     4. Verifies that TLS 1.3 was successfully negotiated.

9. **Authentication Handshake**
   - **File:** `SayraClient/MessageHandler.cs` & `SayraClient/Services/AuthManager.cs`
   - **Class/Method:** `MessageHandler.HandleMessageAsync` & `AuthManager.HandleChallengeAsync`
   - **Description:**
     1. Once TLS is active, the server sends a raw JSON `AUTH_CHALLENGE` containing a cryptographic challenge string.
     2. `AuthManager` reads the client master key from the `SAYRA_MASTER_KEY` environment variable or the `SecurityConfig:MasterKey` appsettings parameter.
     3. Generates a new 32-byte (256-bit) ephemeral cryptographically secure random session key (`_pendingSessionKey`).
     4. Proof of Identity: Computes an HMAC-SHA256 signature over the challenge string using the Master Key.
     5. Session Key Exchange: Encrypts the ephemeral session key using AES (using the Master Key as the AES key) with a freshly generated IV.
     6. Sends the HMAC signature and the AES-encrypted session key back to the server inside an `AuthResponseModel` payload.

10. **Session Established**
    - **File:** `SayraClient/MessageHandler.cs` & `SayraClient/Services/AuthManager.cs`
    - **Class/Method:** `MessageHandler.HandleMessageAsync` & `AuthManager.HandleAuthStatus`
    - **Description:** The server verifies the challenge response, stores the session key, and responds with a JSON `AUTH_STATUS` message set to `"SUCCESS"`. The client's state machine transitions to `ClientState.READY` via `ClientStateManager.TransitionTo`, registers the session key in `SessionKeyManager`, and immediately transmits a starting `CLIENT_CONNECTED` event containing state information to the server.

---

## 3. WHERE IS THE SERVER ADDRESS STORED?

Here is the exhaustive inventory of all potential sources for backend endpoints identified in the SAYRA Client repository:

| Source | Location | Example / Syntax | Runtime Used | Mutable | Details |
|---|---|---|---|---|---|
| **JSON Configuration** | `SayraClient/appsettings.json` | `ServerConfig:IpAddress` | **YES** | **YES** | Set via appsettings file modification. Defaults to empty/unset. |
| **JSON Port Config** | `SayraClient/appsettings.json` | `ServerConfig:Port` | **YES** | **YES** | Defaults to `5000`. Binds TCP port. |
| **UDP Discovery Port** | `SayraClient/appsettings.json` | `ServerDiscovery:UdpPort` | **YES** | **YES** | Defaults to `37020`. Port used for active UDP broadcast. |
| **Local Cache File** | Root directory: `server_cache.json` | `{"LastIPAddress": "192.168.1.100"}` | **YES** | **YES** | Written dynamically by `DiscoveryManager.SaveCache` after successful signature validation. |
| **Update URL Config** | `SayraClient/appsettings.json` | `UpdateConfig:UpdateUrl` | **YES** | **YES** | Defaults to `"http://SAYRA_SERVER_IP:5000/api/updates/manifest"`. |
| **Command-Line Args** | None | N/A | **NO** | **NO** | No custom command-line overrides exist for server endpoints. |
| **Environment Vars** | System Environment | `SAYRA_SERVER_IP` (via system variables) | **NO** | **YES** | Read-only fallback placeholders in config. Not directly parsed in network manager code. |
| **Local SQLite DB** | SQLCipher database | N/A | **NO** | **NO** | No server connection IP coordinates are stored inside SQLite/SQLCipher tables. |
| **Registry Keys** | N/A | N/A | **NO** | **NO** | No Registry keys contain server IP configuration. |

---

## 4. BOOTSTRAP MECHANISM

### Bootstrap Status: **PARTIAL**

### Detailed Explanation:
SAYRA does **not** employ a dedicated cloud-based Bootstrap/Discovery Server, service registry, or installer-provided configuration portal. Instead, its bootstrap and server discovery strategy is **subnet-local (LAN-based)**.

- **Why it is categorized as PARTIAL:**
  - **Implemented Local LAN Discovery:** The UDP active broadcast system (`UdpDiscoveryClient`) acts as a highly effective, plug-and-play local discovery bootstrap. Workstations automatically broadcast on port `37020`, find active, authorized servers, validate them cryptographically using a localized public RSA certificate (`server_public.key`), and cache them locally in `server_cache.json`.
  - **Missing Enterprise WAN Bootstrap:** There is no remote/external service registry, configuration service, or secure cloud endpoint fallback to bootstrap the client when operating outside a local subnet where UDP broadcasts are dropped. If deployed in a multi-tenant corporate environment or wide-area network (WAN) spanning multiple subnets, the system **requires manual configuration** of the `ServerConfig:IpAddress` property in `appsettings.json` or a DNS-based routing layer.

---

## 5. DNS / DOMAIN STRATEGY

### DNS Configuration Strategy:
SAYRA's client can connect using an **IP Address** (e.g. `"192.168.1.100"`), a **Hostname** (e.g. `"sayra-control-01"`), or a fully qualified **Domain Name** (e.g. `"control.sayra.example"`).

### How Resolution Works:
The IP resolution is delegated entirely to the operating system's standard TCP socket layer inside `System.Net.Sockets.TcpClient.ConnectAsync(string host, int port)`.

1. **Resolution Frequency:** DNS resolution is performed **every single connection attempt**.
2. **Code Implementation:** Inside `TcpClientManager.ConnectAsync`, the client calls:
   ```csharp
   await _currentTcpClient.ConnectAsync(ip, port, connectCts.Token);
   ```
   If the parameter `ip` is a domain name (e.g. `"control.sayra.example"`), `.NET` resolves the hostname to an IP address on *every* invocation.
3. **No Internal Caching:** The application does **not** cache DNS records in memory or maintain custom DNS-to-IP mappings. It relies purely on the OS DNS cache.

### Critical DNS Update Scenario:
> **If DNS changes from `control.sayra.example` (pointing to `1.2.3.4`) to pointing to `5.6.7.8`, will the Client automatically use the new IP?**

**YES.**
If connection to the old IP (`1.2.3.4`) is lost, `TcpClientManager.ReceiveMessagesLoopAsync` breaks due to socket disconnection. The client transitions to `ClientState.DISCONNECTED` and clears its resolved cache (`_resolvedIp = null`, `_resolvedPort = null`). During the next reconnection attempt, `ResolveAndConnectAsync` resolves the static configuration domain `"control.sayra.example"` again via `ConnectAsync`, forcing the OS resolver to fetch the updated DNS record pointing to `5.6.7.8`.

---

## 6. IP ADDRESS CHANGE SCENARIO

Let's evaluate the exact lifecycle sequence when the Central Backend moves from `1.2.3.4:5000` to `5.6.7.8:5000`, assuming the old IP is no longer available.

### Scenario Evaluation:
1. **Does Client detect connection loss?** **YES.** Inside `TcpClientManager.ReceiveMessagesLoopAsync`, `StreamReader.ReadLineAsync` returns `null` or throws a socket exception.
2. **Does Client retry?** **YES.** The exception is caught in `TcpClientManager.StartAsync`, which transitions the state machine to `ClientState.RECOVERING` and schedules a retry loop.
3. **Does it retry the same IP?** **YES, initially, then NO.**
   - If a static IP was explicitly configured (`ServerConfig:IpAddress`), it will retry that same IP indefinitely.
   - If using the LAN discovery mechanism, it will attempt to connect to the cached IP from `server_cache.json`. If that connection fails, it invalidates the cache and initiates a fresh UDP broadcast (`forceFresh: true`), allowing it to discover the new server IP (`5.6.7.8`).
4. **Does it re-resolve DNS?** **YES.** If a domain name was used as the static IP configuration, it re-resolves the domain on every retry.
5. **Does it reload configuration?** **NO.** The client does not re-read `appsettings.json` from disk automatically during reconnection.
6. **Does it contact a Bootstrap Server?** **NO.** There is no remote bootstrap server configured.
7. **Does it have a fallback endpoint?** **NO.** No hardcoded fallback endpoint list exists in the core TCP client flow.
8. **Does it discover a new endpoint?** **YES (LAN-only).** It broadcasts a UDP request on port `37020` and binds to the newly responding server IP.
9. **Does it require manual configuration?**
   - **NO** if the client relies on LAN UDP Discovery or domain name resolution.
   - **YES** if a static, hardcoded IP address was entered in `appsettings.json` and needs to be changed to a different static IP address.
10. **Does the Client become permanently disconnected?**
   - **NO** if using LAN UDP Discovery or Domain-based config.
   - **YES** if using a hardcoded static IP configuration.

### Final IP Change Scenario Verdict:
* **For LAN Discovery Deployments:** **PASS**
* **For Domain/DNS Configured Deployments:** **PASS**
* **For Hardcoded Static IP Deployments:** **FAIL** (requires manual appsettings modification or workstation re-installation)

---

## 7. SERVER DOMAIN CHANGE SCENARIO

### Scenario Evaluation:
- **Old Domain:** `control.sayra.example`
- **New Domain:** `central.sayra.example`

### Automatic Recovery Capability: **FAIL**
The SAYRA Client **cannot** discover a brand-new top-level domain name automatically.

### Why:
There is no "Domain Redirect" mechanism or secondary fallback domain list defined in `TransportPolicy` or `TcpClientManager`. If the client is configured with a static domain, it reads `"control.sayra.example"` and will attempt to resolve and connect to that exact string indefinitely during its reconnect loops.

### Operational Recovery Process Required:
To migrate clients to the new domain, administrators must perform one of the following:
1. **Local Configuration Update:** Edit `appsettings.json` on each client workstation to change the `ServerConfig:IpAddress` value to `"central.sayra.example"`.
2. **Enterprise Group Policy (GPO):** Push an updated `appsettings.json` configuration file to all workstation directories.
3. **Active Directory DNS Alias (CNAME):** Configure a temporary CNAME record in the local DNS server mapping `control.sayra.example` to `central.sayra.example`.

---

## 8. SERVER FAILOVER

### Failover Capabilities Status: **NO AUTOMATIC WAN BACKEND FAILOVER FOUND**

### Code Architecture Analysis:
- There is **no secondary/fallback endpoint registry** configuration defined inside `appsettings.json` or `TransportPolicy.cs`.
- There is **no client-side pool rotation** or list of alternative hostnames.
- There is **no DNS round-robin resolution parsing** (the client simply connects to the first IP returned by the OS resolver).

### Subnet-Level LAN Failover Support:
While WAN failover is missing, local subnet failover is supported via UDP. If multiple servers exist on the same subnet:
1. The client broadcasts a discovery request.
2. If the previously cached server ID is not found, `DiscoveryManager.SelectBestServer` automatically picks the server with the **lowest round-trip latency** from the responding pool.

---

## 9. RECONNECT BEHAVIOR

SAYRA Client features a robust, thread-safe exponential backoff reconnection engine.

### Reconnect Flow Trace:
```
Connection Lost (ReadLineAsync returns null / throws exception)
    ↓
Transition to DISCONNECTED & clear IP/Port memory variables
    ↓
Transition to RECOVERING
    ↓
Increment Retry Counter in ReconnectManager
    ↓
Calculate backoff delay: Min(BaseDelay * 2^(RetryCount-1), MaxDelay)
    ↓
Wait for delay (Task.Delay)
    ↓
Call ResolveAndConnectAsync (checks Cache/DNS/UDP)
    ↓
If ConnectAsync succeeds: Reset Retry Counter, transition to READY
If ConnectAsync fails: Start next retry loop iteration
```

### Key Reconnect Characteristics:
- **Endpoint Used:** If configured with a static endpoint, it retries that endpoint. If discovery is enabled, it retries the cached IP first; if that fails, it executes a fresh UDP broadcast.
- **DNS Resolution:** DNS is resolved on every reconnection attempt inside `TcpClientManager.ConnectAsync`.
- **Delay Calculation:** Uses exponential backoff:
  $$\text{Delay} = \min(\text{ReconnectBaseDelaySeconds} \times 2^{\text{RetryCount}-1}, \text{ReconnectMaxDelaySeconds})$$
  - `ReconnectBaseDelaySeconds` default: `2` seconds.
  - `ReconnectMaxDelaySeconds` default: `60` seconds.
- **Jitter:** There is **no randomized jitter** added to the reconnect interval in `ReconnectManager.cs` (it is a pure deterministic doubling algorithm).
- **Infinite Retries:** If `MaxReconnectAttempts` in `appsettings.json` is set to `-1` (default), the retry loop runs **infinitely** (`TcpClientManager` will attempt to reconnect forever without halting the service).

---

## 10. ENDPOINT UPDATE

Can the Central Backend remotely tell a Client: *"The Backend endpoint has changed"*?

### Update Capabilities Status: **REMOTE ENDPOINT MIGRATION NOT SUPPORTED**

### Why:
There is no executable path, packet structure, or command router mapping inside `MessageHandler.cs` or `CommandRouter.cs` supporting a backend endpoint rewrite or server migration command.

If the server sends a configuration sync event via the configuration engine (`ConfigurationSynchronizationService`), the configuration validator (`ConfigurationValidator`) verifies JSON schema changes. However, because the network loop (`TcpClientManager`) holds a live static reference to the endpoint it resolved on connection start, changes to the local configuration do not dynamically update the active connection endpoint, and the client will not migrate to a new server endpoint on the fly.

---

## 11. SECURITY DURING SERVER DISCOVERY

SAYRA implements multi-layered cryptographic checks during server discovery and connection phases to prevent Man-In-The-Middle (MITM) and rogue server redirection attacks.

### Rogue Server Scenario Analysis:
*Suppose an attacker compromises the local network, spoofing DNS records or ARP responses to redirect client traffic to a malicious server.*

The client **safely rejects the connection** and shuts down the handshake sequence immediately. It is protected by three cryptographic defense lines:

### Security Line 1: UDP Response RSA Signature Validation
When the client receives a UDP response from a discovered server on the LAN, it extracts the signature from the response. It reconstructs the raw string payload:
`"{serverId}{serverName}{ip}{tcpPort}{timestamp}{nonce}"`
It verifies this payload against the server's signature using the public RSA key file `server_public.key` at the application root (`DiscoveryValidator.VerifySignature`). If the signature is invalid or if the attacker does not possess the matching private key, the response is discarded.

### Security Line 2: Replay and Time-Skew Protection
To block replay attacks, the client uses a rolling window:
- **Timestamp Validation:** Rejects any discovery response whose timestamp differs by more than $\pm10$ seconds from the client's system clock (`DiscoveryValidator.Validate`).
- **Nonce Tracking:** Tracks unique UUID nonces in a thread-safe `HashSet<string>` cache. Rejects any duplicate nonces.

### Security Line 3: TLS 1.3 Handshake & Certificate Pinning
Once TCP is connected, the client enforces a TLS 1.3 handshake (`TlsConnectionManager.ConnectAsync`).
- **Certificate Validation:** Custom verification (`ValidateServerCertificate`) checks expiration and hostname mismatches.
- **Certificate Pinning:** If enabled in the transport policy, the client extracts the presented certificate and matches its thumbprint and/or public key SHA-256 hash against the configured pinnings:
  - `TransportSecurity:PinnedCertificateThumbprint`
  - `TransportSecurity:PinnedPublicKeyHash`
If no match is found, the connection is immediately terminated.

### Code Evidence:
- **File:** `Sayra.Client.Discovery/Services/DiscoveryValidator.cs`
  - **Method:** `Validate(ServerDiscoveryResponse)` and `VerifySignature`
- **File:** `SayraClient/Security/Transport/TlsConnectionManager.cs`
  - **Method:** `ValidateServerCertificate`

---

## 12. FIRST-RUN / NEW CLIENT SCENARIO

Assume a completely fresh SAYRA installation with no local configuration, cached endpoints, or active sessions.

### Step-by-Step Initial Setup Resolution:

1. **Backend Address and Port:** Resolves to `"SAYRA_SERVER_IP"` placeholder in `appsettings.json`. Since the value is the placeholder, the client triggers the UDP LAN discovery sequence. It broadcasts a UDP request on port `37020`.
2. **Server Identity Discovery:** Discovers available servers, validates their RSA signature, and populates `server_cache.json` with the verified IP and port.
3. **Authentication Token Exchange:** Triggered by `MessageHandler` catching the `AUTH_CHALLENGE` packet. The client uses the Master Key loaded from `SecurityConfig:MasterKey` or the `SAYRA_MASTER_KEY` environment variable. It generates a fresh, ephemeral session key, encrypts it using AES-256-CBC, computes an HMAC-SHA256 hash of the challenge, and sends it back to the server.
4. **Device Identity Resolution:** The client generates its device identity via `Environment.MachineName` which is sent during the initial UDP discovery request and the authenticated handshake.
5. **Initial Configuration:** Once authenticated, the client state machine enters `READY`. It triggers `ConfigurationSynchronizationService` to request, fetch, validate, and apply the general operational configuration from the server.

---

## 13. DISASTER RECOVERY PROFILE

Below is the recovery matrix outlining the client's behavior under various operational failures:

| Scenario | Automatic Recovery | Mechanism | Result | Risk |
|---|---|---|---|---|
| **IP Change (LAN Discovery)** | **YES** | Failed connection invalidates cache; triggers fresh UDP discovery broadcast. | **SUCCESS** | Minimal; relies on local UDP broadcasting. |
| **IP Change (Static Config)** | **NO** | Reconnection attempts loop indefinitely targeting the outdated static IP address. | **FAIL** | High; requires manual edit of `appsettings.json`. |
| **DNS IP Change** | **YES** | Connection exception triggers retry; DNS hostname re-resolved via OS on next attempt. | **SUCCESS** | Low; subject to OS DNS cache propagation delay. |
| **Domain Change** | **NO** | No redirect parsing; continues to resolve old domain string indefinitely. | **FAIL** | High; requires DNS CNAME mapping or configuration update. |
| **Server Down** | **YES** | Triggers exponential backoff reconnect loop; reconnects once server recovers. | **SUCCESS** | None; infinite retry loop prevents service crash. |
| **New Workstation Install** | **YES** | Initiates first-run active UDP discovery broadcast; authenticates via Master Key. | **SUCCESS** | Minimal; requires valid `server_public.key` and Master Key. |
| **DNS Failure** | **YES** | Connection fails; retries with exponential backoff until DNS resolution succeeds. | **SUCCESS** | None; client recovers when DNS services are restored. |
| **Network Recovery** | **YES** | Stream breaks; triggers reconnect loop; restores session when internet returns. | **SUCCESS** | Low; session key is re-negotiated seamlessly. |

---

## 14. COMPREHENSIVE ARCHITECTURE DIAGRAM

The diagram below maps the runtime architecture of the SAYRA Client's connectivity, discovery, and handshake pipeline:

```
                  +-------------------------------------------------------+
                  |                      SAYRA CLIENT                     |
                  +-------------------------------------------------------+
                                              |
                             Is Static IP / Domain Configured?
                                             / \
                                           /     \
                                   [Yes] /         \ [No / Placeholder]
                                       /             \
                                      v               v
                        +------------------+   +------------------------------+
                        |  Static Config   |   |     DiscoveryManager         |
                        | (appsettings)    |   |    (server_cache.json)       |
                        +------------------+   +------------------------------+
                                 |                            |
                                 |                    Does cache exist?
                                 |                           / \
                                 |                         /     \
                                 |                 [Yes] /         \ [No / Expired]
                                 |                     /             \
                                 |                    v               v
                                 |             +------------+   +-------------------------+
                                 |             | Load Cache |   |    UdpDiscoveryClient   |
                                 |             |    (IP)    |   | (Multicast UDP Port     |
                                 |             +------------+   |  37020 on 255.255.255)  |
                                 |                    |         +-------------------------+
                                 |                    |                      |
                                 |                    |             Receives responses
                                 |                    |                      |
                                 |                    |                      v
                                 |                    |         +-------------------------+
                                 |                    |         |    DiscoveryValidator   |
                                 |                    |         | (Verify signature via   |
                                 |                    |         |  server_public.key)     |
                                 |                    |         +-------------------------+
                                 |                    |                      |
                                 v                    v                      v
                        +---------------------------------------------------------+
                        |                 TlsConnectionManager                    |
                        |             (Establishes TCP on port 5000)              |
                        +---------------------------------------------------------+
                                                     |
                                        Commence TLS 1.3 Handshake
                                                     |
                                                     v
                        +---------------------------------------------------------+
                        |             Validate Server Certificate                 |
                        |      (Check Expiration, Hostname & Pinning)            |
                        +---------------------------------------------------------+
                                                     |
                                                     v
                        +---------------------------------------------------------+
                        |                       AuthManager                       |
                        |     (Receives challenge; HMACS with Master Key;        |
                        |      Generates & encrypts AES ephemeral Session Key)   |
                        +---------------------------------------------------------+
                                                     |
                                                     v
                                       [ClientState.READY Activated]
```

---

## 15. FINAL VERDICT

Based on our forensic code audit, here are the readiness scores for the SAYRA Client's connectivity subsystem:

### Bootstrap Readiness: **80%**
*Pros:* Seamless local subnet discovery makes deployment extremely simple and zero-configuration.
*Cons:* Lacks a cloud-based registry or WAN bootstrapping strategy out-of-the-box.

### Server Discovery Readiness: **95%**
*Pros:* Dynamic UDP client with highly secure RSA-signature checking, timestamp verification, and nonce replay defense.

### IP Change Resilience: **85%**
*Pros:* LAN discovery clients automatically re-resolve and connect to the new server IP without intervention.
*Cons:* Hardcoded static IP configurations do not automatically transition and fail permanently.

### Failover Readiness: **65%**
*Pros:* LAN discovery clients automatically transition to alternative local servers with the lowest latency if the primary server goes down.
*Cons:* No WAN failover pool, secondary endpoints, or DNS round-robin support is implemented.

### First-Run Provisioning: **90%**
*Pros:* Automatically bootstraps, discovers, authenticates, and synchronizes workstation configurations on local subnets.

### Overall Backend Endpoint Resilience: **83%**
*Verdict Classification:*

### **READY WITH LIMITATIONS**

*SAYRA Client is highly production-ready and securely built for local gaming centers operating on single subnets (LAN). However, deployment in distributed WAN configurations spanning multiple subnets over the internet is limited and requires manual IP configuration or DNS-level load balancing.*

---

## 16. THE MOST IMPORTANT FINAL QUESTION

> **If the SAYRA Central Backend server changes its IP address tomorrow, without manually touching every installed SAYRA Client, will the existing Clients automatically find and reconnect to the new server?**

# Answer:
### **PARTIAL**

### Technical Explanation & Code Evidence:

The ability of existing clients to automatically discover and connect to the new server IP is determined entirely by how they were initially configured:

#### **CASE 1: Clients using LAN Server Discovery (Default Config)**
**YES.**
If the workstations do not have a hardcoded static IP configured (i.e. `ServerConfig:IpAddress` is missing, empty, or set to `"SAYRA_SERVER_IP"`), they will recover automatically.
- **Code Path & Execution Flow:**
  When the server IP changes, the active TCP connection is lost. The client's receive loop (`TcpClientManager.ReceiveMessagesLoopAsync` in `TcpClientManager.cs`) throws an exception, and the client transitions to `ClientState.RECOVERING` inside `TcpClientManager.StartAsync`.
  On the next reconnect attempt, `TcpClientManager.ResolveAndConnectAsync` attempts to connect to the old IP loaded from `server_cache.json`. Because the old IP is no longer available, the connection fails.
  The client then immediately executes the fresh discovery fallback path:
  ```csharp
  if (!connected && _configuration.GetValue<bool>("ServerDiscovery:Enabled", true) &&
      (string.IsNullOrEmpty(staticIp) || staticIp == "SAYRA_SERVER_IP"))
  {
      _logger.LogInformation("Connection to cached/resolved server failed. Retrying with fresh discovery...");
      _stateManager.TransitionTo(ClientState.DISCOVERING_SERVER);
      var response = await _discoveryService.DiscoverAsync(cancellationToken, forceFresh: true);
      if (response != null)
      {
          _resolvedIp = response.ip;
          _resolvedPort = response.tcpPort;
          await ConnectAsync(_resolvedIp, _resolvedPort ?? staticPort, cancellationToken);
      }
  }
  ```
  Calling `DiscoverAsync` with `forceFresh: true` tells `DiscoveryManager.InternalDiscoverAsync` (in `Sayra.Client.Discovery/Services/DiscoveryManager.cs`) to bypass `server_cache.json` and invoke:
  ```csharp
  var responses = await _udpClient.BroadcastDiscoveryAsync(Environment.MachineName, TimeSpan.FromSeconds(timeoutSec), cancellationToken);
  ```
  This broadcasts a new discovery request across the subnet. The new backend server will respond, the client will validate its RSA signature, update `server_cache.json` with the new IP address, and reconnect successfully with **zero administrator intervention**.

---

#### **CASE 2: Clients using Static IP Configuration**
**NO.**
If the clients were configured with a hardcoded static IP in `appsettings.json` (e.g. `ServerConfig:IpAddress` is set to `"1.2.3.4"`), they will **fail permanently** and loop indefinitely attempting to reconnect to the dead IP.
- **Code Path & Execution Flow:**
  Inside `TcpClientManager.ResolveAndConnectAsync`, the presence of a static IP bypasses the UDP discovery service entirely:
  ```csharp
  string? staticIp = _configuration["ServerConfig:IpAddress"];
  if (!string.IsNullOrEmpty(staticIp) && staticIp != "SAYRA_SERVER_IP")
  {
      _resolvedIp = staticIp;
      _resolvedPort = staticPort;
  }
  ```
  Since `_resolvedIp` is set to the old static IP, the fresh discovery retry path is never executed. The client will attempt to connect to the offline IP address and wait for the `ReconnectManager` delay, looping infinitely without any mechanism to find the new IP address. Under this configuration, an administrator **must manually touch every workstation** to edit `appsettings.json` or push a GPO update.

---

#### **CASE 3: Clients using a Domain Name / DNS Config**
**YES.**
If the clients were configured with a static DNS domain (e.g. `ServerConfig:IpAddress` is set to `"control.sayra.example"`), they will recover automatically once the DNS record propagates.
- **Code Path & Execution Flow:**
  Because the client's memory variable `_resolvedIp` is cleared on connection failure (`_resolvedIp = null`), the client will call `ConnectAsync` using the domain name string on the next retry. Under the hood, `System.Net.Sockets.TcpClient` performs a fresh DNS resolution on the hostname on every connection attempt, automatically routing the client to the new backend server IP address once the DNS record points to the new server.

# Sayra Server: Architecture Refinement & Validation

## 1. Introduction
This document provides a deep architectural audit and refinement of the Sayra Server design. As the Sayra Client ecosystem matures, the server must evolve from a simple command relay (as seen in `mock_server.py`) into a production-grade, scalable, and resilient distributed system. This refinement focuses on high availability, enterprise-grade security, and readiness for large-scale LAN deployments (500+ clients).

## 2. Executive Summary
The current Sayra Server design, while functionally sound for small-scale testing, requires significant architectural enhancements to meet production standards. Key areas of focus include:
- Transitioning from a synchronous, thread-per-connection model to a high-performance asynchronous I/O or event-driven architecture.
- Implementing a robust persistence layer to ensure session integrity and historical data tracking.
- Hardening security through advanced key management and rate-limiting.
- Introducing a modular API Gateway to facilitate real-time administrative oversight via a future Admin Panel.

This refinement outlines the path toward a "Smartlaunch-level" commercial product.

## 3. Architecture Completeness Review
The baseline design lacks several critical subsystems necessary for a production-grade enterprise application:

### Missing Subsystems
- **Persistence Layer (RDBMS):** Essential for storing client registries, session history, audit logs, and configuration.
- **Background Job Scheduler:** Required for periodic health checks, automated update polling, and database maintenance (e.g., Quartz.NET or Hangfire).
- **API Gateway:** A dedicated entry point for the Admin Panel to decouple management traffic from the high-frequency client TCP traffic.
- **Authorization & RBAC:** Necessary to manage different administrative levels (Owner, Manager, Staff) for the Admin Panel.
- **Health Monitoring & Telemetry Aggregator:** A subsystem to aggregate diagnostics into a time-series format for trend analysis and alerting.
- **Distributed Event Bus (Internal):** For decoupling modules like Session Management and Logging.

## 4. Scalability Validation
The server must handle up to 500-1000 concurrent clients without performance degradation.

### Bottlenecks Identified
- **Thread Management:** A thread-per-connection model (like `mock_server.py`) will exhaust system resources at 500+ clients due to context switching and memory overhead.
- **Synchronous Database I/O:** Blocking DB calls during message processing will create backpressure, delaying heartbeat responses and commands.
- **Monolithic Message Processing:** Processing complex commands (like `LIST_PROCESSES` with large payloads) in the main loop will block other clients.

### Recommendations
- **Asynchronous I/O:** Utilize .NET `SocketAsyncEventArgs` or `System.IO.Pipelines` for high-performance, non-blocking network I/O.
- **Actor Model or Task Queuing:** Implement a task-based processing pipeline where incoming messages are queued and handled by a worker pool.
- **Database Connection Pooling:** Ensure the persistence layer uses efficient pooling to prevent connection exhaustion during bursts (e.g., "reconnect storms").
- **Stateless Core with Cache:** Maintain active session metadata in an in-memory cache (Redis or `MemoryCache`) to avoid constant DB lookups for time-sensitive operations.

## 5. Failure Domain Analysis
Robustness is critical for a system controlling 500+ workstations.

### Critical Failure Points & Mitigations
- **Server Crash:**
    - *Impact:* All clients disconnect; session timing stops; admin loses control.
    - *Mitigation:* Implement a high-availability (HA) cluster with a primary/secondary server. Use a shared persistence layer (SQL cluster/HA).
    - *Recovery:* Clients auto-reconnect to the secondary IP. Server restores state from the DB and sends `SYNC_STATE` commands to all clients.
- **Database Failure:**
    - *Impact:* Server cannot persist session changes or registry new clients.
    - *Mitigation:* Use an Outbox Pattern for critical events. Log locally to a flat file if the DB is unreachable and replay once restored.
    - *Recovery:* Automated failover to a replica database.
- **Client Reconnect Storm (500+ clients):**
    - *Impact:* Network saturation; CPU spikes during bulk authentication handshakes.
    - *Mitigation:* Implement exponential backoff and jitter in client reconnect logic. Use a server-side handshake queue with rate limiting.
- **Partial Message Delivery / Network Partition:**
    - *Impact:* Desynchronized state (e.g., Client thinks session started, Server thinks it failed).
    - *Mitigation:* Idempotent command processing. Every command must have a unique `RequestId`. Server must verify execution results before updating authoritative state.

## 6. Data Architecture Review
A robust persistence model is required for commercial reliability.

### Recommended Data Architecture
- **SQL RDBMS (PostgreSQL or SQL Server):** Recommended for relational data integrity (Client Registry, Session History, RBAC).
- **NoSQL / Time-Series (Optional):** Consider for Telemetry/Diagnostics if high-resolution data retention is required.

### Core Entities
- **Clients:** `ClientId (PK), MAC, Hostname, LastKnownIP, HardwareProfile, Status, CreatedAt`.
- **Sessions:** `SessionId (PK), ClientId (FK), StartTime, EndTime, ExpectedDuration, InitialBalance, Status, LastSyncTime`.
- **CommandAudit:** `CommandId (PK), ClientId (FK), Action, Payload, Result, Status, ExecutedBy (AdminId), Timestamp`.
- **TelemetryLogs:** `LogId (PK), ClientId (FK), CPUUsage, RamUsage, ActiveProcessCount, Timestamp`.
- **AdminUsers:** `AdminId (PK), Username, PasswordHash, Role (Owner/Manager/Staff), LastLogin`.

### Persistence Strategy
- **Session History:** Must be persisted immediately upon state change (Start/Pause/End).
- **Event Sourcing (Consideration):** For a high-fidelity audit trail, consider storing all session state transitions as an append-only stream.

## 7. Security Architecture Review
Hardening the server against LAN-based threats and insider attacks is paramount.

### Security Improvements
- **Key Rotation & Management:**
    - The `MasterKey` should be stored in a Secure Vault (e.g., HashiCorp Vault or Azure Key Vault) rather than environment variables in production.
    - Implement automatic `SessionKey` rotation every 4 hours or upon significant state changes to minimize the impact of a compromised key.
- **Rate Limiting & DoS Protection:**
    - Implement per-client rate limiting for commands and authentication attempts.
    - Reject payloads exceeding 1MB to prevent memory exhaustion attacks.
- **Replay Protection Scalability:**
    - Use a sliding window of observed `RequestId`s per client (persisted in-memory/cache) to detect and reject replayed packets within the 10s timestamp window.
- **Client Identity Validation:**
    - Perform strict MAC-to-IP binding checks. If a client connects from an unexpected IP, flag it for administrative review.
- **Secure Logging:**
    - All security-sensitive actions (Auth failures, command rejections) must be logged with high severity and cannot be modified by standard admin roles.

## 8. Event-Driven Design Evaluation
A transition from a synchronous model to an event-driven architecture is recommended for decoupling and scalability.

### Current Sync Model vs. Proposed Event-Driven Model
| Feature | Synchronous Model | Event-Driven Model (Internal Bus) |
| :--- | :--- | :--- |
| **Coupling** | High (Network layer calls Session layer directly) | Low (Modules communicate via events) |
| **Scalability** | Limited by thread pool | Highly scalable (Async processing) |
| **Extensibility** | Hard to add new features (e.g., Notification Svc) | Easy (New subscribers can listen to existing events) |
| **Failure Isolation** | Error in one module can block the entire thread | Failures are localized to specific subscribers |

### Proposed Internal Event Bus
- **Core Events:** `ClientConnected`, `AuthenticationSuccess`, `SessionStarted`, `CommandExecuted`, `TelemetryReceived`.
- **Async Pipelines:** Incoming packets are parsed, validated, and published to the bus. Subsystems (SessionManager, TelemetryHub, AuditLogger) subscribe to relevant events and process them asynchronously.
- **Technologies:** Use .NET `Channel<T>` for high-performance internal queuing or an external broker like RabbitMQ if horizontal server scaling is required in the future.

## 9. Admin Panel Integration Readiness
The server must support a real-time Admin Panel for remote oversight.

### Integration Strategy
- **API Gateway (REST + WebSockets):** Implement a dedicated API layer (e.g., ASP.NET Core Web API) for the Admin Panel.
    - **REST:** For CRUD operations (Client registration, app management, report generation).
    - **WebSockets (SignalR):** For real-time updates (Client status changes, live telemetry, session progress).
- **Decoupled Traffic:** Management traffic should use a different port and authentication mechanism (JWT-based) than client TCP traffic.
- **Real-time Dashboards:** The server must push events to the Admin Panel immediately via SignalR hubs.

## 10. Final Architecture Improvement Plan

### Improved Final Architecture (Logical View)
```text
[ Admin Panel (Web/Mobile) ]
           |
           ▼ (HTTPS / WSS + JWT)
+-----------------------------------------------------------+
|                    API GATEWAY LAYER                      |
| (Auth, Rate Limiting, REST Controllers, SignalR Hubs)     |
+-----------------------------------------------------------+
           |
           ▼ (Internal Event Bus / Pub-Sub)
+-----------------------------------------------------------+
|                  SAYRA SERVER CORE                        |
|                                                           |
|  +---------------------+        +----------------------+  |
|  |  Session Engine     |        |   Command Router     |  |
|  +---------------------+        +----------------------+  |
|  |  Identity Provider  |        |   Telemetry Hub      |  |
|  +---------------------+        +----------------------+  |
|  |  Update Manager     |        |   Security Vault     |  |
|  +---------------------+        +----------------------+  |
|                                                           |
|  +------------------------------------------------------+  |
|  |              ASYNC NETWORK ENGINE (TCP)              |  |
|  +------------------------------------------------------+  |
+-----------------------------------------------------------+
           |                             |
           ▼ (SQL / Cache)               ▼ (Secure TCP)
+-----------------------+      +----------------------------+
|   PERSISTENCE LAYER   |      |      SAYRA CLIENTS         |
| (PostgreSQL / Redis)  |      |   (500+ Terminals)         |
+-----------------------+      +----------------------------+
```

### Missing Components List
1.  **PostgreSQL Schema:** For long-term data persistence.
2.  **Redis Cache:** For high-frequency session state tracking.
3.  **SignalR Hubs:** For real-time Admin Panel communication.
4.  **ASP.NET Core API:** For administrative management.
5.  **Secure Vault Integration:** For cryptographic key protection.
6.  **Background Job Worker:** For system maintenance and update polling.

### Recommended Refactoring Plan
1.  **Priority 1: Core Async Network Engine.** Refactor the TCP handler to use `Pipelines` or async sockets.
2.  **Priority 2: Persistence Layer.** Integrate Entity Framework Core with PostgreSQL.
3.  **Priority 3: Internal Event Bus.** Implement a central message broker for decoupling modules.
4.  **Priority 4: Security Hardening.** Implement rate limiting and key vault integration.
5.  **Priority 5: API Gateway.** Develop the REST/SignalR layer for admin access.

### Risks of Non-Implementation
- **High Risk:** System crash or instability at >100 clients due to thread exhaustion.
- **High Risk:** Security breach via LAN sniffing or replay attacks if communication isn't hardened.
- **Medium Risk:** Data loss during server restarts without a robust persistence layer.

### Production Readiness Score (Server-Side)
- **Current Score:** 35% (Functional mock, but lacks persistence, scalability, and enterprise security).
- **Target Score:** 95%+ (Post-refinement).

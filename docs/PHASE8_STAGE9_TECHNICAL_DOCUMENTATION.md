# PHASE 8, STAGE 9 — Enterprise Dashboard Provider & Monitoring Integration
## Technical Documentation

This document describes the design, pipeline architecture, snapshot generation workflow, caching, refresh strategies, read models, and integration details of the **Enterprise Dashboard Provider** for the SAYRA Client.

---

## 1. Architecture Overview

The Dashboard Provider is a presentation and status aggregation layer situated within the clean architecture layout of the SAYRA client application. It serves as a unified entry point for UI dashboards (WPF local console, future desktop and web consoles, REST APIs, or SignalR feeds) to obtain real-time workstation status, metrics summaries, security compliance rating, alert states, and individual subsystem diagnostics.

In conformance with clean architecture guidelines, the Dashboard Provider is **strictly a read-only data provider/aggregator**:
- It **does NOT** collect telemetry data on its own.
- It **does NOT** trigger, store, or modify alerts.
- It **does NOT** execute active recovery or healing routines.
- It is transport-agnostic and maintains isolation between presentation details and underlying observability engine operations.

```
                  ┌──────────────────────────────────────────────┐
                  │                 Dashboard UI                 │
                  │   (WPF local, Desktop, REST API, SignalR)    │
                  └──────────────────────┬───────────────────────┘
                                         │
                                         ▼
                  ┌──────────────────────────────────────────────┐
                  │              IDashboardProvider              │
                  │             (DashboardProvider)              │
                  └──────┬────────────────────────────────┬──────┘
                         │                                │
                         ▼ (Failure Isolated Queries)     ▼ (Cached Read Models)
            ┌───────────────────────────┐    ┌───────────────────────────┐
            │   Telemetry & Monitoring  │    │     Lightweight Cache     │
            │    Existing Components    │    │      (SemaphoreSlim)      │
            └───────────────────────────┘    └───────────────────────────┘
```

---

## 2. Dashboard Snapshot Generation Pipeline

The `DashboardProvider` uses a pull-based asynchronous execution model to construct the unified `DashboardSnapshot` and specific read models.

### Data Aggregation Workflow
1. **Telemetry & Core Metrics**: Queries `ILiveTelemetryService.CaptureSnapshotAsync` to extract CPU usage, RAM utilization percentages, local drive space metrics, and network connectivity state.
2. **Performance Snapshot**: Queries `IPerformanceMonitor.GetLatestPerformanceSnapshotAsync` to read database operation latencies, IPC pipe latency, server TCP connections latency, download speed across active pipelines, upload speeds, CLR metrics, and offline persistent queue lengths.
3. **Active Alerts**: Queries `IAlertEngine.GetActiveAlertsAsync` to determine total count of active alert records and group them to construct a detailed priority breakdown.
4. **Workstation Activity Sessions**: Queries `ISessionRepository.GetActiveSessionsAsync` to count unique interactive users currently authenticated, and actively running game processes.
5. **Subsystem Health & Failures**: Queries `IHealthMonitor.GetDetailedHealthAsync` and `IHealthMonitor.GetHealthSummaryAsync` to compute total system failure events, recovery events, and map the health status of all 15 core subsystems.
6. **Security Hardening**: Queries `ISecurityHardeningService.VerifySystemIntegrityAsync` and `ISecurityHardeningService.ValidatePolicyAsync` to evaluate security compliance percentage and overall system integrity states.

---

## 3. Read Models

The Dashboard Provider serves seven optimized, immutable, type-safe read models:

| Read Model | Purpose & Monitored Metrics |
|---|---|
| **Overview** | High-level summary of active workstation, authenticated online users, running games, and general health rating. |
| **Subsystem Status** | Full status details (Health, Status Message, Last Updated, and Active Issues) for all 15 core components. |
| **Performance Summary** | Real-time system CPU/Memory, network speeds, local storage Disk I/O latencies, DB/IPC latencies, and offline queue length. |
| **Alert Summary** | Active unhandled alert count, detailed alert records list, and breakdown count by alert priority level. |
| **Security Summary** | Detailed posture report featuring security violations count, compliance rating, anti-tamper, and encryption states. |
| **Recovery Summary** | Summary of self-healing orchestrations, failure counters, total successful recoveries, and failure statistics. |
| **Compliance Summary** | Update eligibility rating, rollout status, pending updates, and applied policies. |

---

## 4. Cache & Refresh Strategies

To minimize CPU usage, avoid thread-blocking, and scale concurrent client calls, the provider implements a **lightweight, thread-safe, non-blocking cache**:

### 4.1 Configurable Refresh Interval
Driven by `DashboardOptions.RefreshIntervalSeconds` (configured in `appsettings.json` under `Observability:Dashboard`). If a snapshot or read model is requested, and the cached snapshot is younger than the configured interval, the cached instance is returned instantly without querying any service.

### 4.2 Automatic Invalidation & Non-Blocking Rebuilds
If the cache is stale (older than the refresh interval), a rebuild is initiated. To guarantee that multiple concurrent client threads do not overload the system by triggering duplicate re-evaluation loops, the provider checks the lock with `WaitAsync(0)` (zero timeout). If the rebuild lock is already held, concurrent readers immediately receive the currently cached snapshot (stale-while-rebuild pattern), eliminating thread pool starvation and blocking.

### 4.3 Manual and Scheduled Refresh
- **Manual**: Exposes `RefreshAsync` on `IDashboardProvider` to bypass cache timers and force a complete rebuild of the snapshot and all read models.
- **Scheduled Subscription**: Stream-based updates can be subscribed to via `StreamDashboardUpdatesAsync(onUpdate, CancellationToken)`. The loop runs on a background task, calling the refresh pipeline at the configured interval and dispatching snapshot updates to subscribers until cancellation is requested.

---

## 5. Enterprise Monitoring View & Failure Isolation

Each of the 15 subsystems is represented within the `DashboardSubsystemStatusReadModel`:
1. **Authentication**
2. **Database**
3. **Network**
4. **IPC**
5. **Notifications**
6. **Downloads**
7. **Updates**
8. **Plugins**
9. **Telemetry**
10. **Recovery**
11. **Security**
12. **Policies**
13. **Watchdog**
14. **Overlay**
15. **Synchronization**

### 5.1 Failure Isolation Policy
If a monitored engine or subsystem service throws an exception or is unregistered during snapshot generation:
- The exception is captured, logged, and isolated.
- The failing subsystem's health is marked as `"Warning"`, `"Critical"`, or `"Offline"` with description of the error added to `ActiveIssues`.
- **The overall snapshot generation continues successfully**, populating all other subsystems and fields without crashing or interrupting the console or REST/SignalR transport pipelines. This ensures high availability of the dashboard system under degraded system states.

---

## 6. Integration and Extension Points

### 6.1 Dependency Injection
The dashboard provider is registered inside the DI container as a single Singleton instance:
```csharp
services.AddSingleton<IDashboardProvider, DashboardProvider>();
```
Any presentation layer (WPF views, background workers, RPC controllers) can consume the provider via standard constructor dependency injection.

### 6.2 Transport Agnostic Design
`IDashboardProvider` does not depend on ASP.NET Core, SignalR Hubs, or WPF UI classes. This makes it cleanly reusable in:
- A local WPF Administration overlay.
- An ASP.NET Core REST Controller action.
- A SignalR background broadcaster Hub.
- A remote gRPC worker service.

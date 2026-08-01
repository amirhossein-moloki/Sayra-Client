# Sayra Client - Observability Platform
## PHASE 8 — STAGE 6: Enterprise Diagnostics Platform Technical Documentation

## 1. Architectural Overview & Boundaries

The Stage 6 Enterprise Diagnostics Platform is a high-performance, resilient, and fully asynchronous subsystem within the SAYRA Client's Observability platform. It is engineered to analyze workstation health status, aggregate subsystem findings, and generate actionable administrative recommendations.

### Core Architectural Separation
The platform operates on a strict **Analyze-Only** model:
* **Diagnostics Analyzes Data:** It consumes and interprets existing live state from other services (Performance Monitors, Security Hardeners, Resource Monitors, etc.).
* **Diagnostics Does Not Collect Raw Telemetry:** It does not query OS APIs directly for telemetry values or run low-level hardware loop queries. That is the responsibility of Stage 2 Telemetry Collectors.
* **Diagnostics Does Not Self-Heal:** It identifies anomalies and generates structured recommendations, but never automatically triggers mitigation. Self-healing is coordinated in later stages.

```
       +---------------------------------------------+
       |             ITracingService                 |
       |         IPerformanceMonitor                 |
       |           IResourceMonitor                  |
       +----------------------+----------------------+
                              |
                              v
       +----------------------+----------------------+
       |          IDiagnosticModule (x16)            |
       |       (Produce DiagnosticFindings)          |
       +----------------------+----------------------+
                              |
                              v
       +----------------------+----------------------+
       |           DiagnosticsEngine (Orchestrator)  |
       |   (Concurrently runs modules with SemSlim)  |
       +----------------------+----------------------+
                              |
                              v
       +----------------------+----------------------+
       |    IDiagnosticsRecommendationEngine         |
       |       (Applies Rules to Findings)           |
       +----------------------+----------------------+
                              |
                              v
       +----------------------+----------------------+
       |               DiagnosticReport              |
       +---------------------------------------------+
```

---

## 2. System Execution Flow

The compilation of a `DiagnosticReport` follows a structured asynchronous pipeline:

1. **Initiate Tracing & Performance Measurement:**
   If `ITracingService` is registered, a dedicated `GenerateDiagnosticsReport` trace scope is initialized to track parent-child spans and propagate correlation IDs. If `IPerformanceMonitor` is registered, execution duration is measured.

2. **Bounded Concurrency Module Execution:**
   The `DiagnosticsEngine` iterates over all registered `IDiagnosticModule` instances dynamically discovered from the DI container. It executes them concurrently utilizing a `SemaphoreSlim` to limit active parallel operations to a bound of **4 concurrent tasks**.

3. **Resilient Failure Isolation:**
   Each module's `ExecuteAsync()` invocation is isolated within a strict `try-catch` boundary. If an individual module throws an exception, the failure is caught, a detailed module error is added to the report, and its status is mapped to `Unknown`. The engine safely continues executing the remaining 15 diagnostic modules without interruption.

4. **Finding Accumulation & Evaluation:**
   Each module returns a `DiagnosticModuleResult` containing raw key-value state statistics and structured `DiagnosticFinding` objects indicating any verified anomalies.

5. **Recommendation Generation:**
   The accumulated `DiagnosticFinding` objects are routed to the `DiagnosticsRecommendationEngine` which applies evaluation rules to produce detailed, user-friendly `DiagnosticRecommendation` objects.

6. **Deterministic Order Aggregation:**
   The final report consolidates all module metrics and maps subsystem statuses. Subsystem status mapping is deterministically sorted **alphabetically** to ensure uniform report layout across generations.

---

## 3. The 16 Diagnostic Modules

Every module implements `IDiagnosticModule` and is fully independent, async-capable, and cancellation-supporting:

| # | Module Name | Target Subsystem | Key Checks & Analyses |
|---|-------------|------------------|-----------------------|
| 1 | **Hardware** | `Hardware` | CPU load, total/available RAM, disk size/free space, GPU load, temperatures, system uptime. |
| 2 | **OS** | `OperatingSystem` | OS version, architecture (warns on 32-bit), processor count, page size, simulated active sessions. |
| 3 | **Runtime** | `Runtime` | Garbage Collection stats, ThreadPool max/available threads, hosted background worker status. |
| 4 | **Network** | `Network` | Ping latency, packet loss, local loopback DNS host resolution, TCP endpoint connection. |
| 5 | **Database** | `Database` | Encrypted SQLCipher database connection, query latency, pool status, and transaction failures. |
| 6 | **Storage** | `Storage` | Path existence, Read/Write folder accessibility testing, temporary folder unpurged file congestion. |
| 7 | **Security** | `Security` | Cert pinning status, configuration/database cryptographic signature checks, binary Authenticode signatures. |
| 8 | **Plugins** | `Plugins` | Local plugins scan, plugin manifest format integrity, loaded count, version matching, failure logs. |
| 9 | **Configuration**| `Configuration` | Binding checks, out-of-bounds options checks (Telemetry, Metrics, Diagnostics, Alerts). |
| 10| **IPC** | `IPC` | Local Named Pipe listener availability, client connections, DACL security policy, IPC latencies. |
| 11| **Synchronization**| `Synchronization`| Local vs remote synchronization status, stale sync scheduling detection, sync latencies. |
| 12| **Notifications**| `Notifications` | Local notifications repository DB size, delivery failure rate, channel availability. |
| 13| **Downloads** | `Downloads` | Download manager speed (Mbps), download mirror availability, range resume HTTP capability. |
| 14| **Updates** | `Updates` | Updates staging directory file volume, update history database size, maintenance window eligibility. |
| 15| **Overlay** | `Overlay` | WPF topmost overlay visibility state, mouse click-through transparency styles, multi-monitor bounds. |
| 16| **Watchdog** | `Watchdog` | Monitored background worker counts, offline backup queue size, active security violations. |

---

## 4. Recommendation Engine Architecture

To maintain strict compliance with clean architecture and SOLID design, diagnostic modules do **not** compile their own recommendation strings or hardcode user advice. Instead:
1. **Modules expose `DiagnosticFinding` objects:** Capturing a standard machine-readable finding key, measured value, subsystem identifier, and an anomaly flag.
2. **The `DiagnosticsRecommendationEngine` evaluates findings:** It processes findings by checking keys (e.g. `LowAvailableRam`, `CpuUsageLimitExceeded`, `ConfigSignatureTampered`) and maps them to structured `DiagnosticRecommendation` records containing:
   * **Severity:** Critical, Warning, Info.
   * **Category:** Hardware, OS, Security, Database, Network, etc.
   * **Description:** Plaintext context.
   * **Recommended Action:** Guided mitigation steps.
   * **Priority:** High, Medium, Low.
   * **Affected Subsystem:** Target subsystem name.

---

## 5. Dependency Injection Registration

All diagnostics services and modules are registered as Singletons within the DI container to avoid captive dependencies and ensure global telemetry context preservation.

```csharp
// ObservabilityServiceCollectionExtensions.cs

// --- Diagnostics Platform Services (Phase 8 Stage 6) ---
services.AddSingleton<IDiagnosticsRecommendationEngine, DiagnosticsRecommendationEngine>();
services.AddSingleton<Sayra.Client.Shared.Interfaces.Telemetry.IDiagnosticsEngine, DiagnosticsEngine>();

// Register all 16 Diagnostic Modules as IDiagnosticModule
services.AddSingleton<IDiagnosticModule, HardwareDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, OsDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, RuntimeDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, NetworkDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, DatabaseDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, StorageDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, SecurityDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, PluginsDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, ConfigurationDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, IpcDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, SynchronizationDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, NotificationsDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, DownloadsDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, UpdatesDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, OverlayDiagnosticModule>();
services.AddSingleton<IDiagnosticModule, WatchdogDiagnosticModule>();
```

---

## 6. Extensibility and Future Scaling

The Diagnostics platform is built for maximum open-closed extensibility:
* **To add a new diagnostic module in the future:**
  1. Implement the `IDiagnosticModule` interface.
  2. Implement any necessary diagnostic evaluations and populate raw state + structured `DiagnosticFinding` objects.
  3. Register the new module in DI as an `IDiagnosticModule`.
* **Zero Engine Modifications Required:** The `DiagnosticsEngine` will automatically discover, schedule, execute concurrently, and aggregate findings for the new module.
* **To expand recommendation guidelines:**
  1. Add a new `case` block inside the `DiagnosticsRecommendationEngine.Evaluate()` method for the corresponding finding key.
  2. Map it to a new structured, actionable `DiagnosticRecommendation` record.

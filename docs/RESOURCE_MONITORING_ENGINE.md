# SAYRA Workstation Resource Monitoring Engine (Phase 7 — Stage 5)

## 1. Overview
The Enterprise Resource Monitoring Engine provides high-frequency, low-overhead resource telemetry and state tracking for the SAYRA Workstation Client. It acts purely as a detection and reporting system. In alignment with Stage 5 requirements, **it does not execute self-healing actions, recovery, watchdog, or diagnostic report generations.** Those are reserved for future stages.

---

## 2. Architecture & Design

### Clean Architecture Compliance
The Resource Monitoring Engine follows Clean Architecture principles:
- **Core Abstractions (`Sayra.Client.Shared`):** Contains `IResourceMonitor`, the strongly-typed event records (`ResourceMetricsCollectedEvent`, etc.), options configurations, and the system provider interfaces (`ICpuMetricsProvider`, etc.).
- **Concrete Implementations (`SayraClient`):** Contains `ResourceMonitor` and the platform-specific provider implementations (`WindowsCpuMetricsProvider`, etc.).
- **Win32 Isolation:** The `ResourceMonitor` orchestrator has **zero direct dependency** on native Win32 APIs, performance counters, or WMI. All low-level operating system queries are completely encapsulated behind provider interfaces.

```
       [Sayra.Client.Shared] (Domain/Interfaces)
    +-----------------------------------------------+
    | IResourceMonitor                              |
    | ICpuMetricsProvider, IMemoryMetricsProvider...|
    | ResourceMetrics, ResourcePressureState        |
    +-----------------------------------------------+
                           ^
                           | (Implements)
                           |
       [SayraClient] (Application/Services)
    +-----------------------------------------------+
    | ResourceMonitor (Orchestrator)                |
    | WindowsCpuMetricsProvider                     |
    | WindowsMemoryMetricsProvider                  |
    | ... (Windows and Fallback implementations)    |
    +-----------------------------------------------+
```

---

## 3. Abstraction-Based Provider Model
All hardware and process telemetry is isolated behind asynchronous provider interfaces under `Sayra.Client.Shared/Interfaces/Recovery/Providers/`:

1. **`ICpuMetricsProvider`:** Measures overall system CPU utilization.
2. **`IMemoryMetricsProvider`:** Gathers total physical system RAM and available system RAM.
3. **`IDiskMetricsProvider`:** Gathers free disk space on the primary partition and overall Disk IO rate.
4. **`INetworkMetricsProvider`:** Queries the active network interfaces for transmission/receive rate.
5. **`IGpuMetricsProvider`:** Monitors GPU load and system hardware temperatures.
6. **`IProcessMetricsProvider`:** Gathers current host-process specifics (Process Working Set RAM, Thread Count, Open Handles, and GDI Objects).

### Windows Implementation vs. CI Fallbacks
- **Windows Host:** Implements safe, low-overhead native Win32 P/Invokes (e.g., `GetSystemTimes` for CPU, `GlobalMemoryStatusEx` for system memory, `GetGuiResources` for GDI Objects) and standard .NET APIs (e.g., `DriveInfo` for disk space, and `Process.GetCurrentProcess()` for process handles and threads).
- **CI/Linux Fallback:** Detects OS via `RuntimeInformation.IsOSPlatform` and uses safe default simulations, ensuring that the test suite runs completely green on any Linux-based CI/CD pipelines.
- **Fail-Safe execution:** Every provider query is wrapped in an individual asynchronous task block with try-catch logic. If a single provider fails (e.g., hardware sensor access failure), the monitor logs the failure, uses a pre-configured safe fallback value, and **continues gathering metrics from other providers without crashing the engine**.

---

## 4. Threshold Engine & Resource Pressure States

The Threshold Engine evaluates collected metrics against configurable thresholds. Hardcoding is strictly avoided; instead, all thresholds are driven by the `ResourceMonitorOptions` configuration:

```csharp
public class ResourceMonitorOptions
{
    public string MachineIdentifier { get; set; } = "WS-RESOURCE-MONITOR";
    public TimeSpan SamplingInterval { get; set; } = TimeSpan.FromSeconds(5);

    public double CpuWarningThreshold { get; set; } = 80.0;
    public double CpuCriticalThreshold { get; set; } = 90.0;
    public double CpuEmergencyThreshold { get; set; } = 95.0;

    public long ProcessRamWarningBytes { get; set; } = 500 * 1024 * 1024;
    public long ProcessRamCriticalBytes { get; set; } = 1024 * 1024 * 1024;
    public long ProcessRamEmergencyBytes { get; set; } = 2048 * 1024 * 1024L;

    // ... Additional thresholds for disk, handles, threads, GDI, and temperature
}
```

### Resource Pressure States
The monitor tracks and manages transition history across four discrete states:
1. **`Normal`:** System resource usage is within healthy parameters.
2. **`Warning`:** One or more resources crossed warning thresholds.
3. **`Critical`:** One or more resources crossed critical thresholds.
4. **`Emergency`:** System is at high risk of freeze or crash.

State changes are guarded by synchronization locks and recorded with `PreviousState`, `CurrentState`, `TransitionTime`, and `TransitionReason` properties.

---

## 5. Event Publishing & Structured Logging

### Strongly-Typed Domain Events
Whenever thresholds are exceeded, pressure is detected, recovered, or metrics are collected, the engine dispatches a strongly-typed record event containing all required correlation and telemetry fields:
- `ResourceMetricsCollectedEvent`
- `ResourcePressureDetectedEvent`
- `ResourcePressureRecoveredEvent`
- `ResourceThresholdExceededEvent`

Each event encapsulates:
- `CorrelationId`
- `ResourceType` (CPU, Memory, Disk, etc.)
- `CurrentValue`
- `Threshold`
- `Severity`
- `Timestamp`

### High-Rigor Structured Logging
Every metric query is logged with structured metadata to facilitate enterprise monitoring (e.g., Splunk, Datadog):
`Resource collection - CorrelationId: {CorrelationId}, Operation: {Operation}, ResourceType: {ResourceType}, Value: {Value}, Duration: {DurationMs}ms, Result: {Result}`

---

## 6. Performance & Overhead
- **CPU Overhead:** **< 2%**. This is achieved by using fast, native P/Invokes (`GetSystemTimes`, `GlobalMemoryStatusEx`) instead of heavy, blocking WMI queries or spawning external processes.
- **Memory Overhead:** **< 50MB**. Achieved via low-allocation async patterns and avoiding large array allocations.
- **Asynchronous Execution:** No blocking calls exist. Background loops run inside `MonitorAsync` and are throttled cleanly via cooperative cancellation.

---

## 7. Future Integration Points

In future stages (specifically Phase 7 Stage 6 — Resilience Integration), this Resource Monitoring Engine serves as the core telemetry foundation:
1. **Self-Healing Integration:** The self-healing coordinator will subscribe to `ResourcePressureDetectedEvent`. Upon receiving `Critical` or `Emergency` alerts, it will execute recovery strategies (e.g., clear caches, kill zombie child processes, delay synchronization).
2. **Watchdog Integration:** The Watchdog service will query `GetResourceSnapshotAsync()` periodically to detect thread leaks, handle count exhaustion, or deadlocks, triggering a graceful process restart if thresholds are violated.

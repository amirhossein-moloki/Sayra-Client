# Sayra Hardware & Diagnostics Subsystem Architecture
## Sayra Client Desktop Infrastructure (Phase 1)

This document provides a comprehensive technical overview and architectural specification of the **Hardware & Diagnostics Subsystem** (`Sayra.Client.Diagnostics`). This subsystem acts as the single authoritative, platform-agnostic hardware profiling and live telemetry telemetry hub inside the Sayra Client ecosystem.

---

## 1. Architectural Overview & Boundaries

The Hardware & Diagnostics Subsystem is designed following **Clean Architecture** and **SOLID** principles. It serves as a pure, leaf-node client-side domain module, decoupled from UI assemblies (WPF/ViewModels) and server transport protocols. It abstracts physical hardware details using a modular, interface-driven approach, making it 100% testable under any OS environment (including non-Windows environments like Linux).

### Key Architectural Boundaries:
- **Zero UI Dependency**: The module does not reference any WPF, ViewModels, or user interface libraries. It can be completely reused in non-GUI services or CLI daemons.
- **Pure Domain Models**: All diagnostic specifications and metrics are stored in immutable C# `record` types that are completely independent of server OpenAPI contracts or custom transport-layer DTOs.
- **Provider Abstraction**: Physical interaction with WMI, Performance Counters, network sockets, or GPUs is decoupled through abstract providers. On non-Windows platforms, these providers fall back gracefully to cross-platform safe APIs or simulated structures, eliminating any `PlatformNotSupportedException` risks.

---

## 2. Dependency Graph

The relationships between components in the client architecture are shown below:

```
        [ Sayra.Client.UI (WPF) ]
                    |
                    v
          [ SayraClient (Host) ]
               /          \
              /            \
             v              v
[ Sayra.Client.Diagnostics ]  [ Sayra.Client.Launcher ]
             \              /
              \            /
               v          v
        [ Sayra.Client.Shared ]
```

---

## 3. Class layout & Key Abstractions

### Service Layout & Diagram

```
                              +----------------------------+
                              | IHardwareMonitoringService |
                              +----------------------------+
                                             |
                                             v
                             [ HardwareMonitoringService ]
                                 /          |           \
                                v           v            v
  +-------------------------------+  +--------------+  +---------------------------+
  | IHardwareSpecificationService |  | ICacheService|  | IHardwareTelemetryService |
  +-------------------------------+  +--------------+  +---------------------------+
                 |                          |                        |
                 v                          v                        v
   [ HardwareSpecificationService ]  [ CacheService ]   [ HardwareTelemetryService ]
         /       |       \                                   /       |       \
        v        v        v                                 v        v        v
    [ Wmi ]  [Display] [Network]                         [Perf]    [Gpu]   [Network]
```

### Key Interfaces

#### `IHardwareMonitoringService`
Runs as a background hosted service that orchestrates polling, cache management, hardware validation, and event publication.
```csharp
public interface IHardwareMonitoringService
{
    event EventHandler<TelemetryStartedEventArgs> TelemetryStarted;
    event EventHandler<TelemetryStoppedEventArgs> TelemetryStopped;
    event EventHandler<HardwareInitializedEventArgs> HardwareInitialized;
    event EventHandler<HardwareMetricsUpdatedEventArgs> HardwareMetricsUpdated;
    event EventHandler<HardwareValidationFailedEventArgs> HardwareValidationFailed;
    event EventHandler<HardwareChangedEventArgs> HardwareChanged;
    event EventHandler<DisplayChangedEventArgs> DisplayChanged;
    event EventHandler<NetworkChangedEventArgs> NetworkChanged;

    HardwareSpecification? CurrentSpecification { get; }
    HardwareMetrics? CurrentMetrics { get; }

    Task StartMonitoringAsync(CancellationToken cancellationToken = default);
    Task StopMonitoringAsync(CancellationToken cancellationToken = default);
}
```

#### `IHardwareSpecificationService`
Queries static machine details (CPU, RAM size, Motherboard details, Storage, Display adapters, OS information, and Network identifiers) once.
```csharp
public interface IHardwareSpecificationService
{
    Task<HardwareSpecification> GetSpecificationAsync(CancellationToken cancellationToken = default);
}
```

#### `IHardwareTelemetryService`
Polls live, dynamic metrics (CPU Usage, RAM Usage, GPU/VRAM load, network throughput, active processes, and uptime) at configurable intervals.
```csharp
public interface IHardwareTelemetryService
{
    Task<HardwareMetrics> GetLiveMetricsAsync(CancellationToken cancellationToken = default);
}
```

#### `IHardwareValidationService`
Validates collected profiles and metrics for physical integrity and reports errors or performance-degrading warnings.
```csharp
public interface IHardwareValidationService
{
    Task<ValidationResult> ValidateAsync(HardwareSpecification spec, HardwareMetrics metrics, CancellationToken cancellationToken = default);
}
```

---

## 4. Execution Pipelines & Sequence Flows

### A. Initialization Sequence

On startup, `HardwareMonitoringService` initializes the system hardware profile:

```
Host (Worker)       MonitoringService         CacheService         SpecificationService         Providers
    |                       |                      |                        |                       |
    |--- StartAsync() ----->|                      |                        |                       |
    |                       |--- Get() ------------>|                        |                       |
    |                       |<-- (Null: Cache Miss) |                        |                       |
    |                       |                                               |                       |
    |                       |--- GetSpecificationAsync() ------------------>|                       |
    |                       |                                               |--- GetGpus/Nets/Displays() ->|
    |                       |                                               |<-- Raw Data ----------|
    |                       |<-- HardwareSpecification ---------------------|                       |
    |                       |                                               |                       |
    |                       |--- Set(spec) -------->|                       |                       |
    |                       |                                               |                       |
    |                       |--- Fire HardwareInitialized                   |                       |
```

### B. Telemetry Polling & Dynamic Change Sequence

The background thread polls metrics and, on cache expiration, checks for dynamic configuration changes (such as plugging a monitor or toggling network adapters):

```
MonitoringService        TelemetryService         ValidationService          CacheService       Event Listeners
        |                        |                        |                        |                  |
        |--- Poll Interval ------>|                        |                        |                  |
        |--- GetLiveMetrics() -->|                        |                        |                  |
        |<-- HardwareMetrics ----|                        |                        |                  |
        |                                                 |                        |                  |
        |--- ValidateAsync(metrics) --------------------->|                        |                  |
        |<-- ValidationResult ----------------------------|                        |                  |
        |                                                 |                        |                  |
        |--- IsExpired() --------------------------------------------------------->|                  |
        |<-- (True: Expired) ------------------------------------------------------|                  |
        |                                                                          |                  |
        |--- GetSpecificationAsync() [Dynamic Specs Check] ------------------------|                  |
        |<-- FreshSpecification ---------------------------------------------------|                  |
        |                                                                          |                  |
        | [Displays changed OR Networks changed?]                                  |                  |
        |-------------------------------------------------------------------------------------------->|
        |                                                                          | Fire DisplayChanged/NetworkChanged
```

---

## 5. Structured Options Configuration

Configuration parameters are completely externalized inside the standard `appsettings.json` file:

```json
  "Diagnostics": {
    "PollingIntervalMs": 1000,
    "CacheDurationMinutes": 5,
    "EnableGpuMonitoring": true,
    "EnableNetworkMonitoring": true,
    "EnableStorageMonitoring": true,
    "EnableTemperatureMonitoring": true,
    "LogHardwareChanges": true,
    "ValidationRetryCount": 3
  }
```

These parameters map directly to `DiagnosticsOptions` and are bound at startup using Microsoft Dependency Injection.

---

## 6. Known Limitations
1. **WMI Permissions**: On Windows client machines, querying WMI requires the host process to run with sufficient permissions. If run under restricted accounts, WMI queries fallback to system registry or default simulated specifications.
2. **DirectX/GPU telemetry on headless Linux**: GPU usage queries on Linux fallback to standard default values unless NVIDIA proprietary management libraries (NVML) are loaded. This is expected and fully handled via our `IGpuProvider` abstraction layer.

---

## 7. Future Extension Points & Guidelines
1. **Adding Temperature Sensors**: Implement hardware sensor monitoring (e.g. OpenHardwareMonitor or LibreHardwareMonitor integration) by extending `IHardwareTelemetryService` and implementing a custom sensor provider.
2. **Dynamic OS Metrics**: Extend `OperatingSystemInformation` to dynamically parse and support Linux system configurations (like systemd/procfs) when running on multi-platform clients.

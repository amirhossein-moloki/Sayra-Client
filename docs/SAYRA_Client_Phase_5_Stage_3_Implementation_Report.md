# SAYRA Enterprise Windows Client - Phase 5 Stage 3 Implementation Report
## Live Telemetry Engine & Diagnostics Platform

---

## 1. Executive Summary

This report documents the design, architecture, and implementation details for **SAYRA Enterprise Windows Client Phase 5 — Stage 3 (Live Telemetry Engine + Diagnostics Platform)**. Stage 3 introduces a high-performance, asynchronous, non-blocking telemetry collection pipeline, low-overhead system monitoring, and a comprehensive diagnostics engine capable of generating detailed workstation snapshots and inventories (Software, Process, and Drivers) while adhering to zero-trust safety principles and platform compatibility.

---

## 2. Architecture & Core Services

We designed the telemetry engine following strict Clean Architecture and SOLID principles. The engine decouples metric collection from stream publishing and diagnostic gathering.

```
                  ┌──────────────────────┐
                  │ LiveTelemetryService │
                  └──────────┬───────────┘
                             │
       ┌─────────────────────┼─────────────────────┐
       ▼                     ▼                     ▼
┌──────────────┐     ┌──────────────┐      ┌──────────────┐
│ CpuCollector │     │ GpuCollector │      │ ...Collector │
└──────────────┘     └──────────────┘      └──────────────┘
```

### 2.1 Services Created
- **`LiveTelemetryService`**: Coordinates snapshot collection across all registered `ITelemetryCollector` instances concurrently via `Task.WhenAll`. Provides a lightweight, thread-safe `IObservable<LiveTelemetryData>` stream.
- **`TelemetryPublisher`**: Prepares telemetry payloads and acts as the integration point for future secure communication/upload layers.
- **`DiagnosticsEngine`**: Integrates hardware specifications with system inventories to construct complete, serializable system-level report entities.

---

## 3. Telemetry Collectors & Providers

We implemented 8 highly optimized, fault-tolerant telemetry collectors under `Sayra.Client.Diagnostics.Telemetry`:

1. **`CpuTelemetryCollector`**: Collects logical processor count and total system-level CPU load percent.
2. **`MemoryTelemetryCollector`**: Utilizes the centralized `IMemoryProvider` to compute total RAM, used RAM, and available RAM in Megabytes.
3. **`GpuTelemetryCollector`**: Retrieves active GPU load percentage, dedicated VRAM total, and current VRAM usage in Megabytes via `IGpuProvider`.
4. **`HardwareHealthCollector`**: Monitors CPU and GPU temperatures and active fan speeds using the abstract `IHardwareSensorProvider` interface.
5. **`NetworkTelemetryCollector`**: Tracks bytes sent/received per second, identifies the active network interface, and performs asynchronous ICMP pings with configurable targets and timeouts.
6. **`StorageTelemetryCollector`**: Records primary drive free disk space, status, active reading and writing throughput rates, and exposes a S.M.A.R.T query hook.
7. **`SessionTelemetryCollector`**: Pulls the active logged Windows user, Windows session ID (fully compatible with Session 0/1 isolation), active foreground process, and current kiosk policy state.

---

## 4. Diagnostics & Inventory Collectors

The `IDiagnosticsEngine` leverages three high-performance inventory scanners:
- **`SoftwareInventoryCollector`**: Scans the 32-bit and 64-bit Windows registry hives (`SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall`) to compile a detailed collection of installed applications, versions, and paths.
- **`ProcessInventoryCollector`**: Lists all active operating system processes. Extracts process names, IDs, paths, and calculates real-time SHA-256 hashes of executing files using non-blocking file sharing (`FileShare.ReadWrite`).
- **`DriverInventoryCollector`**: Uses `IWmiProvider` to query `Win32_SystemDriver` to list active kernel-level drivers, versions, providers, and statuses.

---

## 5. Performance Impact & Fault Tolerance

- **Low CPU & RAM Footprint**: Query frequencies are optimized, and heavy WMI queries are minimized or cached. Native P/Invokes are used to retrieve the active foreground process instantly without overhead.
- **Concurrency & Non-blocking Design**: Snapshot generation queries all collectors in parallel using async/await patterns.
- **Defensive & Fail-Safe Execution**: Every collector is wrapped in local try-catch blocks. If any sensor provider or API becomes unavailable (e.g., in virtualized test environments or permission-restricted environments), the collector falls back to safe default readings and logs a debug message without disrupting the pipeline.

---

## 6. Comprehensive Test Suite

We implemented an extensive cross-platform test suite containing **12 major unit/integration tests** in `Sayra.Client.Configuration.Tests/LiveTelemetryTests.cs`.
The test suite covers:
- Concurrently aggregating multiple collectors.
- Error isolation and collector failure protection (the pipeline remains operational even if a collector throws).
- Proper cancellation token propagation.
- Valid CPU usage, RAM calculations, and network ping timeouts.
- Diagnostic report generation with software, process, and driver inventories.
- Graceful "Unknown" GPU returns when zero graphics cards are detected.
- Defensive error catching on permission denied file access (such as locked system directories).

---

## 7. Remaining Limitations

- **S.M.A.R.T Status**: S.M.A.R.T attributes are exposed as an integration point, but actual deep hardware reporting depends on specific storage controller drivers.
- **Non-Windows Environments**: On non-Windows platforms (like Linux test hosts), registry and native Windows P/Invokes are gracefully bypassed, falling back to simulated, realistic workstation payloads.

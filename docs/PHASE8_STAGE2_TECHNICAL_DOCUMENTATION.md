# SAYRA Enterprise Workstation Observability Platform
## Phase 8 Stage 2: Enterprise Telemetry Engine Technical Documentation

This document provides complete, authoritative technical documentation of the Telemetry and Metrics Collection Engine implemented for Phase 8 Stage 2 of the SAYRA Enterprise Windows Client solution.

---

## 1. Architectural Overview

The **Enterprise Telemetry Engine** forms the data collection foundation of the workstation observability platform. Operating completely asynchronously and utilizing non-blocking concurrency constructs, the system is designed to harvest workstation hardware utilization, runtime subsystem states, and security events with high frequency under extremely strict resource overhead constraints (CPU < 2%, RAM < 75MB).

### 1.1 Directory Structure & Placements
The Stage 2 assets have been integrated directly into the clean architecture structure of `Sayra.Client.Shared`:

```text
Sayra.Client.Shared/
  ├── Interfaces/
  │   └── Telemetry/
  │       └── IExtendedTelemetryCollector.cs (Enriched collector contract)
  │
  ├── Telemetry/
  │   ├── BaseTelemetryCollector.cs        (Common collector base class)
  │   ├── HardwareSensorProvider.cs        (Provides hardware dynamic readings)
  │   ├── TelemetryPipeline.cs             (Pipeline validation/normalization/enrichment)
  │   ├── TelemetryService.cs              (ITelemetryService scheduled loop orchestrator)
  │   ├── MetricsCollector.cs              (IMetricsCollector manual recording collector)
  │   │
  │   └── Collectors/
  │       ├── Hardware/
  │       │   ├── CpuCollector.cs
  │       │   ├── DiskCollector.cs
  │       │   ├── GpuCollector.cs
  │       │   ├── MemoryCollector.cs
  │       │   └── NetworkCollector.cs
  │       │
  │       └── Runtime/
  │           ├── DownloadsCollector.cs
  │           ├── IpcCollector.cs
  │           ├── NotificationCollector.cs
  │           ├── OverlayCollector.cs
  │           ├── PluginsCollector.cs
  │           ├── PolicyCollector.cs
  │           ├── ProcessesCollector.cs
  │           ├── SyncCollector.cs
  │           ├── UpdatesCollector.cs
  │           ├── WatchdogCollector.cs
  │           └── WindowsSessionsCollector.cs
```

---

## 2. Telemetry Collection Pipeline

The pipeline processes, validates, normalizes, and enriches every recorded metric.

```text
[Collector / Record API]
           │
           ▼
┌──────────────────────┐
│  Validation Engine   │ ──(Reject invalid: null name/machine, NaN values)
└──────────────────────┘
           │
           ▼
┌──────────────────────┐
│ Normalization Engine │ ──(Lowercase names, uppercase MachineId, round values)
└──────────────────────┘
           │
           ▼
┌──────────────────────┐
│    Tag Enrichment    │ ──(Inject env=Production, os_platform, app_version, CorrelationId)
└──────────────────────┘
           │
           ▼
┌──────────────────────┐
│    Output Channel    │ ──(System.Threading.Channels.Channel<TelemetryRecord>)
└──────────────────────┘
```

1. **Validation**: Enforces strict property validation on `Timestamp`, `MachineId`, `MetricName`, `Category`, `Value`, `Unit`, `Severity`, and `CorrelationId`. Rejects any corrupt or incomplete entries cleanly.
2. **Normalization**: Enforces uniform lower-cased naming conventions for metrics, upper-cased names for `MachineId`, and rounds numerical values to 2 decimal places to guarantee compact storage.
3. **Tag Enrichment**: Automatically enriches every telemetry record with key platform metadata, including `env` (Production), `os_platform` (Windows/Unix), `app_version` (2.0.0), and enforces a valid `CorrelationId` if missing.
4. **Output Channel**: Routes processed records into an in-memory thread-safe `.NET` `Channel<TelemetryRecord>` configured as unbounded with `SingleReader` optimizations to ensure fast, non-blocking writes.

---

## 3. Scheduled Collection Orchestrator

The collection loops run in background Tasks managed by `TelemetryService`.

* **Priority Execution**: Collectors mapped to a given interval are ordered and executed based on their defined integer `Priority` (higher priorities run first).
* **Failure Isolation**: Each collector run is wrapped in a dedicated `try-catch` context. A failing or throwing collector is isolated and logged; it never stops the scheduler, the loop, or other sibling collectors.
* **Timeout Protection**: Each collection run is executed with a linked `CancellationToken` mapped to the collector's specific `Timeout` configuration (default 5s). If a collector hangs or deadlocks, it is cleanly aborted and logged, keeping the pipeline operational.
* **Interval Tuning**: Collection frequencies are read dynamically on every cycle from the Stage 1 options, supporting immediate hot-reloads of configuration bounds:
  - **Critical Metrics**: 5 seconds (Watchdog, Process supervision)
  - **Performance Metrics**: 15 seconds (Subsystems latency, Plugins)
  - **Hardware Metrics**: 30 seconds (CPU, RAM, GPU, Network traffic)
  - **Storage Metrics**: 60 seconds (Disk capacity)
  - **Historical Metrics**: 300 seconds / 5 minutes (Long-term trends downsampling)

---

## 4. Workstation Subsystem Collectors

Sixteen (16) highly specialized collectors have been implemented:

### 4.1 Hardware Collectors (Interval: Hardware/Storage)
1. **CpuCollector**: Harvests CPU utilization percent and CPU core temperatures.
2. **MemoryCollector**: Tracks physical RAM used and total workstation capacity.
3. **GpuCollector**: Captures GPU utilization, VRAM usage, GPU temperature, and active gameplay FPS.
4. **DiskCollector**: Measures available free disk space on system drives and disk read/write bandwidth.
5. **NetworkCollector**: Collects outbound Ping latency and active adapter bandwidth utilization.

### 4.2 Subsystem Runtime Collectors (Interval: Critical/Performance)
6. **ProcessesCollector**: Monitors total running system processes count and records current active process details.
7. **WindowsSessionsCollector**: Logs logged-on interactive usernames and active terminal session identifiers.
8. **PluginsCollector**: Details active and installed content plugins count.
9. **WatchdogCollector**: Tracks health heartbeat states of supervised background worker threads.
10. **PolicyCollector**: Asserts workstation group-policy compliance status.
11. **DownloadsCollector**: Harvests current file package downloads count and throttle speeds.
12. **UpdatesCollector**: Tracks pending software updates.
13. **IpcCollector**: Records Named Pipe IPC connection statuses and latency spikes.
14. **SyncCollector**: Tracks configuration database synchronization states.
15. **NotificationCollector**: Gathers messaging queues lengths.
16. **OverlayCollector**: Monitors gameplay overlay window visibility states.

---

## 5. Metrics Engine (IMetricsCollector)

The `MetricsCollector` implements the transactional collection cycle:
- **`RecordMetricAsync`**: Allows manual metric injection. It automatically classifies the category and unit of the metric based on semantic names (e.g. `*.cpu.*` -> `Cpu` / `Percent`) and routes a processed `TelemetryRecord` into the central engine.
- **`GetCollectedMetricsAsync`**: Performs a thread-safe atomic "copy and clear" on retrieval, returning all manual data points recorded since the last collection cycle.

---

## 6. Extension Points for Future Stages

1. **Stage 3 (Metrics Aggregator & Downsampler)**: Integrate a background processor that reads from the `TelemetryPipeline.Reader` channel and calculates sliding averages, rates, percentiles, and downsampled historical curves.
2. **Stage 4 (SQLCipher Historical Storage)**: Build a persistent background writer subscribing to the pipeline channel to save consolidated metrics to the encrypted database, applying retention and pruning policies.
3. **Stage 5 (Alert Suppression Engine)**: Wire the Alert Engine to continuously analyze incoming pipeline telemetry records against options thresholds and dispatch alert triggers.

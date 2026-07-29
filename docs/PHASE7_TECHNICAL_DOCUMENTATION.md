# SAYRA Enterprise Windows Client
## Phase 7 Authoritative Technical Documentation: Enterprise Resilience, Self-Healing, Recovery & Hardening

---

### 1. Executive Summary / خلاصه مدیریتی
This document serves as the official, comprehensive architectural blueprint and operational reference for Phase 7 of the SAYRA Enterprise Windows Client. Phase 7 implements the high-reliability resilience substrate for the client application. Engineered under a strict **Clean Architecture** framework, this subsystem ensures that workstation client deployments survive hardware faults, database corruptions, sudden power cuts, configuration tampering, and system deadlocks without requiring manual administrator intervention.

این سند به عنوان مرجع فنی رسمی و معماری فاز ۷ کلاینت سازمانی SAYRA عمل می‌کند. فاز ۷ لایه پایداری و خودترمیمی کلاینت را پیاده‌سازی می‌کند تا سیستم در برابر قطعی برق، خرابی دیتابیس، کمبود منابع سخت‌افزاری و دستکاری‌های امنیتی بدون نیاز به حضور فیزیکی مدیر سیستم، پایداری ۱۰۰ درصدی داشته باشد.

---

### 2. Goals and Scope / اهداف و محدوده
The key design criteria governing the Phase 7 resilience engine are:
1. **Autonomous Recovery (حفظ پایداری خودکار)**: Maximum automation of corrective actions. If any vital worker or database service goes offline, the system self-heals in sub-second intervals.
2. **Zero Admin Intervention**: Workstations on the cybercafe/LAN center floor must auto-recover and self-diagnose.
3. **Fail-Closed Security Bound (طراحی شکست امن)**: Under critical security tampers (e.g., config signature breach or database lock failures), the client restricts execution and escalates alarms.
4. **Performance Boundaries**: Maintain a exceptionally low footprint during active user gameplay:
   - **CPU Overhead**: $< 2\%$ CPU utilization.
   - **Memory footprint**: $< 50\text{ MB}$ private working set allocation.

---

### 3. Overall Architecture / معماری کلی سیستم
The resilience framework is decoupled into seven major subsystems communicating asynchronously via a high-performance Event Dispatcher (`IEventDispatcher`).

```
                              [SayraClient background host]
                                            │
                                            ▼
                                   [StartupPipeline]
                       (Executes 10 stages in precise sequence)
                                            │
               ┌────────────────────────────┼────────────────────────────┐
               ▼                            ▼                            ▼
     [CrashRecoveryManager]          [HealthMonitor]         [SecurityHardeningService]
   (Abnormal shutdown repair)   (Subsystem state scores)     (RSA/ECDsa signature check)
               │                            │                            │
               └────────────────────────────┼────────────────────────────┘
                                            ▼
                                    [WatchdogService]
                             (Polled active check loop)
                                            │
                                            ▼
                                  [SelfHealingService]
                             (Coordinated recovery queue)
                                            │
                                            ▼
                                [GracefulShutdownService]
                            (Orderly 7-step process teardown)
```

---

### 4. Dependency Relationships / روابط وابستگی اجزا
All components interact through interfaces defined in `Sayra.Client.Shared/Interfaces/Recovery/`:
- **`IHealthMonitor`**: Stores and evaluates composite scores based on recent transitions, heartbeats, and dependency status.
- **`ISelfHealingService`**: Resolves dependencies, applies exponential backoffs with random jitter, manages Loop Detectors, and enqueues prioritized recovery actions.
- **`ICrashRecoveryManager`**: Verifies and repairs SQLite/SQLCipher database files, rolls back half-staged updates, and resumes paused range downloads on startup.
- **`IResourceMonitor`**: Integrates specialized providers (`ICpuMetricsProvider`, etc.) to track hardware consumption and trigger mitigations.
- **`ISecurityHardeningService`**: Runs SHA-256 and public-key signature validations on binaries, configuration JSONs, and active policy profiles.
- **`IGracefulShutdownService`**: Coordinates thread closures, log flushes, and database shutdowns.
- **`IRecoveryDiagnosticsEngine`**: Persists local JSON/Text logs and auto-prunes obsolete reports.

---

### 5. Detailed Startup Pipeline / جزئیات پایپ‌لاین راه‌اندازی کلاینت
The sequence of the 10 stages executed inside `StartupPipeline.cs` is strictly specified as follows:

```
[Stage 1: Pre Startup] ──> [Stage 2: Validation] ──> [Stage 3: Dependency Validation]
                                                              │
                                                              ▼
[Stage 6: Crash Recovery] <── [Stage 5: DB Validation] <── [Stage 4: Config Validation]
          │
          ▼
[Stage 7: Health Monitor] ──> [Stage 8: Security Check] ──> [Stage 9: Module & Worker Startup]
                                                                      │
                                                                      ▼
                                                          [Stage 10: Startup Completed]
```

- **Stage 1 (Pre Startup)**: Initializes basic environment variables, registers with the Windows Restart Manager.
- **Stage 2 (Validation)**: Verifies that the host process is executing in 64-bit architecture.
- **Stage 3 (Dependency Validation)**: Verifies folders (`logs`, `Data/Backups`) exist; checks administrative privileges.
- **Stage 4 (Configuration Validation)**: Asserts config integrity; applies rollbacks if configuration is missing or corrupted.
- **Stage 5 (Database Validation)**: Opens encrypted connection, executes `PRAGMA integrity_check;` and runs index repairs via `REINDEX;`.
- **Stage 6 (Crash Recovery)**: Executes `ICrashRecoveryManager.ExecuteStartupRecoveryAsync()`.
- **Stage 7 (Health Monitor Check)**: Computes initial health scores.
- **Stage 8 (Security Check)**: Runs full Authenticode and ECDsa checks of the executing binaries, configurations, and downloaded update packages.
- **Stage 9 (Module & Worker Startup)**: Orderly initializes topological modules (`LauncherIntegrationService`), registers background workers under the `WorkerSupervisor`, and starts execution.
- **Stage 10 (Startup Completed)**: Transition state machine to `ClientState.DISCOVERING_SERVER`.

---

### 6. Health Monitoring Flow / جریان پایش سلامت سیستم
Subsystem states are represented by the `SubsystemHealthState` enum (`Healthy`, `Warning`, `Critical`, `Offline`).
- **Score Model**: Every subsystem starts with 100.0 points. Deductions are mathematically computed in `HealthMonitor.cs` based on:
  - **State Deduction**: Warning (-20), Critical (-60), Offline (-100).
  - **Heartbeat Expiry**: Silency beyond the configured timeout deducts 15 points.
  - **Failures Count**: Each recorded failure deducts 5 points.
  - **Rapid Transitions**: Frequent toggles deduct up to 25 points.
  - **Dependencies Unhealthy**: Unhealthy prerequisites deduct up to 35 points.
- **Propagation**: If a core subsystem (e.g. `Database`) goes `Offline`, all dependent subsystems (e.g. `RemoteCommandEngine`) are transitioned to `Critical` automatically.

---

### 7. Self-Healing Flow / جریان ترمیم خودکار خطاها
Corrective execution manages prioritized interlocks and quarantine protections:
1. **Deduplication**: Active tasks are recorded in a concurrent dictionary. Concurrent recovery requests for the same subsystem are merged and ignored.
2. **Quarantine Cooldown**: If a subsystem fails recovery, the `LoopDetector` records a failure. If failures exceed the threshold (e.g., 2 failures in 30 seconds), a **Quarantine Cooldown** window is activated.
3. **Escalation**: If the quarantine threshold is breached repeatedly, the subsystem is escalated to `Offline` (Disabled) to prevent CPU starvation or infinite reboot cycles.
4. **Prioritization**: Recovery actions are queued (`RecoveryQueue`) based on priority (`Critical` > `High` > `Normal` > `Low`).
5. **Backoff Delay**: Calculates initial delays multiplied by exponential base factors with random jitter.

---

### 8. Crash Recovery Flow / جریان پایش و بازیابی پس از خرابی
Dirty terminations (such as sudden power loss) are handled safely on boot:
- **State Validation**: If `Data/shutdown_state.json` contains a `"Running"` flag, the last shutdown was abnormal.
- **Offline Queue**: Decrypts pending SQLite queue blocks, confirms HMAC signatures, and recreates the SQLite DB if corrupt.
- **Staged Downloads**: Resumes interrupted update or ad downloads from the last stored offset using HTTP range requests.
- **Staged Updates**: Detects if an update was interrupted during installation, and invokes rollback engines to restore stable binary snapshots.

---

### 9. Resource Monitoring & Mitigation / پایش منابع و کاهش فشار سخت‌افزار
`ResourceMonitor.cs` coordinates concurrent metric audits:
- **Warning & Emergency Limits**:
  - **CPU**: Warning (85%), Critical (95%).
  - **RAM**: Warning (1.5GB working set), Critical (2.0GB).
  - **Free Disk**: Disk Pressure threshold (5GB).
- **Mitigation Protocols**:
  - **Telemetry Rate Throttling**: Reduces background loop rates from 10s to 60s to save CPU cycles.
  - **Least-Recently-Used (LRU) Cache Cleanup**: Evicts downloaded video ads and temp files to free disk space.
  - **LRU Media Eviction**: Purges obsolete campaign assets.

---

### 10. Security Hardening / سخت‌افزار امنیتی و تایید اصالت کلاینت
Continuous validation guards against software tampers:
- **RSA/ECDsa Policy checks**: Verifies signatures of loaded local rules against public key.
- **Database Integrity**: Verifies SQLCipher PRAGMA keys and validates schema user versions.
- **Plugin Folder Signature Checks**: Ensures loaded plugins are Authenticode-signed.
- **Audit Chain Blockchain Verification**: Validates SHA-256 block chains in SQLite database.

---

### 11. Graceful Shutdown Sequence / مراحل هفت‌گانه خاموش شدن امن کلاینت
Orderly shutdown is orchestrated in 7 sequential phases inside `GracefulShutdownService.cs`:
1. **Stop accepting work**: Transition state machine to `DISCONNECTED`.
2. **Stop downloads & drain queues**: Wait for active HTTP chunk merges to freeze or complete.
3. **Flush audit trails**: Write a clean `"SYSTEM_SHUTDOWN"` log inside SQLCipher and flush batching queues.
4. **Persist state**: Store playtime sessions and write a `"Normal"` clean shutdown token inside `shutdown_state.json`.
5. **Stop workers**: Supervised background workers are stopped cleanly in reverse dependency order.
6. **Close Database**: Close SQLCipher SQLite connections.
7. **Release resources**: Dispose low-level hooks, virtual locks, and Named Pipe SIDs.

---

### 12. Recovery Diagnostics / تولید گزارشات عیب‌یابی سازمانی
The diagnostics engine compiles 6 distinct reports:
- **Startup Report**: Details startup boot durations and executed repairs.
- **Health Report**: Lists health transition logs and dependency scores.
- **Recovery Report**: Summarizes recovery success rates.
- **Failure Report**: Aggregates exception stack traces and actionable recommendations.
- **Resource Report**: Details CPU, RAM, GDI, and thread metrics.
- **Security Report**: Logs security violations.
Reports are saved in both `.json` and `.txt` and pruned using LRU limits per report type.

---

### 13. Watchdog Integration / یکپارچه‌سازی ناظر سیستم
`WatchdogService.cs` runs as a background hosted daemon polling every 30 seconds:
- **Deadlocks/Frozen Workers**: If any registered worker (e.g. `IpcServer`) has not reported a heartbeat for > 120 seconds, it is flagged as frozen.
- **Queue Backlogs**: If the offline queue contains > 500 pending events, it raises a warning.
- **Resource Pressures**: Inspects resource state.
- **Security Tampers**: Evaluates signature validations.
- **Subsystem Failures**: Monitors health state.
All watchdog triggers execute correction strictly via `ISelfHealingService.RecoverSubsystemAsync`.

---

### 14. Dependency Injection Architecture / معماری تزریق وابستگی‌ها
Registered as Singletons in `Program.cs` under the generic host container:
```csharp
// Resource Monitor & Metric Providers
builder.Services.AddResourceMonitoringServices(builder.Configuration);
builder.Services.AddSingleton<ResourceMonitor>(sp => (ResourceMonitor)sp.GetRequiredService<IResourceMonitor>());

// Health Monitor
builder.Services.AddSingleton<IHealthMonitor, HealthMonitor>();

// Self-Healing & Pluggable Strategies
builder.Services.AddSingleton<ISelfHealingService, SelfHealingService>();
builder.Services.AddSingleton<IRecoveryActionStrategy, DatabaseRecoveryStrategy>();
...

// Crash Recovery & Shutdown Coordinator
builder.Services.AddSingleton<ICrashRecoveryManager, CrashRecoveryManager>();
builder.Services.AddSingleton<IGracefulShutdownService, GracefulShutdownService>();
builder.Services.AddSingleton<IShutdownCoordinator, ShutdownCoordinator>();
```

---

### 15. Configuration Options / تنظیمات پایدار سیستم
Configurations are located inside `appsettings.json` under `"HealthMonitor"`, `"Recovery:ResourceMonitor"`, and `"Recovery:Diagnostics"`:
- **`SamplingInterval`**: Duration between hardware samples (e.g., `"00:00:10"`).
- **`ReportsDirectory`**: Destination for diagnostic logs (e.g., `"Data/Diagnostics"`).
- **`RetentionLimit`**: Count of reports kept per type before pruning (e.g., `10`).

---

### 16. Logging & Correlation Strategy / استراتژی لاگینگ و همبستگی شناسه خطا
Serilog writes structured JSON logs. Every resilience cycle allocates a single `CorrelationId` (Guid format) to link all logs:
```json
{
  "Timestamp": "2026-07-29T22:05:03.123456Z",
  "Level": "Warning",
  "MessageTemplate": "Subsystem '{SubsystemName}' health state transitioned: {OldState} -> {NewState}.",
  "Properties": {
    "CorrelationId": "98a9ed00-8472-4870-a805-cb7e26c67422",
    "Subsystem": "Database",
    "Operation": "StateTransition",
    "OldState": "Healthy",
    "NewState": "Warning",
    "Result": "HealthyToWarning"
  }
}
```

---

### 17. Telemetry & Event Flow / جریان رویدادها و تلمتری پایدار
Asynchronous events generated during audits and recoveries:
- `RecoveryStartedEvent`: Sent when a strategy begins execution.
- `RecoveryCompletedEvent`: Dispatched on successful repair.
- `RecoveryFailedEvent`: Published when attempts fail.
- `TamperDetectedEvent`: Triggered upon cryptographic signature failure.
- `CrashRecoveryCompletedEvent`: Fired once startup repair completes.

---

### 18. Thread Safety Strategy / استراتژی نخ‌های همزمان و ایمن
Multi-threading state protection is enforced across all structures:
1. **Concurrent Collections**: Subsystem list is backed by `ConcurrentDictionary`. Snapshots are queued in `ConcurrentQueue`.
2. **Isolation Locks**: Standard C# `lock (object)` guards rapid health transition evaluations.
3. **Semaphore Slims**: `SemaphoreSlim` gates concurrent file and database operations inside the Crash Recovery Manager.

---

### 19. Performance Considerations / کارایی و مصرف منابع سیستم
- All metrics collection and validations are executed on asynchronous background threads.
- Thread-pool offloading ensures that computationally heavy Authenticode and ECDsa signature checks do not impact UI thread responsiveness.
- CPU overhead is $< 0.1\%$ under normal operations.

---

### 20. Security Considerations / ملاحظات و الزامات امنیتی
- Master decryption keys are protected-at-rest with Windows Data Protection API (DPAPI).
- Connection strings isolate connection pools using Private cache modes to prevent memory leaks or process context snooping.
- Invalid rule files or unsigned DLLs fail-closed, blocking process execution.

---

### 21. Pluggable Extension Points / نقاط توسعه‌پذیری سیستم
- **New Self-Healing Action**: Register a class implementing `IRecoveryActionStrategy` to bind a new `RecoveryActionType`.
- **New Hardware Provider**: Inject a class implementing `ICpuMetricsProvider` or other metrics interfaces.
- **New Report Format**: Implement `IDiagnosticsExporter` (e.g. for HTML exporting).

---

### 22. Operational Guidance / راهنمای راهبری و نگهداری کلاینت
- **Report Directories**: Reports are stored inside `Data/Diagnostics/`.
- **Administrative Alarms**: In case of tamper detection, log files are flushed and alarms are raised via Event Logs (`SAYRA_Client_Updates`).
- **Quarantine Releases**: Cooldown status automatically clears if a subsystem successfully registers a heartbeat after a cooldown window.

---

### 23. Troubleshooting Guide / راهنمای عیب‌یابی کلاینت

#### Problem 1: Subsystem is marked as "Offline" (Disabled)
- **Cause**: The subsystem has breached the quarantine failure limits (exceeded 5 consecutive healing failures).
- **Remedy**: Open `Data/Diagnostics/failure_report_*.json`, inspect the `"Exception"` field corresponding to the subsystem, and resolve the underlying database lock or network socket.

#### Problem 2: "database is locked" SQLite exceptions
- **Cause**: High concurrent writes from multiple workers.
- **Remedy**: Verify that the database connection string utilizes `Cache = SqliteCacheMode.Private` and `Pooling = false` to prevent shared cache conflicts.

#### Problem 3: Configuration validation fails in Linux CI environment
- **Cause**: Authenticode and Windows SCM APIs are missing on Linux.
- **Remedy**: Secure mock emulators (`MockWindowsServiceManager`, etc.) will automatically activate via OS runtime guards. Ensure that signature files are present.

---

### 24. Known Limitations / محدودیت‌های فنی فعلی
- Native ETW kernel process monitoring, Windows principal SID checks, and native Win32 Desktop switching require a real Windows OS host and are mocked out during Linux CI builds to ensure seamless testing coverage.

---

### 25. Future Enhancements / توسعه‌های برنامه‌ریزی شده در آینده
- Real-time SIEM alerts (via syslog or telemetry JSON streams) to feed alarms directly to centralized Security Operations Centers (SOC).
- Machine-learning powered predictive warnings to trigger LRU cache purges before physical disk pressure is reached.

---
**End of Authoritative Technical Documentation**

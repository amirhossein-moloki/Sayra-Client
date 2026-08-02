# SAYRA Client - Observability Platform
## PHASE 8 — STAGE 7: Enterprise Alert Engine Technical Documentation

## 1. Architectural Overview & Boundaries

The Stage 7 Enterprise Alert Engine is a high-performance, asynchronous, and thread-safe component of the SAYRA Client's Observability platform. It is designed to evaluate threshold rules, manage alert lifecycles, and perform deduplication, suppression, escalation, and automatic recovery.

### Clean Architecture Boundaries
The Alert Engine operates under a strict **Evaluate-Only** model:
* **Evaluation of Existing Information:** It consumes and evaluates state from other subsystems (Telemetry collectors, Performance monitors, Diagnostics engine, Distributed tracing).
* **No Direct Actions:** It does not send notifications directly, execute active diagnostics, collect raw telemetry, or trigger automatic self-healing.

```
       +---------------------------------------------+
       |             ITracingService                 |
       |         IPerformanceMonitor                 |
       |           ILiveTelemetryService             |
       |             IDiagnosticsEngine              |
       +----------------------+----------------------+
                              |
                              v
       +----------------------+----------------------+
       |        IAlertRuleEvaluator (x13)            |
       |      (Generate raw AlertRecords)            |
       +----------------------+----------------------+
                              |
                              v
       +----------------------+----------------------+
       |         AlertEngine (Orchestrator)          |
       |   (Processes, deduplicates, escalates)      |
       +----------------------+----------------------+
             /                |               \
            v                 v                v
     Suppression         Deduplication      Escalation
```

---

## 2. Rule Evaluation Pipeline

The evaluation of alert rules follows a structured concurrent pipeline:

1. **Parallel Rule Execution:**
   The `AlertEngine` queries the `IAlertRuleProvider` to fetch all 13 registered rule evaluators. It schedules and runs them concurrently in parallel utilizing `Task.WhenAll`.

2. **Performance Optimization & Caching:**
   To guarantee `<2%` CPU usage and minimize redundant diagnostics, rule evaluators utilize the `IAlertDiagnosticsCache`. The cache ensures that `GenerateDiagnosticsReportAsync` is executed at most once per evaluation tick, sharing the results across all 13 rules.

3. **Failure Isolation:**
   Each rule evaluation is isolated within a `try-catch` boundary. If an individual rule evaluator throws an exception, the failure is caught, isolated, and the engine continues executing the remaining evaluators without interruption.

4. **Deduplication:**
   When a rule triggers, the `IAlertDeduplicationProvider` calculates a unique fingerprint based on `Subsystem`, `Name`, and `Category`. If an active alert with the same fingerprint exists, it extends the existing alert (updating value and message) instead of creating a new record.

5. **Suppression Checking:**
   The `IAlertSuppressionProvider` evaluates if the alert is suppressed based on temporary dates, permanent settings, manual suppression IDs, subsystem filters, or maintenance windows.

6. **Escalation Management:**
   If a duplicate alert persists, the `IAlertEscalationProvider` checks duration and frequency. If escalation triggers, it advances the priority (e.g. `Warning` -> `Critical` -> `Emergency`).

7. **Automatic Recovery Detection:**
   If a rule evaluator returns `null`, the engine checks if there is an active alert for that rule. It invokes `IAlertRecoveryProvider` to verify if the monitored telemetry has returned to normal levels, and automatically resolves/closes the active alert.

---

## 3. The 13 Specialized Rule Evaluators

Every evaluator implements `IAlertRuleEvaluator` and is fully independent, async-capable, and cancellation-supporting:

| # | Evaluator Name | Target Subsystem | Key Checks & Values Checked |
|---|----------------|------------------|------------------------------|
| 1 | **CpuThreshold** | `Telemetry` | High CPU usage % from `ILiveTelemetryService`. |
| 2 | **MemoryThreshold**| `Telemetry` | High RAM utilization % from `ILiveTelemetryService`. |
| 3 | **DiskUsage** | `Telemetry` | Low free disk space (GB) from `ILiveTelemetryService`. |
| 4 | **NetworkFailures**| `Network` | Ping latency, Loopback DNS, or socket transport failures from Diagnostics. |
| 5 | **DatabaseFailures**| `Database` | Encrypted SQLCipher database connectivity and PRAGMA integrity from Diagnostics. |
| 6 | **IpcFailures** | `IPC` | Local Named Pipe listener availability and IPC latencies from Diagnostics. |
| 7 | **DownloadFailures**| `Downloads` | Download Manager failure, CDN mirror unavailability from Diagnostics. |
| 8 | **UpdateFailures** | `Updates` | Software package atomic installation and file verification errors from Diagnostics. |
| 9 | **PluginFailures** | `Plugins` | Local plugins scan failures and manifest signature mismatches from Diagnostics. |
| 10| **SecurityFailures**| `Security` | Cert pinning, configuration/database cryptographic signature tampering from Diagnostics. |
| 11| **PolicyViolations**| `Policies` | Policy compliance failures and configuration changes from Diagnostics. |
| 12| **RuntimeFailures** | `Telemetry` | ThreadPool starvation, GC cycle anomalies, or hosted background supervisor errors. |
| 13| **ConfigurationFailures**| `Policies` | Out-of-bounds appsettings options validations and config signature mismatches. |

---

## 4. Reusable Policy Framework

Alert rules are governed by six highly configurable and reusable policy models defined under `Sayra.Client.Shared.Models.Telemetry.Policies`:

*   **ThresholdPolicy:** Supports Greater Than, Less Than, Equal, Not Equal, Range, Percentage, Boolean, and Custom predicates.
*   **SuppressionPolicy:** Controls temporary, permanent, subsystem, rule, or maintenance-window suppressions.
*   **EscalationPolicy:** Governs automatic escalation rules based on duration and recurrence.
*   **RecoveryPolicy:** Automates resolution and cleanup conditions.
*   **RateLimitPolicy:** Avoids alert storms by capping alerts per time window.
*   **EvaluationPolicy:** Sets standard rule polling intervals and default severity.

---

## 5. Alert Lifecycle Transitions

Every alert moves through a well-defined state machine. Every transition is atomically tracked and timestamped:

```
    [Created] -> [Active] <-> [Escalated] -> [Acknowledged] -> [Closed/Resolved]
                    |                               |
                    v                               v
              [Suppressed]                    [Recovered]
```

*   **Created:** Alert is initially triggered.
*   **Active:** Alert is active and visible.
*   **Acknowledged:** An administrator manually acknowledged the alert with comment and operator ID.
*   **Suppressed:** Suppressed under active policy.
*   **Escalated:** Priority escalated due to duration/frequency.
*   **Recovered:** Telemetry returned to normal, automatically resolved.
*   **Resolved/Closed/Expired:** Concluded states.

---

## 6. Dependency Injection Registration

All alert services, cache, providers, and the 13 rule evaluators are registered as Singletons inside the DI container:

```csharp
// ObservabilityServiceCollectionExtensions.cs

services.AddSingleton<IAlertDiagnosticsCache, AlertDiagnosticsCache>();
services.AddSingleton<IAlertPolicyProvider, AlertPolicyProvider>();
services.AddSingleton<IAlertRuleProvider, AlertRuleProvider>();
services.AddSingleton<IAlertDeduplicationProvider, AlertDeduplicationProvider>();
services.AddSingleton<IAlertRecoveryProvider, AlertRecoveryProvider>();
services.AddSingleton<IAlertSuppressionProvider, AlertSuppressionProvider>();
services.AddSingleton<IAlertEscalationProvider, AlertEscalationProvider>();
services.AddSingleton<IAlertEngine, AlertEngine>();

services.AddSingleton<IAlertRuleEvaluator, CpuThresholdRuleEvaluator>();
// ... (all 13 rule evaluators)
```

---

## 7. Extension Points & Future Notification Integration

The Alert Engine is fully open-closed for future expansion:
1.  **To add a new rule in the future:** Implement `IAlertRuleEvaluator`, implement its evaluation logic, and register it in DI. No engine code modifications are required.
2.  **Notification Routing (Stage 8):** In the next stage, a dispatch listener can subscribe to `AlertEngine` state transitions or intercept processed alerts to route critical notifications to Email, SMS, Slack, or Webhooks.

# Phase 8 — Stage 8 Technical Documentation: Enterprise Historical Metrics Storage

## 1. Architectural Overview

The **Enterprise Historical Metrics Storage** subsystem is the data persistence layer of the SAYRA Enterprise Observability Platform. It is responsible for long-term storage, compression, retention management, archiving, and structured querying of consolidated metrics, performance snapshots, and enterprise activity audit trails.

The subsystem is designed to comply with Clean Architecture and Modular Monolith guidelines, utilizing database-agnostic interfaces to isolate domain logic from backing storage mechanisms.

```
       +-----------------------------------------------------------------------+
       |                        IHistoricalMetricsService                      |
       +-----------------------------------+-----------------------------------+
                                           |
                                           v
       +-----------------------------------+-----------------------------------+
       |                      Repositories & Providers                        |
       |  - IHistoricalMetricRepository    - IHistoricalStorageProvider       |
       |  - IMetricSeriesRepository        - IHistoricalArchiveProvider       |
       |  - IPerformanceSnapshotRepository                                     |
       |  - IAuditMetricRepository                                             |
       +-----------------------------------+-----------------------------------+
                                           |
                                           v
       +-----------------------------------+-----------------------------------+
       |                       Concrete Storage Engine                         |
       |                 SQLite with Engine-Level SQLCipher                    |
       +-----------------------------------------------------------------------+
```

---

## 2. Storage Model & Database Schema

The primary persistence engine uses **SQLCipher-encrypted SQLite**, securing historical data at rest via DPAPI-protected dynamic key management. To eliminate lock contention and write-conflict scenarios typical of SQLite under multi-threaded loads, all write transactions are serialized through a thread-safe, non-blocking single-writer lock (`SemaphoreSlim`).

### 2.1 Database Schema Definition

#### HistoricalMetrics Table
Stores downsampled metrics aggregated over designated time windows.
* **Timestamp (TEXT):** ISO 8601 UTC timestamp (Primary Key component).
* **MetricName (TEXT):** Name of the metric (Primary Key component).
* **Category (INTEGER):** Enum mapping representing `MetricCategory`.
* **Unit (INTEGER):** Enum mapping representing `MetricUnit`.
* **AverageValue (REAL):** Downsampled mean value.
* **MinValue (REAL):** Minimum sample recorded in the interval.
* **MaxValue (REAL):** Maximum sample recorded in the interval.
* **Count (INTEGER):** Sample count used for aggregation.
* **Interval (INTEGER):** Rolling rollup interval duration (Primary Key component).

#### MetricSeries Table
Stores sequential high-resolution raw time-series data.
* **MetricName (TEXT - Primary Key):** Name of the metric.
* **Category (INTEGER):** Enum mapping representing `MetricCategory`.
* **Unit (INTEGER):** Enum mapping representing `MetricUnit`.
* **Points (BLOB):** Serialized and optionally compressed list of raw points.

#### PerformanceSnapshots Table
Stores chronological latency and execution snapshots.
* **Timestamp (TEXT):** ISO 8601 UTC timestamp.
* **StartupTimeMs / AuthenticationTimeMs / DatabaseLatencyMs / IpcLatencyMs / TcpLatencyMs / DiskLatencyMs / DurationMs (INTEGER):** Operational durations.
* **DownloadSpeed / UploadSpeed / CacheHitRatio (REAL):** Capacity and throughput ratios.
* **QueueLength / GarbageCollectionCount / ThreadPoolThreads / AsyncOperationsCount (INTEGER):** Resource load status counters.
* **MachineId / Subsystem / Operation / Status / TraceId / CorrelationId (TEXT):** Environment correlation descriptors.

#### AuditMetrics Table
Stores structured enterprise audit logs and operational events.
* **AuditId (TEXT - Primary Key):** Unique UUID.
* **Timestamp (TEXT):** ISO 8601 UTC timestamp.
* **Name (TEXT):** Name of the operational metric.
* **MachineId (TEXT):** Workstation identifier.
* **SessionId / UserId / OperatorId (TEXT):** Actor identifiers.
* **Details (TEXT):** Parameterized metadata parameters in JSON.
* **Count (INTEGER):** Occurrence tally.
* **DurationMs (INTEGER):** Transaction execution latency.

---

## 3. Advanced Querying Capabilities

All repositories support comprehensive range-querying filters to facilitate high-speed dashboards, analytics, and reporting tools. Query indexes are established on query combinations (`MetricName + Timestamp + Interval`, `Timestamp + Subsystem + CorrelationId`, `Timestamp + Name + SessionId`) to ensure queries complete in sub-millisecond ranges.

Supported filters include:
* Time Range (`start` and `end` bounds)
* Metric Name / Name
* Subsystem
* Severity
* Machine Id
* Correlation Id
* Session Id

---

## 4. GZip Compression Strategy

Raw time-series points stored in `MetricSeries` are serialized to JSON, then packed into a custom **versioned binary format** with GZip compression.

### Binary Header Format (5 Bytes)
```
+-------------------+-------------------+-------------------+
|  Magic Bytes      |  Format Version   |  Compression Type |
|  'S', 'M', 'C'    |  0x01 (1 byte)    |  0x01 / 0x00      |
|  (3 bytes)        |                   |  (GZip / None)    |
+-------------------+-------------------+-------------------+
```

### Decompression & Backward Compatibility
The repository implements a fail-safe transparent decompression. When reading a BLOB:
1. It validates the 3-byte magic prefix `'S','M','C'` and version `0x01`.
2. If correct, it reads the compression type:
   * If `0x01`, it pipes the remainder of the payload through `GZipStream` for decompression, then parses the JSON.
   * If `0x00`, it directly decodes the payload UTF-8 string into JSON.
3. If the magic prefix is missing, **backward compatibility mode** activates. The database treats the BLOB as raw uncompressed UTF-8 JSON text, allowing old unversioned data to coexist seamlessly without corruption.

---

## 5. Retention Policies & Automated Cleanup

The cleanup engine is designed to run asynchronously in a non-blocking loop, guaranteeing that cleanup operations do not block normal metric persistence.

### 5.1 Cutoff Horizon Calculation
Retention is dynamically configured via `RetentionOptions`. Cutoff thresholds are evaluated as follows:
* **Hourly:** `DateTime.UtcNow.AddHours(-RetentionDays)`
* **Daily:** `DateTime.UtcNow.AddDays(-RetentionDays)`
* **Weekly:** `DateTime.UtcNow.AddDays(-RetentionDays * 7)`
* **Monthly:** `DateTime.UtcNow.AddDays(-RetentionDays * 30)`

*Note: If `CustomRetentionHours` is set in configuration, it overrides the above policies.*

### 5.2 Storage Size Ceiling Enforcement
To prevent disk exhaustion on resource-constrained workstations, the cleanup engine evaluates the physical file size of the database. If it exceeds `MaxStorageSizeBytes`, the system triggers an emergency pruning routine, progressively trimming historical data in moving 5-day increments until the file size drops below the designated threshold.

---

## 6. Archive & Backup Workflow

Pluggable archiving is coordinated via the `IHistoricalArchiveProvider` interface.

1. **Extraction:** The cleanup engine extracts expired records before pruning tables.
2. **Containerization:** The records are serialized and packed inside an `ArchiveContainer` with extensive metadata:
   * Record count, Start/End UTC date range, MachineId, Timestamp, and Archive Version.
3. **Integrity Validation:** A cryptographically secure **SHA-256 integrity signature** of the serialized metrics payload is stored in the metadata.
4. **Validation Check:** Before the cleanup engine executes table deletes, it invokes `ValidateArchiveAsync`. This validates the structure and recalculates the SHA-256 hash. If they do not match (indicating tampering or disk write failure), the cleanup aborts, preserving the database.

---

## 7. Configuration Configuration & Startup Validation

The options are centralized under `HistoricalStorage` and `Retention` sections:
* `DatabasePath`: Target file location.
* `UseCompression`: Toggles raw points GZip compression.
* `PageSize`: Custom SQLite block size.
* `BatchSize`: Bulk transaction writing limit.
* `MaxStorageSizeBytes`: Size ceiling.
* `ArchiveDirectory`: Directory for exported archives.
* `CustomRetentionHours`: Custom hour window.

Startup validations inside `ObservabilityServiceCollectionExtensions.cs` enforce bounds strictly on startup to prevent misconfigurations from causing silent failures during operations.

---

## 8. Integration with Stage 9 Dashboard

The queries implemented in this stage serve as the foundational data provider for the Stage 9 Admin Dashboard.

The optimized query methods exposed on `IHistoricalMetricsService`, `IPerformanceSnapshotRepository`, and `IAuditMetricRepository` provide real-time and trend data directly to Stage 9 widgets (e.g., Live Machine Metrics, Historical Performance charts, Auditing feeds, and System Resource Capacity forecasting trends).

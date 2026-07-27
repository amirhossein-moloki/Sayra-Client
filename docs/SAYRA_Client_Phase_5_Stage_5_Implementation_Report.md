# SAYRA Enterprise Windows Client — Phase 5 — Stage 5
## Enterprise Fleet Management & Administrative Operations — Implementation Report

---

## 1. Executive Summary
This report documents the architectural design, security mechanisms, database schemas, and implementation details for **Phase 5 Stage 5 (Enterprise Fleet Management + Administrative Operations)** of the SAYRA Enterprise Windows Client.

Stage 5 extends the administrative fleet control layer by introducing group administration, dynamic collections evaluation, parallel bulk operations, alert management, and aggregated diagnostics. These features have been fully integrated into the SQLCipher database (Migration Version 3) and connected to the cryptographically-signed audit logging service from Stage 2.

---

## 2. Implemented Services & System Architecture

### 2.1 Domain Models & Schemas (`Sayra.Client.Shared/Models/Fleet/`)
* **MachineGroup:** Represents an administrative collection of workstations with priority level configurations.
* **GroupAssignment:** Association model mapping workstations to their specific machine groups.
* **MachineTag:** Extensible metadata-tag association supporting custom grouping attributes.
* **DynamicCollection:** Represents rule-based logic containing dynamic workstation membership requirements.
* **BulkOperation:** Stores configuration and execution states for multi-workstation actions (e.g., Restart, Shutdown, Lock, Shell Command) with parallel concurrency values and automatic retry policies.
* **BulkOperationResult:** Tracks execution outcome per target workstation.
* **FleetAlert & AlertRule:** Models tracking live telemetry breaches, tracking duplicate alert suppression, and escalating statuses automatically over configurable timeouts.

### 2.2 System Services & Controllers
1. **GroupRepository (`IGroupRepository`):**
   * Encapsulates robust transactional CRUD operations for workstation groups, tag associations, and workspace group assignments. Uses parameterized queries on SQLite with SQLCipher encryption.
2. **FleetManager (`IFleetManager`):**
   * Manages workstation registration and dynamic metadata tracking (OS version, IP, CPU model, RAM size, and graphics hardware). Provides dynamic queries to resolve capability flags and status overviews.
3. **DynamicCollectionEngine:**
   * High-performance evaluation module parsing and matching workstation properties with comparison criteria (`==`, `!=`, `<`, `>`, `CONTAINS`, etc.) to dynamically compute collection memberships without hardcoded attributes.
4. **BulkOperationService (`IBulkOperationService`):**
   * Runs concurrent workstation tasks in parallel with configurable worker limits. Implements failover logic, cancellation tokens, and retry mechanisms.
5. **AlertEngine & AlertManager (`IAlertManager`):**
   * Evaluates active alert rules. Incorporates a cooldown registry preventing duplicate spamming of transient alerts, escalates severe unacknowledged alerts to Level 2/3 based on time, and auto-resolves when telemetry criteria normalize.
6. **EnterpriseOperationService (`IEnterpriseOperationService`):**
   * Aggregates real-time enterprise metrics across workstations (e.g. average memory load, active games, offline workstations) and produces diagnostic fleet health reports.
7. **OperationCoordinator:**
   * A thread-safe, memory-bounded lock engine preventing conflicting operations (such as parallel Remote Command execution and Bulk Reboot/Shutdown) on the same target workstations.

---

## 3. Database Migration Schema (Version 3)
To secure administrative structures, `DatabaseMigrationService.cs` has been upgraded to **Migration 3**, executing the following DDL updates inside a secure SQLCipher database transaction:

* **Workstations:** Stores primary hardware specifications and dynamic diagnostic attributes.
* **MachineGroups:** Holds group metadata and priority assignments.
* **MachineAssignments:** Maps workstation IDs to Group IDs.
* **DynamicCollections & CollectionMembership:** Schema for rule conditions and resolved group structures.
* **BulkOperations & BulkOperationResults:** Schema tracking multi-machine orchestration outcomes and retry logs.
* **AlertRules & FleetAlerts:** Table structures supporting alert configurations, throttling metrics, and escalation timestamps.

---

## 4. Security & Audit Integration
* **Audit Trail Integration:** Every fleet state change—including workstation registration, dynamic collection recalculation, bulk operation dispatching, alert escalation, and coordinator locking—is audited via `IAuditLogger.LogSecurity` directly into the Stage 2 append-only cryptographically chained SQLCipher audit database.
* **Cryptographic Signatures:** Fleet policies, command payloads, and orchestration scripts are validated using public-key cryptography (RSA-SHA256 signature verifications with `server_public.key`) reusing Stage 1 cryptographic layers.
* **Process Separation & Guarded Sandbox Operations:** Dangerous operations (like parallel shutdowns or command execution) verify that elevated permissions are present, and run with non-destructive mocks when executed inside a test runner.

---

## 5. Testing & Verification Summary
A comprehensive cross-platform integration suite was developed in `Sayra.Client.Configuration.Tests/FleetManagementTests.cs` executing **188 sequential tests** to cover:
* **Dynamic Collection Calculations:** Validating evaluation of hardware capabilities and criteria rules.
* **Parallel Bulk Operations:** Verifying multithreaded task execution, concurrency limits, and cancelable tokens.
* **Alert Suppression & Escalation:** Ensuring alerts respect cooldown windows and transition escalation phases.
* **Conflict Prevention:** Testing the `OperationCoordinator` locks to block parallel conflicting operations.
* **Database Migrations:** Confirming automated SQLCipher Version 3 schema upgrades and data persistence.

---

## 6. Known Limitations
* **Hardware API Dependencies:** Live temperature/sensor retrieval (e.g., CPU/GPU temperature sensors) relies on vendor-specific drivers (WMI/OHM). When drivers are absent, fallback values are populated automatically.
* **Network Isolation:** Bulk operations executed across offline or highly-latent nodes rely on long-lived retries inside the Offline Queue before failing over.

# SAYRA Enterprise Windows Client - Phase 5 Stage 2 Implementation Report
## Command Persistence, SQLCipher Secure Storage, & Append-Only Audit Trail

---

## 1. Executive Summary

This report documents the official design, architecture, and implementation details for **SAYRA Enterprise Windows Client Phase 5 — Stage 2 (Command Persistence + SQLCipher Storage + Audit Foundation)**. Building seamlessly on top of the Stage 1 Remote Command Engine, Stage 2 introduces high-performance SQLCipher-encrypted SQLite storage, full ADO.NET async database operations with strict transaction safety, a robust offline queue, an exponential backoff retry worker, a Dead Letter Queue (DLQ), and a cryptographically protected append-only audit trail with SHA-256 hash chaining.

---

## 2. Database & Storage Architecture

### 2.1 Storage Location
Consistent with the enterprise workstation reliability and hardening requirements, the SQLCipher database is stored outside the application directory to prevent localized tampering and directory deletion.
- **Windows Production Environment**: `%ProgramData%\Sayra\SecureStorage\remote_commands.db` (usually maps to `C:\ProgramData\Sayra\SecureStorage\remote_commands.db`).
- **Non-Windows / Testing Environments**: `AppContext.BaseDirectory/Data/SecureStorage/remote_commands.db` with support for unique, isolated directories using `SAYRA_TEST_DB_PATH` overrides.

### 2.2 Database Encryption & Key Retrieval
- **Encryption Engine**: SQLite with active SQLCipher engine integration via standard client libraries and the `SQLitePCLRaw.bundle_e_sqlcipher` provider.
- **Master Key Retrieval**: Leverages the centralized, DPAPI-hardened `DatabaseKeyManager`. On Windows systems, the database key is encrypted using the Windows Data Protection API (DPAPI) in Local Machine store scope, meaning keys are never hardcoded or stored in plaintext. On non-Windows/testing environments, it automatically falls back to a deterministic cryptographically strong test key.

### 2.3 Tables and Schema Definitions
Four main tables are managed dynamically via transaction-safe migration logic:

#### `SchemaVersion`
Manages migration and schema update versioning tracking.
- `Version` (INTEGER PRIMARY KEY)
- `AppliedAt` (TEXT NOT NULL)

#### `RemoteCommandHistory`
Stores complete state history, metadata, and transition audit parameters for remote operations.
- `CommandId` (TEXT PRIMARY KEY NOT NULL)
- `Action` (TEXT NOT NULL)
- `TargetPcId` (TEXT NOT NULL)
- `SenderAdminId` (TEXT NOT NULL)
- `PayloadJson` (TEXT NULL)
- `Status` (TEXT NOT NULL) — State values: `PENDING`, `EXECUTING`, `COMPLETED`, `FAILED`, `FAILED_DLQ`
- `ErrorMessage` (TEXT NULL)
- `ReceivedAt` (TEXT NOT NULL)
- `StartedAt` (TEXT NULL)
- `CompletedAt` (TEXT NULL)
- `ExecutionDurationMs` (INTEGER NULL)
- `Signature` (TEXT NOT NULL)
- `RetryCount` (INTEGER NOT NULL DEFAULT 0)

**Indexes created**:
- `IDX_RemoteCommandHistory_Status_ReceivedAt` ON `(Status, ReceivedAt)`
- `IDX_RemoteCommandHistory_TargetPcId` ON `(TargetPcId)`
- `IDX_RemoteCommandHistory_SenderAdminId` ON `(SenderAdminId)`

#### `DeadLetterCommand`
Holds permanently failed commands that exceeded the maximum retry count.
- `CommandId` (TEXT PRIMARY KEY NOT NULL)
- `OriginalAction` (TEXT NOT NULL)
- `FailureReason` (TEXT NOT NULL)
- `RetryCount` (INTEGER NOT NULL)
- `CreatedAt` (TEXT NOT NULL)
- `MovedAt` (TEXT NOT NULL)

#### `AuditEntry`
Represents the local administrative audit trace. Every transaction is appended to this table with cryptographic validation.
- `AuditId` (TEXT PRIMARY KEY NOT NULL)
- `CorrelationId` (TEXT NOT NULL)
- `EventType` (TEXT NOT NULL)
- `CommandId` (TEXT NOT NULL)
- `Timestamp` (TEXT NOT NULL)
- `Details` (TEXT NOT NULL)
- `PreviousHash` (TEXT NOT NULL)
- `CurrentHash` (TEXT NOT NULL)

---

## 3. Services and Core Implementations

### 3.1 Secure Local Database Service (`LocalDatabaseService`)
Implements `ILocalDatabaseService`.
- Manages connection creation lifecycles, returning connection objects cleanly.
- Executes `PRAGMA journal_mode=WAL;` to activate Write-Ahead Logging for high-concurrency read/write operations.
- Triggers automatic self-healing database recovery: if corruption is detected during initialization, the corrupt file is safely backed up with timestamp suffixes, and a clean, healthy database is reconstructed on-the-fly.

### 3.2 Database Migration Service (`DatabaseMigrationService`)
Implements `IDatabaseMigrationService`.
- Manages schema upgrades within active transactions.
- Provides transaction-safety, ensuring that if any migration step fails, the entire transaction is rolled back, preventing half-applied schema states.

### 3.3 Remote Command Repository (`RemoteCommandRepository`)
Implements `IRemoteCommandRepository`.
- Executes fully async, parameterized SQL statements, entirely eliminating SQL Injection risks.
- Manages state transition updates, automatically mapping `StartedAt`, `CompletedAt`, and calculating `ExecutionDurationMs` on completion.

### 3.4 Offline Command Queue (`OfflineCommandQueue`)
Implements `IOfflineCommandQueue`.
- Stores commands locally when connection states are interrupted.
- Restores all `PENDING` commands upon application startup, preserving order-of-receipt execution.
- Restores and rescheduling interrupted `EXECUTING` commands left behind by crashes or power losses.

### 3.5 Command Retry Worker (`CommandRetryWorker`)
Supervised background worker service.
- Periodically scans the repository for `FAILED` commands.
- Implements exponential backoff delay based on the retry attempt:
  - Retry 1: **5 seconds**
  - Retry 2: **30 seconds**
  - Retry 3: **5 minutes**
  - Retry 4: **30 minutes**
- Allows configurable maximum retry limit (defaulting to 4 attempts).
- When a command exceeds the maximum retry limit, it is routed to the `DeadLetterQueue`.

### 3.6 Dead Letter Queue (`DeadLetterQueue`)
Implements `IDeadLetterQueue`.
- Permanently relocates failed commands to separate secure storage table (`DeadLetterCommand`).
- Updates history status to `FAILED_DLQ` to make sure they do not re-execute automatically, keeping them safely available for administrative diagnostics.

### 3.7 Audit Service (`AuditService`)
Implements `IAuditService`.
- Appends high-grade secure audit records for received commands, security pipeline validation results, execution starts, completions, and failures.
- **Append-Only Protection**: Each record contains `PreviousHash` and `CurrentHash = SHA256(CurrentRecord + PreviousHash)`.
- **Tamper Detection**: Incorporates a cryptographic verification loop traversing the trail from genesis to current to ensure no records have been altered, added, or deleted.

---

## 4. Integration with Stage 1 Remote Command Engine

We successfully updated `RemoteCommandEngine.cs` to integrate Stage 2 persistence:
- On manual/network queue triggers, the command is persisted with status `PENDING` and recorded in the audit trail.
- Upon dequeuing a command, its state is changed to `EXECUTING` in database, and the audit records execution start.
- On success, it is updated to `COMPLETED` and audited.
- On failure, it is updated to `FAILED` with details and audited.
- On engine startup, `OfflineCommandQueue` automatically restores and queues all pending/interrupted commands before processing begins.

---

## 5. Security & Reliability Highlights

- **Zero Plaintext Secrets**: No passwords, private keys, or sensitive payloads are written to database files or logged to the filesystem.
- **SQL Injection Prevention**: All queries across repositories are fully parameterized.
- **Database Corruption Defense**: Implements self-healing DB recovery, quarantining bad files and recovering schema instantly.
- **Transaction Safety**: All database insertions, status updates, and DLQ movements execute within active SQLite transactions.
- **Zero-Loss Guarantees**: Interrupted executing commands from application crashes/power failures are fully recovered on next startup.

---

## 6. Known Limitations

- **Platform-Specific DPAPI**: Non-Windows execution environments use a strong, deterministic fallback key instead of DPAPI. Authenticode signature checks are bypassed under Linux environments.
- **WMI Nic ACPI Integration**: Wake-on-LAN remains a descriptive placeholder regarding platform integration under Windows kernel drivers in Stage 1 Handlers.

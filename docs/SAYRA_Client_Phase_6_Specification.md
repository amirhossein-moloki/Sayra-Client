# SAYRA Enterprise Windows Client: Phase 6 — Enterprise Update & Deployment Platform
## Official Architectural Specification Document

**Author:** Principal Enterprise Systems Architect, DevOps Architect, Windows Deployment Architect, and Technical Specification Writer
**Target Platform:** SAYRA Enterprise Windows Client (Phase 6)
**Status:** Approved & Released
**Classification:** Enterprise Confidential — Technical Reference Architecture

---

## 1 Executive Summary

### 1.1 Document Purpose
This document serves as the official Enterprise Architectural Specification and single source of truth for **Phase 6 — Enterprise Update & Deployment Platform** of the SAYRA Enterprise Windows Client. It establishes a rigorous, comprehensive blueprint defining every required subsystem, domain model, interface, data flow, security control, and deployment policy.

This specification is engineered to be exhaustive, technical, and complete, ensuring that human engineering teams or advanced AI implementation agents can develop, test, and deploy the Phase 6 platform to production specifications with zero ambient ambiguity or need for architectural clarification.

### 1.2 Context & Mission
The SAYRA platform operates across thousands of physical gaming center workstations distributed over multi-branch physical environments. Operating in these high-density, public environments presents severe operational challenges: unstable local WAN or LAN connectivity, limited or costly internet bandwidth, and hostile local environments where technically skilled end-users attempt to bypass client security.

To maintain 99.99% operational availability and guarantee immediate security hot-fixing, the client requires a robust, secure, resilient, and fully automated remote update mechanism. The platform must perform background updates seamlessly, resume interrupted downloads without bandwidth waste, apply differential binary patches, enforce cryptographic trust, and roll back gracefully in the event of any deployment failure.

### 1.3 Business & Enterprise Goals
*   **Zero Operational Downtime:** Maximize workstation uptime by performing updates silently in the background, utilizing scheduled maintenance windows, and avoiding intrusive visual prompts during gaming sessions.
*   **Staged and Resilient Deployments:** Mitigate risk via phased rollouts, canary groups, and ring-based deployment policies, preventing broad fleet failures due to unseen hardware or software incompatibilities.
*   **Bandwidth Cost Optimization:** Minimize bandwidth consumption across thousands of workstations via binary delta updates, chunked caching, local local-network mirrors, and dynamic bandwidth limiting.
*   **Zero Data and State Loss:** Ensure that system configurations, local user session timers, offline transaction queues, and diagnostic log history are fully preserved across version migrations.

### 1.4 Technical Goals
*   **Asynchronous, Non-Blocking Operations:** Design update checks, downloads, and package verification as asynchronous background services that do not degrade CPU or GPU performance during active gaming sessions.
*   **Robust Cryptographic Enforcement:** Validate the integrity and origin of all update manifests, packages, and delta binaries using RSA/ECDSA digital signatures, certificate pinning, and hash verification prior to execution.
*   **Transactional Installation and Rollback:** Implement atomic, transaction-safe installation routines that back up existing states, execute silent upgrades, and automatically restore the preceding stable state in the event of installation, process, or system power failures.
*   **Clean Architecture Adherence:** Maintain strict separation of concerns among the presentation (WPF UI), application coordination, infrastructure interaction, and external Windows API management layers.

---

## 2 Phase Scope

### 2.1 Included in Phase 6
The Phase 6 scope covers the following major technical domains:
1.  **Update Manager:** The central coordinator managing state machine transitions, scheduling, recovery, and version alignment.
2.  **Package Manager:** The specification of the SAYRA secure package format (`.spk`), including signed JSON manifests, chunk arrays, and dependency chains.
3.  **Version Manager:** Strict Semantic Versioning (SemVer 2.0.0), version compatibility matrices, minimum supported baselines, and forced upgrade configurations.
4.  **Update Scheduler:** Dynamic scheduling systems utilizing randomized jitter, manual administrator bypasses, maintenance windows, and localized bandwidth policies.
5.  **Download Manager:** Resumable chunked file downloader with parallel pipelines, backoff retry logic, bandwidth constraints, and CDN mirror selection.
6.  **Package Verification Engine:** Cryptographic verification engine performing certificate pinning, RSA signature validation, and chunk-by-chunk SHA-256 validation.
7.  **Installer Engine:** Silent upgrade coordinator orchestrating the termination of WPF shells and Session 0 services, deploying dependencies, and utilizing the Windows Restart Manager.
8.  **Rollback Engine:** An atomic recovery subsystem managing pre-installation system snapshots, registry backups, database state protection, and immediate rollback triggers.
9.  **Delta Update Engine:** A binary-differential patching engine applying highly optimized byte diffs to reduce bandwidth overhead.
10. **Deployment Policies:** Centralized configuration mapping canary rollouts, deployment rings, staged percentages, pilot configurations, and emergency pause overrides.
11. **Update Cache & History:** Local storage manager handling cache rotation, file retention, local database transaction history logging, and telemetry uploads.

### 2.2 Excluded from Phase 6
*   **P2P Peer Distribution (Phase 7):** Local workstation-to-workstation LAN peer-to-peer patch distribution. (Phase 6 strictly utilizes centralized CDN/Server download pipelines).
*   **Direct Hardware Firmware Flashing (Phase 8):** Updating physical motherboard BIOS, GPU firmware, or custom router firmware.
*   **Dynamic Remote Desktop Interop (Phase 8):** Direct real-time screen sharing and remote input redirection.

### 2.3 Dependencies on Previous Phases
*   **Phase 1 Foundation:** Relies on the .NET 8 Windows Service host architecture and the SCM integration framework.
*   **Phase 2 Configuration:** Utilizes the local SQLCipher database structure and JSON synchronization coordinators to store local update state and history logs.
*   **Phase 3 Security:** Enforces DPAPI-secured local storage secrets, named pipe IPC security ACLs, code signing validation, and tamper-detection triggers.

### 2.4 Success Criteria
1.  **Zero-Loss Version Migration:** All local databases, configurations, and offline logs migrate with 100% data integrity during version upgrades.
2.  **Zero-Bypass Validation:** 100% of tampered, unsigned, or corrupt update files are detected and rejected prior to execution.
3.  **Graceful Auto-Rollback:** If an installation fails, crashes, or is interrupted (including by hard power failure), the workstation must recover and return to the fully functional preceding version within 60 seconds of reboot.
4.  **Low Performance Footprint:** Background downloads and checks must consume $<0.5\%$ CPU and $<30\text{MB}$ RAM, causing zero visual FPS drop in games.

---

## 3 High-Level Architecture

### 3.1 Overall Update Architecture
The SAYRA Enterprise Update Platform is divided between elevated operations running within the Windows Service (Session 0) and low-privilege user presentation elements (Session 1+). A dedicated `Sayra.Client.Updater` standalone utility is deployed to perform file swapping, process stopping, and service restarts without introducing process lockups.

```
                                 ┌────────────────────────────────────────────────────────┐
                                 │                 SAYRA UPDATE SERVICE                   │
                                 │                 (Central Server / CDN)                 │
                                 └───────────────────────────┬────────────────────────────┘
                                                             │
                                       Secure HTTPS TLS 1.3  │ (Signed Manifests, Delta Packages)
                                                             ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ WINDOWS SERVICE HOST (Session 0 - NT AUTHORITY\SYSTEM)                                                                │
│                                                                                                                       │
│  ┌───────────────────────┐       ┌───────────────────────┐       ┌───────────────────────┐       ┌─────────────────┐  │
│  │     UpdateManager     │       │    DownloadManager    │       │  PackageVerification  │       │   UpdateCache   │  │
│  │ - Orchestrates State  ├──────►│ - Chunked Download    ├──────►│ - RSA/ECDSA Validation├──────►│ - Encrypted SPK │  │
│  │ - Evaluates Policies  │       │ - Resumable Streams   │       │ - SHA-256 Hash Checks │       │ - Auto-Cleanup  │  │
│  └──────────┬────────────┘       └───────────────────────┘       └───────────────────────┘       └─────────────────┘  │
│             │                                                                                                         │
│             │ Launches elevated                                                                                       │
│             ▼                                                                                                         │
│  ┌─────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐  │
│  │                                          SAYRA.CLIENT.UPDATER.EXE                                               │  │
│  │ - Standalone helper run as SYSTEM                                                                               │  │
│  │ - Interacts with Windows SCM to Stop/Start SayraClient                                                          │  │
│  │ - Restores physical backups on rollback triggers                                                                │  │
│  └─────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘  │
└─────────────┬─────────────────────────────────────────────────────────────────────────────────────────────────────────┘
              │
              │ IPC Named Pipe Bridge (\\.\pipe\SayraClientIpcPipe)
              ▼
┌───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ INTERACTIVE WPF UI CLIENT (Session 1+ - Low Privilege User Context)                                                    │
│                                                                                                                       │
│  ┌──────────────────────────────────────────────────────┐       ┌──────────────────────────────────────────────────┐  │
│  │                   WPF SHELL UI                       │       │               UPDATE VISUAL DIALOGS              │  │
│  │  - Monitors active gaming session focus               │       │  - Optional maintenance warning display          │  │
│  │  - Communicates UI state locks over IPC             │       │  - Restricts manual actions during updates       │  │
│  └──────────────────────────────────────────────────────┘       └──────────────────────────────────────────────────┘  │
└───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.2 Deployment Pipeline & Trust Boundaries
1.  **Publishing Boundary:** Developers sign compilation outputs using an Authenticode code-signing certificate and pack files into a secure SAYRA package (`.spk`).
2.  **Server Hosting Boundary:** Packages are hosted on secure HTTPS repositories. A signed JSON manifest governs rings and stages.
3.  **Workstation Network Boundary:** Client requests manifests via HTTPS TLS 1.3 with pinned certificates.
4.  **Local Isolation Boundary:** The update coordinator (Session 0) downloads, hashes, and validates files, ensuring only fully verified packages are mounted.
5.  **Installation Boundary:** The client service initiates `Sayra.Client.Updater` inside the `NT AUTHORITY\SYSTEM` context to safely modify the locked system files.

### 3.3 Package Flow
```
[Manifest Checked] ──► [Verify Signature] ──► [Compute Delta or Full]
                                                    │
             ┌──────────────────────────────────────┴─────────────────────────────────────┐
             ▼ (Delta Available)                                                          ▼ (Full Only)
     [Download Byte Diff]                                                         [Download Complete Chunk Array]
             │                                                                            │
             ▼                                                                            ▼
     [Apply Patch to Core]                                                        [Verify Hash of SPK Archive]
             │                                                                            │
             └──────────────────────────────────────┬─────────────────────────────────────┘
                                                    ▼
                                      [Validate Final File Signatures]
                                                    │
                                                    ▼
                                      [Execute Silent Swapping Engine]
```

### 3.4 Rollback Flow
1.  **Failure Event:** The installer engine detects a file write exception, service start failure, or runtime process crash.
2.  **Signal Capture:** The Standalone Updater intercepts the non-zero exit code or timeout.
3.  **Restoration:** The Updater stops the service, deletes newly extracted files, restores directory snapshots from the `Backup/` folder, and restarts the service on the previous version.
4.  **Reporting:** Upon startup, the restored system registers the rollback event, logs details in the local SQLCipher history database, and transmits telemetry to the central server.

---

## 4 Update Components

### 4.1 Update Manager
*   **Responsibilities:** Acts as the high-level orchestration engine of the update platform. It maintains the system update state machine, evaluates deployment policies, schedules maintenance checks, and initiates the recovery/rollback workflows.
*   **Lifecycle:** Boots immediately with the Windows Service Host. It registers hooks into network state change listeners to evaluate update checks upon connectivity restoration.
*   **Scheduling:** Evaluates cron-based update schedules and maintenance windows configured in `client_config.json`.
*   **State Machine:** Enforces a strict, atomic, and thread-safe state machine:
    *   `Idle` $\rightarrow$ `CheckingForUpdate` $\rightarrow$ `Downloading` $\rightarrow$ `Verifying` $\rightarrow$ `Staged` $\rightarrow$ `Installing` $\rightarrow$ `Restarting` $\rightarrow$ `RollingBack` (if failed) $\rightarrow$ `Completed`.
*   **Recovery:** Intercepts system shutdowns, service crashes, or unexpected power cuts. It maintains state in the local SQLCipher database to resume operations safely from the last verified state upon system reboot.

### 4.2 Package Manager
*   **Package Format (`.spk`):** A custom, uncompressed archive containing a structured manifest, individual file streams, and cryptographic metadata.
*   **Metadata:** Embedded JSON header detailing package GUID, version, target system architecture, package byte size, and timestamp.
*   **Manifest (`manifest.json`):** Holds a comprehensive list of all files, their target relative paths, file permission flags, and individual SHA-256 hashes.
*   **Dependencies:** Specifies prerequisite packages, runtime requirements (such as .NET runtime versions, desktop runtimes), and target database schema version baselines.
*   **Integrity:** Asserts package integrity using a trailer section containing an RSA/ECDSA digital signature over the entire file block preceding the trailer.
*   **Version Compatibility:** Validates that the package matches the workstation hardware architecture (x64) and operating system requirements (Windows 10/11 build requirements).

### 4.3 Version Manager
*   **Semantic Versioning:** Enforces strict compliance with SemVer 2.0.0 (e.g., `MAJOR.MINOR.PATCH-PRERELEASE+BUILD`).
*   **Compatibility Matrix:** Maintained via server-side definitions. Maps client versions against database schema levels to prevent breaking configurations from being loaded.
*   **Minimum Supported Version:** The manifest specifies the absolute minimum client version permitted to contact the server. Workstations running versions below this threshold bypass typical rollouts and are funneled into forced immediate upgrades.
*   **Forced Upgrade:** A flag inside the update manifest that overrides maintenance windows, active game sessions, and local bandwidth throttles, executing a silent immediate installation to apply critical security patches.
*   **Downgrade Rules:** Explicitly blocked by default to prevent replay attacks where an attacker attempts to force a workstation to downgrade to a version containing known vulnerabilities. Downgrades are permitted exclusively via a manually signed administrator override token.

### 4.4 Update Scheduler
*   **Automatic Updates:** Standard background check executed periodically based on configuration-defined intervals.
*   **Manual Updates:** Supports administrative bypasses triggered via the TCP command channel, initiating immediate manifest checks.
*   **Maintenance Windows:** Restricts visual disruptions and service restarts to non-peak hours (e.g., `03:00 - 05:00` local time), evaluating workstation occupancy prior to running updates.
*   **Delayed Updates:** Allows non-critical updates to be deferred by a set number of days based on local deployment policies.
*   **Randomized Jitter Scheduling:** Introduces randomized offsets (e.g., $\pm 1200$ seconds) to the scheduled check times to prevent thousands of workstations from flooding CDN servers simultaneously.
*   **Bandwidth Awareness:** Automatically limits download operations to low-occupancy windows or dynamically halts background tasks if workstation network utilization exceeds configured thresholds.

### 4.5 Download Manager
*   **Chunked Downloads:** Splits the update package into standard 1MB chunks, facilitating easy tracking and resumption.
*   **Resume Support:** Checks local temporary cache files. Downloads resume from the last fully completed chunk, avoiding redundant downloads of large files after connection losses.
*   **Retry Logic:** Uses an exponential backoff routine with added random jitter if chunk requests fail.
*   **Parallel Downloads:** Spawns up to 4 concurrent HTTP/2 streams to accelerate download speeds on high-bandwidth networks.
*   **Bandwidth Limiting:** Enforces dynamic throttling using token-bucket rate limiters configured via deployment configurations (e.g., capping update download speeds at 512 KB/s during peak operational hours).
*   **Mirror Selection:** Computes server latency and network distance metrics to route requests to the nearest CDN node or local LAN caching server.

### 4.6 Package Verification
*   **Digital Signature Validation:** Validates the signature of the update package envelope against the pre-cached `server_public.key` using ECDSA-P384.
*   **Hash Verification:** Computes the SHA-256 hash of each extracted file block and verifies it against the manifest specification prior to execution.
*   **Certificate Validation:** Checks the authenticity of the code-signing certificate, validating certificate revocation lists (CRLs) and ensuring the root authority is trusted.
*   **Tamper Detection:** Instantly aborts the update process and locks the workstation if any file hash mismatch is detected or if signature bytes are stripped from files.
*   **Manifest Verification:** Validates that the manifest’s inner signature matches the outer archive signature block, protecting against payload swap attacks.

### 4.7 Installer Engine
*   **Silent Installation:** Runs entirely inside the background service context with no user-facing interactive prompt dialogs.
*   **Interactive Installation:** Provides optional overlay progress bars on the WPF shell during major, non-silent maintenance phases.
*   **Service Updates:** Orchestered by spawning the independent `Sayra.Client.Updater` executable. The helper stops the primary Windows service, replaces core service binaries, and restarts the service.
*   **WPF Updates:** Updates visual UI client binaries by sending a shutdown IPC command to the active WPF shell, overwriting binaries in the `UI/` folder, and re-spawning the shell inside the active user session context.
*   **Dependency Installation:** Automates silent installation of Microsoft VC++ redistributables or .NET runtime updates using standard silent command-line flags (e.g., `/quiet /norestart`).
*   **Restart Requirements:** Leverages the Windows Restart Manager API to release file locks on active DLLs, minimizing the need for full workstation operating system reboots.

### 4.8 Rollback Engine
*   **Automatic Rollback:** Instantly triggered if the installer engine encounters write access errors, service initialization timeouts, or if the newly launched client crashes within a 5-minute diagnostic window.
*   **Manual Rollback:** Triggered by administrators via signed secure command frames to revert problematic deployments.
*   **Snapshot Strategy:** Prior to file modifications, the installer copies all active executables, DLLs, and key configurations to a timestamped `Snapshots/` subfolder.
*   **Backup Strategy:** Preserves database configurations and local user session states. Local database files (`.db`) are backed up using transactional SQLite backup APIs.
*   **Rollback Validation:** Evaluates system sanity after a rollback operation, ensuring the preceding client starts successfully and restores communication to the central server.
*   **Failure Recovery:** If both the update and the rollback routines fail, the workstation transitions to hard security lockdown and alerts local administrators via local physical sirens or UDP broadcasts.

### 4.9 Delta Update Engine
*   **Binary Delta Updates:** Uses custom byte-level differencing algorithms (VCDIFF format) to calculate differences between the active local binary and the target release binary, minimizing download size for minor changes.
*   **Full Package Updates:** Fallback mechanism automatically executed if the local client version is too old to match the server's pre-compiled delta matrices, or if any delta patch file fails local integrity verification.
*   **Patch Validation:** Computes the SHA-256 hash of the reconstructed binary post-patch application. The result must match the hash listed in the server's update manifest exactly.
*   **Patch Recovery:** If the patched file is corrupt, the Delta engine purges the file, discards delta files, and schedules a full package download.
*   **Patch Compatibility:** Ensures that target files are not in use during patch application by leveraging the local service shutdown pipelines.

### 4.10 Deployment Policies
*   **Ring Deployment:** Organizes the workstation fleet into progressive deployment rings (e.g., `Ring 0: Test`, `Ring 1: Canary`, `Ring 2: General Office`, `Ring 3: Full Production`).
*   **Canary Releases:** Targets minor pools of workstations (e.g., 1%) to monitor telemetry and error rates before expanding rollouts.
*   **Staged Rollout:** Gradually scales deployment exposure (e.g., 10% $\rightarrow$ 25% $\rightarrow$ 50% $\rightarrow$ 100%) over several days.
*   **Pilot Groups:** Restricts initial testing to dedicated physical zones or offices within a gaming center.
*   **Emergency Deployment:** Overrides ring constraints, staging, and maintenance windows to push critical security hotfixes to 100% of the fleet.
*   **Forced Deployment:** Restricts workstation use entirely until the mandatory security update has successfully completed.
*   **Paused Deployment:** Allows administrators to pause active rollouts instantly via the TCP dashboard if unexpected anomalies are reported in canary telemetry.

### 4.11 Update Cache
*   **Local Cache:** Preserves downloaded chunk files and `.spk` files inside a secure `%ProgramData%\SAYRA_Client\UpdateCache` folder.
*   **Cleanup Policy:** Deletes downloaded packages and chunks once the installation completes and passes the 5-minute diagnostic verification window.
*   **Retention:** Keeps the single most recent working `.spk` package to support rollback recovery scenarios without redownloading files.
*   **Disk Limits:** Implements a strict 1GB storage ceiling on cache folders, automatically purging oldest files to prevent disk exhaustion.
*   **Cache Validation:** Performs SHA-256 checks on cached packages prior to reuse to detect local disk rot or tampering.

### 4.12 Update History
*   **Audit Trail:** Records all update lifecycle events inside an encrypted SQLCipher table (`UpdateHistory`).
*   **Installation History:** Logs structural details: `VersionCode`, `InstallTime`, `Duration`, `RegistryUpdated`, and `ExitStatus`.
*   **Rollback History:** Captures details on rollback triggers: error messages, target file exceptions, and restoration status.
*   **Failure History:** Logs details of failed downloads, corrupted manifest signatures, and network timeouts.
*   **Telemetry:** Automatically packages and uploads update history logs to the server to support central dashboard monitoring.

---

## 5 Domain Models

### 5.1 Update Package Model (`UpdatePackage`)
*   **Purpose:** Captures update metadata, cryptographic signatures, chunk layouts, and target architectures.
*   **Fields:**
    *   `PackageId` (Guid, Required): Unique package identifier.
    *   `TargetVersion` (String, Required): Target Semantic Versioning string.
    *   `TargetArchitecture` (Enum `SystemArchitecture`, Required): `X64`, `ARM64`.
    *   `MinSourceVersion` (String, Required): Minimum baseline version required for delta application.
    *   `TotalSizeBytes` (Long, Required): Total byte size of the package.
    *   `PackageHash` (String, Required): SHA-256 checksum of the compiled package.
    *   `Signature` (String, Required): RSA/ECDSA signature computed over the file block using the private key.
    *   `IsDelta` (Boolean, Required): True if package contains binary delta diffs.
    *   `Chunks` (List<ChunkMetadata>, Required): Meta details for all chunk divisions.
*   **Validation:**
    *   `PackageId` must not be empty.
    *   `TotalSizeBytes` must be greater than 0.
    *   `Signature` must match the server's public key fingerprint.
*   **Lifecycle:** `Created` $\rightarrow$ `Discovered` $\rightarrow$ `Downloading` $\rightarrow$ `Verified` $\rightarrow$ `Applied` $\rightarrow$ `Archived` $\rightarrow$ `Purged`.

```json
{
  "PackageId": "3b2a5c9d-d113-44f6-a83a-491295fc6e02",
  "TargetVersion": "2.4.0",
  "TargetArchitecture": "X64",
  "MinSourceVersion": "2.3.0",
  "TotalSizeBytes": 25165824,
  "PackageHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "Signature": "3045022100a35f7cde9a31b4528c...",
  "IsDelta": true,
  "Chunks": [
    {
      "Index": 0,
      "SizeBytes": 1048576,
      "Sha256Checksum": "a1b2c3d4..."
    }
  ]
}
```

---

### 5.2 Update Manifest Model (`UpdateManifest`)
*   **Purpose:** Governs staged rollouts, deployment policies, minimum allowed versions, and Ring parameters.
*   **Fields:**
    *   `ManifestId` (Guid, Required): Globally unique identifier.
    *   `Publisher` (String, Required): Standard publisher identity string.
    *   `ReleaseDate` (DateTime, Required): UTC release timestamp.
    *   `LatestVersion` (String, Required): Highest semantic version available.
    *   `MinRequiredVersion` (String, Required): Absolute minimum version required to contact the server.
    *   `IsForcedUpgrade` (Boolean, Required): True to override maintenance windows.
    *   `Rings` (Dictionary<String, RingPolicy>, Required): Deployment Ring specifications.
    *   `ManifestSignature` (String, Required): SHA-256 signature of manifest data.
*   **Validation:**
    *   `ManifestSignature` must validate against `server_public.key`.
    *   `LatestVersion` must be greater than or equal to `MinRequiredVersion`.

```json
{
  "ManifestId": "d3b07384-d113-44f6-a83a-491295fc6e02",
  "Publisher": "SAYRA Enterprise",
  "ReleaseDate": "2026-10-25T14:32:00Z",
  "LatestVersion": "2.4.0",
  "MinRequiredVersion": "2.1.0",
  "IsForcedUpgrade": false,
  "Rings": {
    "Canary": {
      "TargetVersion": "2.4.0",
      "RolloutPercentage": 10,
      "PauseExecution": false
    },
    "Production": {
      "TargetVersion": "2.3.5",
      "RolloutPercentage": 100,
      "PauseExecution": false
    }
  },
  "ManifestSignature": "8f3b2a5c..."
}
```

---

### 5.3 Rollback Event Model (`RollbackEvent`)
*   **Purpose:** Logs historical rollback triggers, failure causes, and file restoration logs.
*   **Fields:**
    *   `RollbackId` (Guid, Required): Unique rollback record identifier.
    *   `TriggeredAt` (DateTime, Required): UTC timestamp of rollback execution.
    *   `FailedVersion` (String, Required): Version that triggered the failure.
    *   `RestoredVersion` (String, Required): Target safe version restored.
    *   `FailureReason` (String, Required): Detailed error trace or system log.
    *   `RestoredFilesCount` (Int, Required): Number of physical files replaced.
    *   `RestoredDatabases` (List<String>, Required): SQLite databases rolled back.
    *   `Status` (Enum `RollbackStatus`, Required): `PENDING`, `SUCCESSFUL`, `FAILED`.
*   **Validation:**
    *   `FailedVersion` and `RestoredVersion` must be valid SemVer strings.
    *   `Status` must evaluate to `SUCCESSFUL` for the workstation to resume normal operation.

```json
{
  "RollbackId": "9a751db5-12cf-4b77-a5eb-0676451e0892",
  "TriggeredAt": "2026-10-25T14:35:05Z",
  "FailedVersion": "2.4.0",
  "RestoredVersion": "2.3.5",
  "FailureReason": "TimeoutException: SayraClient service failed to reach Running state within 30 seconds of update.",
  "RestoredFilesCount": 142,
  "RestoredDatabases": ["offline_queue.db"],
  "Status": "SUCCESSFUL"
}
```

---

## 6 Interfaces

### 6.1 `IUpdateManager`
*   **Responsibilities:** Orchestrates the update lifecycle, maintains state transitions, schedules background tasks, and manages recovery workflows.
*   **Lifetime:** Singleton registered inside the primary .NET Host.
*   **Thread Safety:** Thread-safe execution enforced via `SemaphoreSlim` blocks.

```csharp
namespace Sayra.Client.Shared.Interfaces.Updates
{
    public interface IUpdateManager
    {
        UpdateState CurrentState { get; }
        Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken);
        Task<bool> StartUpdateAsync(UpdateManifest manifest, CancellationToken cancellationToken);
        Task CancelUpdateAsync();
        Task RegisterStateAsync(UpdateState newState, CancellationToken cancellationToken);
    }
}
```

---

### 6.2 `IDownloadManager`
*   **Responsibilities:** Manages parallel chunked HTTP downloads, throttling constraints, CDN mirrors, and file stream caching.
*   **Lifetime:** Singleton.
*   **Thread Safety:** Reentrant download tasks, thread-safe chunk assemblies.

```csharp
namespace Sayra.Client.Shared.Interfaces.Updates
{
    public interface IDownloadManager
    {
        Task<DownloadResult> DownloadPackageAsync(UpdatePackage package, IProgress<double> progress, CancellationToken cancellationToken);
        Task PauseDownloadAsync();
        Task ResumeDownloadAsync();
        void ConfigureBandwidthThrottle(long bytesPerSecond);
    }
}
```

---

### 6.3 `IPackageVerifier`
*   **Responsibilities:** Enforces cryptographic validation over manifests, SPK archives, chunk structures, and Authenticode signatures.
*   **Lifetime:** Transient.
*   **Thread Safety:** Stateless, thread-safe verification functions.

```csharp
namespace Sayra.Client.Shared.Interfaces.Updates
{
    public interface IPackageVerifier
    {
        Task<bool> VerifyManifestSignatureAsync(UpdateManifest manifest, CancellationToken cancellationToken);
        Task<bool> VerifyPackageIntegrityAsync(string packagePath, UpdatePackage packageMetadata, CancellationToken cancellationToken);
        Task<bool> VerifyFileSignatureAsync(string filePath, string expectedSignature, CancellationToken cancellationToken);
    }
}
```

---

### 6.4 `IInstallerEngine`
*   **Responsibilities:** Manages silent system upgrades, services shutdown pipelines, dependency injection, and process launches.
*   **Lifetime:** Singleton.
*   **Thread Safety:** Singleton execution lock prevents concurrent installation routines.

```csharp
namespace Sayra.Client.Shared.Interfaces.Updates
{
    public interface IInstallerEngine
    {
        Task<InstallationResult> InstallUpdateAsync(string packagePath, CancellationToken cancellationToken);
        Task<bool> RegisterRestartRequirementsAsync(string[] processPaths);
    }
}
```

---

### 6.5 `IRollbackEngine`
*   **Responsibilities:** Oversees directory snapshots, transactional database protection, and automated restore operations on installation failure.
*   **Lifetime:** Singleton.
*   **Thread Safety:** Single-threaded execution context managed by the standalone updater process.

```csharp
namespace Sayra.Client.Shared.Interfaces.Updates
{
    public interface IRollbackEngine
    {
        Task<bool> CreateSnapshotAsync(string snapshotId, CancellationToken cancellationToken);
        Task<bool> ExecuteRollbackAsync(string snapshotId, string failureReason, CancellationToken cancellationToken);
        Task<bool> ValidateRollbackSucceededAsync(string snapshotId, CancellationToken cancellationToken);
    }
}
```

---

### 6.6 `IDeltaUpdateEngine`
*   **Responsibilities:** Generates, validates, and applies binary VCDIFF patch arrays onto existing binaries.
*   **Lifetime:** Transient.
*   **Thread Safety:** Thread-safe, stateless delta parsing.

```csharp
namespace Sayra.Client.Shared.Interfaces.Updates
{
    public interface IDeltaUpdateEngine
    {
        Task<bool> ApplyDeltaPatchAsync(string baseFilePath, string patchFilePath, string outputFilePath, CancellationToken cancellationToken);
        Task<bool> VerifyPatchedFileAsync(string outputFilePath, string expectedSha256, CancellationToken cancellationToken);
    }
}
```

---

## 7 Update Flows

### 7.1 Complete Happy Path Update Flow
```
[Background Poller] ──► Checks Manifest ──► Verified Signature ──► Delta Download ──► Applied Patches
                                                                                              │
                                                                                              ▼
[Silent Swapping Engine] ◄── WPF UI Exits ◄── Service Stops ◄── Snapshot Taken ◄── Verified Final SHA
           │
           ▼
[SCM Starts Service] ──► [WPF Relaunches] ──► [Self-Diagnostics Pass] ──► [State Completed]
```

### 7.2 Power Failure & Interrupted Download Recovery Flow
```
[Downloader Active] ──► [Workstation Sudden Power Cut]
                                │
                                ▼ (System Reboots)
[Service Host Initializes UpdateManager]
                                │
                                ▼ (Reads State Machine from SQLCipher DB)
[Detects Incomplete Download State]
                                │
                                ▼
[Verifies Local Cache Chunks] ──► Resumes Download from Chunk Index 34 ──► Happy Path Resumes
```

### 7.3 Corrupted Package Detection Flow
1.  **Detection:** Download completes. `PackageVerification` computes the SHA-256 hash of the `.spk` package.
2.  **Anomaly:** The computed hash does not match the manifest checksum, or signature validation against `server_public.key` fails.
3.  **Containment:** The update coordinator quarantines the file, purges the temporary download cache folder, and transitions to `Idle` state.
4.  **Reporting:** Logs a warning to the Windows Event Log and registers a security tamper alert in the local history table.

### 7.4 Failed Installation Recovery Flow
```
[Standalone Updater runs as SYSTEM] ──► [Applies extracted files to target directory]
                                                      │
                                                      ▼ (Write Exception / File Locked)
                                           [Installation Aborted]
                                                      │
                                                      ▼
                                       [IRollbackEngine Triggered]
                                                      │
                                                      ▼
                                   [Deletes files applied during update]
                                                      │
                                                      ▼
                                [Extracts Snapshot folder back to active directory]
                                                      │
                                                      ▼
                                            [Restores SQLCipher DB]
                                                      │
                                                      ▼
                                        [SCM Starts Previous Service]
                                                      │
                                                      ▼
                                    [Previous Version Online & Telemetry Sent]
```

---

## 8 Reliability & Resilience

### 8.1 Retry Strategy & Backoff Jitter
All network and API transactions utilize exponential backoff retries paired with randomized jitter to prevent server congestion. This is defined by:
$$T_{\text{delay}} = 2^{\text{attempt}} \times \text{BaseDelaySeconds} + \text{JitterSeconds}$$
Maximum wait thresholds are capped at 300 seconds to ensure consistent connection monitoring.

### 8.2 Circuit Breakers
The update coordinator encapsulates HTTP calls within a circuit breaker. If 5 consecutive socket timeouts or CDN HTTP 5xx errors occur, the circuit transitions to `Open` state for 15 minutes, halting further network operations to conserve system resources.

### 8.3 Disk Full Recovery
Prior to starting a download, the client computes the space required:
$$\text{FreeSpaceRequired} = \text{PackageSize} \times 2.5 + \text{SnapshotSize}$$
If the target drive has insufficient storage, the download is halted. The update cache is cleared, and an alert is logged to notify administrators.

### 8.4 Rollback Guarantees
*   **Transaction Safety:** Directory swapping utilizes transactional file operations (`ReplaceFile` Win32 API), ensuring that the file copy is atomic.
*   **Self-Diagnostics Watchdog:** The standalone updater monitors the newly launched client process. If the service fails to respond to a named-pipe health check within 300 seconds, the rollback routine is automatically initiated.

---

## 9 Security

### 9.1 Cryptographic Chain of Trust
```
┌─────────────────────────────────┐
│     SAYRA ENTERPRISE CA         │ (Offline Root Private Key)
└───────────────┬─────────────────┘
                ▼
┌─────────────────────────────────┐
│  MANIFEST SIGNING CERTIFICATE   │ (ECDSA-P384 Server-Side Manifest Signature)
└───────────────┬─────────────────┘
                ▼
┌─────────────────────────────────┐
│        UPDATE MANIFEST          │ (Contains individual Package SHA-256 Checksums)
└───────────────┬─────────────────┘
                ▼
┌─────────────────────────────────┐
│      BINARY EXE / DLL FILES     │ (Authenticode Signed & Verified via WinVerifyTrust)
└─────────────────────────────────┘
```

### 9.2 Certificate Pinning & Replay Protection
*   All connection requests validate server certificates strictly against `server_public.key` fingerprints.
*   Incoming manifests contain monotonic sequence numbers and dynamic timestamps. Manifests older than 10 seconds from current UTC are discarded to protect against replay attacks.

### 9.3 Secure Installer execution & Least Privilege
*   The `Sayra.Client.Updater` standalone utility is stored in the protected `%ProgramFiles%\SayraClient\` directory with inheritance disabled.
*   Access Control Lists (ACLs) permit write modifications only for the `SYSTEM` account, preventing local standard users from replacing the updater binary.

---

## 10 Performance

### 10.1 System Resource Limits & Performance Profiles

| Update Phase | Max CPU Usage | Max RAM Footprint | Disk I/O Limit | Priority Class | Optimization Strategy |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Manifest Check** | $< 0.1\%$ | $< 5\text{MB}$ | Negligible | Low | Asynchronous background polling |
| **Download** | $< 1.0\%$ | $< 15\text{MB}$ | $5\text{MB/s}$ | Idle | Token-bucket rate limiting, chunk pooling |
| **Verification** | $< 5.0\%$ | $< 25\text{MB}$ | Read saturation | BelowNormal | Hardware-accelerated hashing (AES-NI) |
| **Delta Apply** | $< 10.0\%$ | $< 35\text{MB}$ | Write saturation | BelowNormal | Running VCDIFF decoding on background threads |
| **Installation** | $< 15.0\%$ | $< 40\text{MB}$ | Disk saturation | Normal | Executing during low-occupancy windows |

### 10.2 Update Window Targets
*   **Delta Patch Application:** Completed within 5 seconds of download.
*   **Silent Version Swap:** Completed within 15 seconds of service termination.
*   **Rollback Restore Operation:** Completed within 30 seconds of failure detection.

---

## 11 Windows Integration

### 11.1 Windows Installer (MSI/MSIX Strategy)
*   **Initial Setup:** Managed via a signed standard MSI package that registers the Windows service, configures registry keys, and creates default directory structures.
*   **Self-Update Pipeline:** Minor patch and version upgrades bypass the heavy MSI execution engine. They are deployed via the lightweight, high-speed `Sayra.Client.Updater` binary, avoiding OS dialog prompts.

### 11.2 Windows Service Control Manager Integration
*   The `Sayra.Client.Updater` utility communicates with SCM using the `ServiceController` class.
*   It issues a `Stop` signal, waits up to 30 seconds, and forcefully terminates any orphaned processes using Process ID lists before replacing core service files.

### 11.3 Windows Restart Manager Integration
*   The installer registers active executable files with the native Windows Restart Manager (`RmStartSession`).
*   This facilitates the release of file locks, swapping of active DLL files, and restoration of running states, preventing the need for system restarts.

---

### 11.4 Startup & Registry Integration
*   The updater creates the following registry keys within `HKLM\Software\SAYRA\Client`:

```
HKLM\Software\SAYRA\Client\
├── ActiveVersionCode     (REG_SZ)     - Current semantic version code (e.g. "2.3.5")
├── ActiveChannel         (REG_SZ)     - Active deployment channel (e.g. "Canary")
├── InstallDirectory      (REG_SZ)     - Root installation directory path
└── LastUpdateCheck       (REG_SZ)     - Timestamp of the last successful check (UTC)
```

### 11.5 Event Log Channels
All critical installer events, failures, and rollbacks are written directly to the custom Windows Event Log channel (`SAYRA_Client_Updates`).

---

## 12 Configuration

The update coordinator relies on dynamic configuration parameters managed in the encrypted `client_config.json`:

```json
{
  "UpdatePolicies": {
    "AutoCheckIntervalMinutes": 180,
    "JitterSeconds": 1200,
    "AllowedChannels": ["Canary", "Production"],
    "ActiveChannel": "Canary",
    "BypassActiveUserSession": false
  },
  "MaintenanceWindows": {
    "StartTimeUtc": "03:00:00",
    "EndTimeUtc": "05:00:00",
    "AllowForcedUpgrades": true,
    "MaxOccupancyPercentage": 5
  },
  "BandwidthPolicies": {
    "MaxDownloadRateBytesPerSecond": 1048576,
    "PeakHoursLimitRateBytesPerSecond": 262144,
    "EnableParallelDownloads": true,
    "MaxParallelConnections": 4
  },
  "RollbackPolicies": {
    "DiagnosticSuccessWindowSeconds": 300,
    "AutoRollbackOnServiceCrash": true,
    "RetainSnapshotCount": 1
  },
  "RetentionPolicies": {
    "MaxCacheSizeMegabytes": 1024,
    "PurgeUnusedPackagesDays": 14
  }
}
```

---

## 13 Storage & Cache Schemas

```
%ProgramData%\SAYRA_Client\
├── UpdateCache\                  # Temp folder for chunk downloads and .spk files
├── Snapshots\
│   └── 2.3.5_Snapshot\           # Files from the preceding working version
└── Databases\
    └── update_platform.db        # SQLCipher-encrypted history database
```

### 13.1 `UpdateHistory` SQLCipher Table Schema
```sql
CREATE TABLE IF NOT EXISTS UpdateHistory (
    Id TEXT PRIMARY KEY NOT NULL,
    TargetVersion TEXT NOT NULL,
    SourceVersion TEXT NOT NULL,
    StartTime TEXT NOT NULL,
    EndTime TEXT,
    Status TEXT NOT NULL,           -- 'STAGED', 'COMPLETED', 'FAILED', 'ROLLED_BACK'
    IsDelta INTEGER DEFAULT 0,
    DurationSeconds INTEGER DEFAULT 0,
    TelemetryUploaded INTEGER DEFAULT 0
);

CREATE INDEX IF NOT EXISTS IDX_UpdateHistory_Status ON UpdateHistory(Status);
```

### 13.2 `RollbackLogs` SQLCipher Table Schema
```sql
CREATE TABLE IF NOT EXISTS RollbackLogs (
    Id TEXT PRIMARY KEY NOT NULL,
    UpdateHistoryId TEXT NOT NULL,
    Timestamp TEXT NOT NULL,
    FailureReason TEXT NOT NULL,
    FilesRestoredCount INTEGER NOT NULL,
    FOREIGN KEY(UpdateHistoryId) REFERENCES UpdateHistory(Id)
);
```

---

## 14 APIs & Message Serialization

### 14.1 Version Check Request API (`POST https://update.sayra.io/api/v1/check`)
*   **Headers:** `Content-Type: application/json`, `X-SAYRA-Client-Signature: <SignatureHash>`
*   **Request JSON:**
```json
{
  "WorkstationId": "WS-0941",
  "ActiveVersion": "2.3.5",
  "Channel": "Canary",
  "HardwareFingerprint": "9f8e7d6c5b4a3c2b1a...",
  "DatabaseSchemaVersion": 42
}
```

### 14.2 Version Check Response API
*   **Response JSON:**
```json
{
  "UpdateRequired": true,
  "TargetVersion": "2.4.0",
  "PackageUrl": "https://cdn.sayra.io/packages/release-2.4.0-delta.spk",
  "PackageHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "IsDelta": true,
  "MinRequiredVersion": "2.1.0",
  "IsForcedUpgrade": false,
  "Signature": "3045022100e4d5c6..."
}
```

---

## 15 Testing Strategy

```
       [ENTERPRISE TESTING PIPELINE]
┌───────────────────────────────────────┐
│              UNIT TESTS               │
│ - Validate version checks & jitter    │
│ - Verify chunk hash calculation       │
│ - Parse Manifest serialization blocks │
└──────────────────┬────────────────────┘
                   v
┌───────────────────────────────────────┐
│           INTEGRATION TESTS           │
│ - Execute VCDIFF Patch integrations   │
│ - Test SCM Service stopping/restarts   │
│ - Verify SQLCipher history logging    │
└──────────────────┬────────────────────┘
                   v
┌───────────────────────────────────────┐
│             TAMPER TESTS              │
│ - Force corrupt chunk bytes on disk   │
│ - Alter manifest signatures           │
│ - Intercept TLS cert connection calls │
└──────────────────┬────────────────────┘
                   v
┌───────────────────────────────────────┐
│             CHAOS TESTS               │
│ - Simulate power cuts during install  │
│ - Drop network interfaces mid-download│
│ - Simulate locked visual resources    │
└───────────────────────────────────────┘
```

### 15.1 Testing Scenarios

#### 15.1.1 Unit Tests
*   `Verify_SemVer_Evaluator_Rejects_Invalid_Version_Strings`
*   `Verify_Jitter_Calculator_Output_Is_Within_Defined_Bounds`
*   `Verify_Manifest_Parser_Rejects_Tampered_Signatures`

#### 15.1.2 Integration Tests
*   `Verify_VcdiffDeltaEngine_Correctly_Reconstructs_Binaries`
*   `Verify_InstallerEngine_Successfully_Stops_And_Starts_SAYRA_Service`
*   `Verify_SQLCipherUpdateHistory_Records_State_Transitions_Atomically`

#### 15.1.3 Recovery & Chaos Tests
*   `Simulate_Power_Failure_Mid_File_Swap_And_Assert_Auto_Rollback_On_Reboot`
*   `Drop_Network_Connection_During_Download_And_Verify_Chunk_Resumption`
*   `Inject_Invalid_Signature_Into_Chunk_And_Assert_Immediate_Quarantining`

---

## 16 Acceptance Criteria

Every subsystem must pass explicit, measurable validation criteria before being approved for release:

### 16.1 Verification Engine
*   *Criterion:* 100% of tampered, corrupt, or unsigned packages must be intercepted and quarantined.
*   *Latency:* Signature verification of a 25MB package must complete in $<100\text{ms}$.

### 16.2 Downloader Engine
*   *Criterion:* Downloads interrupted at any point must resume from the last completed chunk with zero bandwidth loss.
*   *Accuracy:* The byte rate-limiter must throttle downloads to within $\pm 5\%$ of configured limits.

### 16.3 Installer Engine
*   *Criterion:* Silent upgrades must execute in the background with zero visual prompts or taskbar focus stealing.
*   *Performance:* RAM usage during background tasks must stay below 30MB, with average background CPU $<0.5\%$.

### 16.4 Rollback Engine
*   *Criterion:* Workstation recovery must return the system to the preceding stable version in $<60$ seconds on failure.
*   *Data Preservation:* 100% of local configurations and offline queues must remain intact after rollback.

---

## 17 Risk Analysis & Mitigations

### 17.1 Security Risks
*   **Risk:** Attackers exploit the update pipeline to push malicious binaries.
*   **Mitigation:** Enforce ECDSA-P384 signatures on all manifests and package files. Code signatures are validated using the native `WinVerifyTrust` API prior to installation.

### 17.2 Operational Risks
*   **Risk:** Update activities saturate local cybercafe networks, causing high latency for active players.
*   **Mitigation:** Limit download tasks to scheduled maintenance windows and implement token-bucket rate limiting dynamically adjusted by active game states.

### 17.3 Rollback Risks
*   **Risk:** Power cuts occur during rollback restoration, leaving the workstation in an unbootable state.
*   **Mitigation:** Directory swaps utilize the atomic transactional `ReplaceFile` Win32 API. Backup directories are maintained on the physical drive until startup self-diagnostics pass successfully.

---

## 18 Future Integration

The Phase 6 Update & Deployment Platform is designed to integrate seamlessly with future developmental phases:

*   **Phase 7 (P2P Distribution):** The download manager's chunk verification and mirror interfaces will support P2P LAN distribution. Caching servers will register chunk availability on local subnets.
*   **Phase 8 (Advanced System Interoperability):** The installer engine will integrate with remote control channels, enabling administrators to push targeted diagnostic scripts and minor tools silently.

---

## 19 Implementation Checklist

### Epic 1: Secure Package Management (P0)
*   **Feature 1.1: SPK Archive Structure**
    *   *Task:* Implement parser for `.spk` package archives.
    *   *Subtask:* Write signature verification wrappers for pinned certificates.
*   **Feature 1.2: Signature and Hash Verification Engine**
    *   *Task:* Implement RSA/ECDSA manifest signature validation checks.
    *   *Subtask:* Create SHA-256 chunk validation pipelines.

### Epic 2: Resumable Chunked Downloader (P0)
*   **Feature 2.1: Chunked Download Manager**
    *   *Task:* Build parallel chunk downloader utilizing HTTP/2 streams.
    *   *Subtask:* Implement local cache tracking to support resume features.
*   **Feature 2.2: Bandwidth Management**
    *   *Task:* Integrate token-bucket rate limiters into download pipelines.

### Epic 3: Standalone Installer and Rollback Utility (P0)
*   **Feature 3.1: Standalone Updater Executable (`Sayra.Client.Updater`)**
    *   *Task:* Build standalone utility running in the elevated SYSTEM context.
    *   *Subtask:* Implement SCM integration to manage service states.
*   **Feature 3.2: Atomic Rollback Engine**
    *   *Task:* Build directory snapshot and restoration systems.

### Epic 4: Delta Update Integration (P1)
*   **Feature 4.1: Delta Application Core**
    *   *Task:* Integrate VCDIFF parsing libraries to apply binary patches.
    *   *Subtask:* Implement hash verification on reconstructed target binaries.

---

## 20 Deliverables

### 20.1 Source Classes & Interfaces
*   `IUpdateManager.cs` (Central update manager interface)
*   `IDownloadManager.cs` (Downloader interface)
*   `IPackageVerifier.cs` (Cryptographic verification interface)
*   `IInstallerEngine.cs` (Silent installer interface)
*   `IRollbackEngine.cs` (Rollback coordinator interface)
*   `IDeltaUpdateEngine.cs` (VCDIFF delta patch engine interface)
*   `UpdateManager.cs` (Main update orchestration class)
*   `DownloadManager.cs` (Resumable chunked file downloader)
*   `PackageVerifier.cs` (Signature and chunk hash verification engine)
*   `DeltaUpdateEngine.cs` (VCDIFF delta application class)

### 20.2 Services, Workers, & Executables
*   `Sayra.Client.Updater.exe` (Standalone elevated helper process)
*   `UpdatePollerWorker.cs` (Background service running update checks)
*   `TelemetryReporterWorker.cs` (Reports installer history to the server)

### 20.3 Policies & Configuration Templates
*   `client_update_policy.json` (Local update settings profile)
*   `update_manifest_schema.json` (JSON schema governing manifest structure)

### 20.4 Database Migrations & Schemas
*   `Migration_20261025_01_InitPhase6.sql` (Creates UpdateHistory, RollbackLogs tables)

### 20.5 Test Suite Portfolio
*   `Sayra.Client.Tests.Updates/` (Automated suite containing over 35 unit, integration, and recovery chaos test definitions)

---
*End of Specification Document.*

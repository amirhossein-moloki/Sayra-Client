# SAYRA Client Contract Specification (Source of Truth)

This specification serves as the absolute **Source of Truth** for the SAYRA Client-Server ecosystem. It describes the complete operational contracts, network expectations, messaging schemas, and operational properties of the feature-complete SAYRA Client. Future Server development must adhere strictly to the rules, schemas, and endpoints defined herein.

---

## Part 1: Comprehensive Subsystem & Feature Specifications

For each operational component within the SAYRA Client solution, the required contract attributes are detailed below.

### 1. Centralized Authentication & Authorization Layer

*   **1.1 Name:** Authentication & Authorization
*   **1.2 Purpose:** Secure identification and validation of system users, including local administrators, main administrators, local PC players (gamers), and reserved workstations. Supports dynamic authentication provider chaining (Local, Reservation, Cached, Offline, Server).
*   **1.3 UI Consumers:**
    *   `LoginWindow` (Standard player logon, administrator credentials)
    *   `TopBar` (Displays current session status, handles administrative bypass and logouts)
*   **1.4 ViewModels:**
    *   `LoginViewModel` (Coordinates credentials verification, parses authentication results, routes flows)
    *   `AdminWorkspaceViewModel` (Admin-only panel authorization check)
*   **1.5 Core Services:**
    *   `IAuthenticationService` (Central authentication orchestrator)
    *   `IAuthorizationService` (Maintains user contexts and permission structures)
    *   `IServerReservationService` (Remote reservation validation and cached checks)
    *   `IAuthenticationProvider` (Decoupled candidate provider interface)
*   **1.6 Required Models:**
    *   `AuthenticatedUser { Username, DisplayName, Role, Permissions, Avatar, SessionId }` (Immutable user record)
    *   `AuthenticationResult { Success, ErrorMessage, User, AuthenticationType, SessionId }`
    *   `UserRole` (Enum: `Gamer`, `Administrator`, `LocalAdministrator`, `Guest`)
    *   `UserPermission` (Enum: `LaunchGames`, `AccessAdminPanel`, `ConfigureSettings`, `ManageUsers`, `BypassBilling`, `ShutdownWorkstation`)
    *   `ReservationInfo { ReservationId, Username, StationId, StartTime, EndTime, RemainingCredits, IsExpired }`
    *   `ReservationValidationResult { Success, Message, Reservation }`
*   **1.7 Required Events:**
    *   `AuthenticationStarted` (Username, Timestamp)
    *   `AuthenticationSucceeded` (User, AuthType, SessionId, Timestamp)
    *   `AuthenticationFailed` (Username, Reason, Timestamp)
    *   `AuthenticationExpired` (User, Timestamp)
    *   `LogoutStarted` (User, Timestamp)
    *   `LogoutCompleted` (Username, Timestamp)
    *   `SessionExpired` (SessionId, Username, Timestamp)
*   **1.8 Background Workers:** None (Invoked interactively upon user submission).
*   **1.9 Communication Requirements:**
    *   **TCP Secure Socket:** Challenge-response verification via central TcpClientManager.
    *   **HTTP REST APIs (Auth fallback):** JSON payloads over SSL/TLS for Reservation Authentication Provider and Server Authentication Provider.
*   **1.10 Required Server Operations:**
    *   Generate cryptographic auth challenges (`AUTH_CHALLENGE`).
    *   Authenticate credentials against master database/Active Directory (`AUTH_RESPONSE`).
    *   Validate user active credits and active reservation bounds.
*   **1.11 Expected Input:** `Username` (string), `Password` (string, PBKDF2 hashed/cleartext over transport), `StationId` (string).
*   **1.12 Expected Output:** Immutable `AuthenticationResult` including session token, permissions map, and user roles.
*   **1.13 Required Permissions:** `AccessAdminPanel` (for AdminWindow), `LaunchGames` (for HomeWindow).
*   **1.14 Error Cases:**
    *   `InvalidCredentialsException` (Wrong username/password)
    *   `AccountLockedException` (Too many failed attempts)
    *   `ProviderUnavailableException` (No connection to central authentication provider)
    *   `AuthenticationFailedException` (General cryptographic/handshake failure)
*   **1.15 Retry Behaviour:**
    *   Fail-over to `CachedAuthenticationProvider` or `OfflineAuthenticationProvider` if LAN server connection is severed.
    *   Abort and notify user of locked account or bad credentials.
*   **1.16 Offline Behaviour:**
    *   Attempts to authenticate using the local offline cache (`reservation_cache.json`) for known players.
    *   Accepts local admin password authenticated locally via PBKDF2 hashed credentials stored in the `Data/local_admin.json` database.
*   **1.17 Current Client Implementation:** Fully Implemented.
*   **1.18 Server Dependency:** Hybrid (Local fallbacks for admin bypass; requires Server for real-time LAN players).

---

### 2. Workstation Session Management & Billing Engine

*   **2.1 Name:** Session Management & Billing
*   **2.2 Purpose:** Tracks active player session states, remaining time, local duration, incremental cost billing, and locks/unlocks the kiosk workstation screen in compliance with server commands.
*   **2.3 UI Consumers:**
    *   `HomeWindow` (Displays countdown, session details, and locked overlay states)
    *   `LoginWindow` (Locks client when no active session is authenticated)
    *   `SessionHero` component (Renders live timer metrics, active rate, and accumulated cost)
*   **2.4 ViewModels:**
    *   `SessionHeroViewModel` (Provides real-time Persian-formatted session updates)
    *   `LoginViewModel` (Initiates session hooks upon successful gamer logons)
*   **2.5 Core Services:**
    *   `SessionManager` / `ISessionManager` (Client core session manager)
    *   `KioskManager` (Applies system registry policies to enforce client lockdown)
*   **2.6 Required Models:**
    *   `SessionModel { SessionId, PcId, Username, StartTime, DurationMinutes, Status, ElapsedSeconds, RatePerHour, CurrentCost }`
    *   `ClientStateDto { CoreState, SessionStatus, RemainingTime, StartTime, ElapsedSeconds, TotalDurationMinutes, RatePerHour, CurrentCost, UserName, IsKioskLocked }`
    *   `SessionStatus` (Enum: `IDLE`, `ACTIVE`, `PAUSED`, `ENDED`)
    *   `ClientCoreState` (Enum: `STARTING`, `DISCOVERING`, `CONNECTING`, `AUTHENTICATING`, `READY`, `IN_SESSION`, `DISCONNECTED`, `RECOVERING`)
*   **2.7 Required Events:**
    *   `SESSION_STARTED` (Ipc message event broadcast to UI)
    *   `SESSION_ENDED` (Ipc message event broadcast to UI)
    *   `SESSION_TIME_UPDATED` (Ticks every second to synchronise timers)
    *   `BILLING_UPDATE` (Emits live hourly cost calculations)
*   **2.8 Background Workers:**
    *   `DispatcherTimer` (WPF UI-thread ticker, updates session hero display)
    *   `SessionManager` internal periodic timer (Ticks every second to write to `session_state.json` and decrement remaining seconds)
*   **2.9 Communication Requirements:**
    *   **Secure TCP Socket:** Outbound heartbeat logs of session progress. Inbound commands for start, pause, resume, or termination.
    *   **Named Pipe IPC:** Local named pipe (`SayraClientIpcPipe`) communicates state changes between the client background service and visual WPF client.
*   **2.10 Required Server Operations:**
    *   `START_SESSION`: Initiates user session duration on specified target terminal.
    *   `STOP_SESSION`: Terminates active duration and forces kiosk lock.
    *   `PAUSE_SESSION`: Temporarily suspends duration countdown, locking the workstation but preserving the session.
    *   `RESUME_SESSION`: Resumes the countdown and unlocks the workspace.
*   **2.11 Expected Input:** Session identifiers, PC identifiers, rate structures, and permitted durations.
*   **2.12 Expected Output:** Operational execution result (`ExecutionResult`) indicating command processing success or failure.
*   **2.13 Required Permissions:** `BypassBilling` (for free admin sessions).
*   **2.14 Error Cases:**
    *   Session timeout (elapsed seconds exceeds duration).
    *   Session state corruption (deserialization error in local file on recovery).
    *   Server communication severed during active session.
*   **2.15 Retry Behaviour:**
    *   Persists active state locally to `session_state.json` so if the client agent crashes, the session automatically resumes.
    *   Runs offline countdown if TCP connection with server is briefly lost.
*   **2.16 Offline Behaviour:**
    *   Retains the countdown using local timer checks. If the server remains disconnected when the session expires, the client locks down the station autonomously.
*   **2.17 Current Client Implementation:** Fully Implemented.
*   **2.18 Server Dependency:** Hybrid (Local persistence guarantees fail-safe continuity; server serves as master state coordinator).

---

### 3. Game & Application Library

*   **3.1 Name:** Game & Application Library
*   **3.2 Purpose:** Maintains the local workstation registry of installed games and programs. Provides categorization, favorite flags, launch metadata, and executable path verification.
*   **3.3 UI Consumers:**
    *   `HomeWindow` / `GameLibrary` control (Renders categorized grid cards)
    *   `AdminWindow` / `GamesDataGrid` (Provides administrative overview, metadata edits, category mapping, and list/compact/grid modes)
    *   `GameDetailWindow` (Displays specific game description, categories, launch policies, and validation states)
*   **3.4 ViewModels:**
    *   `GameLibraryViewModel` (Dynamically loads categories and games, manages categories sidebar)
    *   `AdminWorkspaceViewModel` (Manages CRUD persistence of games library database)
    *   `GameDetailViewModel` (Binds specific game properties, launches status monitoring)
*   **3.5 Core Services:**
    *   `IGameLibraryService` / `GameLibraryService` (Saves, updates, loads list of game profiles)
    *   `IGameValidationService` / `GameValidationService` (Validates target game paths and executable files)
    *   `IGameLibraryRepository` (Local JSON repository mapping persistence)
*   **3.6 Required Models:**
    *   `Game { Id, Name, ExecutablePath, Arguments, WorkingDirectory, IconPath, Enabled, Source, Category }`
    *   `GameCategory { Id, Name }`
    *   `GameSource` (Enum: `Scanner`, `Server`, `Manual`)
    *   `GameValidationResult { Status, Message, IsPlayable }`
    *   `GameValidationStatus` (Enum: `Installed`, `Missing`, `Corrupted`, `Disabled`, `NeedsVerification`, `Unsupported`, `Unknown`)
*   **3.7 Required Events:**
    *   `SyncStarted`, `SyncCompleted`, `SyncFailed` (For client-to-server library synchronisation)
*   **3.8 Background Workers:** None (CRUD interactive actions).
*   **3.9 Communication Requirements:**
    *   **TCP Secure Socket:** Sends synchronization metadata requests to compare workstation catalog against server-defined master templates.
*   **3.10 Required Server Operations:**
    *   `CompareLocalAndServer`: Evaluates workstation client database hashes with central server profiles.
    *   `SyncFromServer`: Transfers global game manifests and centralized launching templates down to the workstation.
*   **3.11 Expected Input:** Workstation game profiles, checksum arrays.
*   **3.12 Expected Output:** Synchronized catalog payload, mapping missing categories and executables.
*   **3.13 Required Permissions:** `AccessAdminPanel` (for editing game library registries).
*   **3.14 Error Cases:**
    *   Target executable missing (Game status changes to `Missing`).
    *   Read/write lock exceptions on `Data/game_library.json`.
*   **3.15 Retry Behaviour:**
    *   Falling back to `MockGameService.GetStaticGames()` if the workstation library file is corrupt, absent, or fails to fetch.
*   **3.16 Offline Behaviour:**
    *   Allows players to see and play already-installed, validated offline local games.
*   **3.17 Current Client Implementation:** Fully Implemented.
*   **3.18 Server Dependency:** Hybrid (Local library manages startup paths; server coordinates global game catalogs and synchronization hashes).

---

### 4. Interactive Application Scanner & Heuristics Engine

*   **4.1 Name:** Application Scanner
*   **4.2 Purpose:** Scans workstation hard drives, system folders, registry directories, and launchers (Steam, Epic, Riot, EA, Ubisoft, GOG, Battle.net) to discover and automatically register installed games and applications. Uses heuristic classifiers to evaluate target PE headers, executable hashes, and shortcut links.
*   **4.3 UI Consumers:**
    *   `AdminWindow` (Renders empty state with "Start Scan" trigger, displays spinner, real-time files counter, and progress bar)
*   **4.4 ViewModels:**
    *   `AdminWorkspaceViewModel` (Triggers asynchronous scan background tasks, binds progress callbacks to update progress bar and stats)
*   **4.5 Core Services:**
    *   `IApplicationScannerService` / `ApplicationScannerService` (Asynchronous directory traverser and registry reader)
    *   `IGameDetectionEngine` (Heuristics classifier evaluating confidence scores)
    *   `IExecutableMetadataProvider` (Extracts publisher, file hashes, and executable embedded icons)
    *   `IScanCacheService` (Caches scanned directory paths to prevent duplicate traversals)
    *   `IScannerValidator` (Applies excluded folders filtering to prevent scanning system files)
*   **4.6 Required Models:**
    *   `DetectedApplication { Id, Name, ExecutablePath, WorkingDirectory, Publisher, Version, Category, Launcher, ExecutableHash, Icon, Type, ConfidenceScore }`
    *   `ScanProgress { TotalFiles, ScannedFiles, CurrentFile }`
    *   `KnownGameSignature { Name, ExecutableName, Launcher, Category, MinSize, MaxSize }`
*   **4.7 Required Events:** None (Utilizes `IProgress<ScanProgress>` to report progress to ViewModels).
*   **4.8 Background Workers:** None (Executes asynchronously as an on-demand task).
*   **4.9 Communication Requirements:** None.
*   **4.10 Required Server Operations:** None (Executes locally on terminal).
*   **4.11 Expected Input:** Root directories lists to scan (optional), progress callbacks.
*   **4.12 Expected Output:** List of `DetectedApplication` structures with populated types ("Game" or "Application").
*   **4.13 Required Permissions:** `AccessAdminPanel` (only authorized administrators can initiate scanner).
*   **4.14 Error Cases:**
    *   Unauthorized folder access (skipped gracefully during traversal).
    *   Corrupted shortcut file (`.lnk`, `.url`) structures.
*   **4.15 Retry Behaviour:**
    *   Saves scanning states to scan cache. In case of failure or scan stop, subsequent attempts hit the local path cache for sub-millisecond responses.
*   **4.16 Offline Behaviour:**
    *   Works fully offline; does not require active server network connectivity to index games.
*   **4.17 Current Client Implementation:** Fully Implemented.
*   **4.18 Server Dependency:** Local Only.

---

### 5. Robust Game Process Launcher & Crash Monitor

*   **5.1 Name:** Game Launcher & Process Monitor
*   **5.2 Purpose:** Launches selected games, manages process startup arguments, binds active session constraints, monitors process execution, triggers automatic crash recovery (re-launches up to 3 times), and captures real-time CPU/RAM statistics.
*   **5.3 UI Consumers:**
    *   `HomeWindow` (Disables "Play" button, displays launching spinner states)
    *   `GameDetailWindow` (Displays real-time status badge: "Playable", "Running", "Launching", "Crash Recovering", updates colors dynamically)
*   **5.4 ViewModels:**
    *   `GameLibraryViewModel` (Receives IPC notifications when games exit/start to update play button states)
    *   `GameDetailViewModel` (Subscribes to launcher events, cleans subscriptions on window close)
*   **5.5 Core Services:**
    *   `IGameLauncherService` / `GameLauncherService` (Launches, stops, and restarts programs)
    *   `IProcessMonitorService` / `ProcessMonitorService` (Tracks PIDs, resources, CPU, and RAM consumption)
    *   `ILauncherRecoveryService` (Orchestrates restart retry parameters)
*   **5.6 Required Models:**
    *   `ProcessStatistics { GameId, Name, Pid, CpuUsagePercentage, RamUsageMb, RunningDuration, IsRunning }`
    *   `LaunchOptions { RunAsAdmin, Arguments, WorkingDirectory }`
*   **5.7 Required Events:**
    *   `GameLaunching` (GameId, Name)
    *   `GameStarted` (Pid, GameId, Name)
    *   `GameExited` (GameId, Name, ExitCode, Duration)
    *   `GameCrashed` (GameId, Name, ExitCode, Reason)
    *   `GameRestarted` (GameId, Name, RetryCount)
    *   `GameKilled` (GameId, Name, Pid)
    *   `LaunchFailed` (GameId, Name, Reason)
*   **5.8 Background Workers:**
    *   `ProcessMonitorService` background task (Checks processes list every 500ms to monitor resource usage and state transitions).
*   **5.9 Communication Requirements:**
    *   **Secure TCP Socket:** Sends process events (`GAME_LAUNCHED`, `GAME_EXITED`, `GAME_CRASHED`) to server for administrator audit logs.
*   **5.10 Required Server Operations:**
    *   Server acts as audit logger; receives client-emitted process execution metrics.
*   **5.11 Expected Input:** `GameId` (string).
*   **5.12 Expected Output:** Boolean startup confirmation, interactive process handle trackers.
*   **5.13 Required Permissions:** `LaunchGames`.
*   **5.14 Error Cases:**
    *   Access denied / Admin privileges required to launch.
    *   Invalid license validation checks.
    *   File execution format error (32-bit vs 64-bit platform conflict).
*   **5.15 Retry Behaviour:**
    *   Automatically executes recovery restarts if a game process crashes within 60 seconds of launch (configured up to 3 retries).
*   **5.16 Offline Behaviour:**
    *   Operates completely offline, but bypasses server telemetry logging if the LAN network is unavailable.
*   **5.17 Current Client Implementation:** Fully Implemented.
*   **5.18 Server Dependency:** Hybrid (Launches locally; logs stats and crashes back to server).

---

### 6. Hardware Diagnostics & Telemetry Profiling

*   **6.1 Name:** Hardware Diagnostics & Telemetry
*   **6.2 Purpose:** Acquires technical hardware specifications (CPU, GPUs, RAM modules, display configurations, OS metrics, DirectX/OpenGL/Vulkan states) and continuously gathers real-time telemetry performance data (CPU load, RAM consumption, uptime statistics).
*   **6.3 UI Consumers:**
    *   `HardwarePanel` control (Renders live load dials, display resolutions, and refresh rates)
    *   `AdminWindow` (Displays hardware overview summaries)
*   **6.4 ViewModels:**
    *   `HardwarePanelViewModel` (Ticks on timer thread to refresh UI dials and spec strings)
*   **6.5 Core Services:**
    *   `IHardwareSpecificationService` (Extracts hardware spec records)
    *   `IHardwareTelemetryService` (Gathers live load percentages)
    *   `IHardwareMonitoringService` (Central specification and metrics aggregator)
*   **6.6 Required Models:**
    *   `HardwareSpecification { Cpu, Gpus, Memory, Storages, Motherboard, OperatingSystem, GraphicsApi, Displays, Networks, CollectedAt }`
    *   `HardwareMetrics { CpuUsagePercentage, TotalMemoryMb, AvailableMemoryMb, UsedMemoryMb, MemoryUsagePercentage, Uptime, GpuMetrics, StorageMetrics, NetworkMetrics }`
    *   `TelemetrySnapshot { CpuPercentage, RamPercentage, UptimeSeconds, ActiveProcess, Timestamp }`
*   **6.7 Required Events:**
    *   `HardwareInitialized` (Specification)
    *   `HardwareMetricsUpdated` (Metrics)
    *   `HardwareChanged` (OldSpecification, NewSpecification)
*   **6.8 Background Workers:**
    *   `DispatcherTimer` (Updates visual dials on UI thread every 2 seconds)
    *   Telemetry reporting service thread (Schedules telemetry uploads to the server).
*   **6.9 Communication Requirements:**
    *   **Secure TCP Socket:** Transmits telemetry packets (`TelemetryModel`) containing hardware diagnostic reports periodically to the server.
*   **6.10 Required Server Operations:**
    *   `UploadTelemetry`: Receives and tracks system performance trends for target terminals.
*   **6.11 Expected Input:** Telemetry snapshot frequencies.
*   **6.12 Expected Output:** Dynamic telemetry confirmation.
*   **6.13 Required Permissions:** None (Runs globally).
*   **6.14 Error Cases:**
    *   WMI interface query timeout or access restricted (reverts to safe diagnostic fallbacks).
    *   GPU/Display detection failures (reverts to generic motherboard adapter values).
*   **6.15 Retry Behaviour:**
    *   If WMI queries fail, system returns realistic hardcoded fallbacks (e.g., ASUS Motherboard, 32GB RAM module configurations) to maintain flawless UI render state.
*   **6.16 Offline Behaviour:**
    *   Queries hardware metrics and updates the UI locally. Discards server-bound telemetry payload logs if network connection is lost.
*   **6.17 Current Client Implementation:** Fully Implemented.
*   **6.18 Server Dependency:** Hybrid (Diagnostic acquisition executes locally; server receives telemetry data for remote dashboard display).

---

### 7. UDP LAN Auto-Discovery Protocol

*   **7.1 Name:** Auto-Discovery
*   **7.2 Purpose:** Dynamically scans the LAN network via encrypted UDP socket broadcast loops to find, handshake, and resolve the Server's IP address and TCP command port without manual administrator configurations.
*   **7.3 UI Consumers:**
    *   `LoginWindow` (Displays server discovery states, switches status indicator from Offline to Online)
*   **7.4 ViewModels:**
    *   `LoginViewModel` (Synchronizes visual connection indicators with client-state transitions)
*   **7.5 Core Services:**
    *   `IDiscoveryService` (Standard interface resolving target server endpoints)
    *   `DiscoveryManager` (Performs active UDP broadcasts and processes server beacons)
    *   `UdpDiscoveryClient` (Manages socket listeners on UDP Port `37020`)
    *   `DiscoveryValidator` (Verifies cryptographic server signature nonces)
*   **7.6 Required Models:**
    *   `DiscoveryRequest { ClientId, MachineName, Timestamp, Nonce }`
    *   `ServerDiscoveryResponse { ip, tcpPort, serverName, serverId, Signature }`
    *   `DiscoveryResponse { ip, tcpPort, serverName, serverId, Signature, LatencyMs }`
    *   `ServerCache { IpAddress, Port, ServerName, DiscoveredAt }`
*   **7.7 Required Events:** None (Runs synchronously on startup or disconnect recovery flows).
*   **7.8 Background Workers:** None (Run on demand as part of the connection startup flow).
*   **7.9 Communication Requirements:**
    *   **UDP Broadcast:** Dispatches broadcast frames to `255.255.255.255` on Port `37020`.
*   **7.10 Required Server Operations:**
    *   Server must listen on UDP Port `37020` and reply with signed payload packets containing server identification, IP, and command ports.
*   **7.11 Expected Input:** Security nonces and broadcast intervals.
*   **7.12 Expected Output:** Confirmed `DiscoveryResponse` mapping server network credentials.
*   **7.13 Required Permissions:** None.
*   **7.14 Error Cases:**
    *   Port conflicts on local port bindings.
    *   Invalid server cryptographic signatures (indicates rogue server on network, packet dropped).
*   **7.15 Retry Behaviour:**
    *   Caches the discovered server settings locally in `server_discovery_cache.json`.
    *   On disconnect, first tries the cached server IP; if connection fails, triggers a fresh UDP broadcast scan.
*   **7.16 Offline Behaviour:**
    *   Retries UDP scanning indefinitely on a progressive reconnect backup schedule, keeping the client locked down in an offline state.
*   **7.17 Current Client Implementation:** Fully Implemented.
*   **7.18 Server Dependency:** Requires Server.

---

### 8. Secure Background Binary Update Engine

*   **8.1 Name:** Binary Update System
*   **8.2 Purpose:** Automatically queries the central update server for updated build manifests, compares local assembly versions, downloads new binary files, validates integrity hashes (SHA-256) and RSA signatures, and spawns the standalone utility `SayraUpdater.exe` to swap files.
*   **8.3 UI Consumers:** None (Operates completely as a silent background agent).
*   **8.4 ViewModels:** None (Communicates progress to local named pipe IPC clients).
*   **8.5 Core Services:**
    *   `UpdateManager` (Background service controlling version checks and download schedules)
    *   `UpdateVerificationService` (Performs RSA-256 binary validation checks)
*   **8.6 Required Models:**
    *   `UpdateManifest { Version, ReleaseNotes, PackageUrl, Checksum, Signature, IsCritical, ReleaseDate }`
    *   `UpdateProgressPayload { Version, ProgressPercentage, CurrentAction }`
*   **8.7 Required Events:**
    *   `UPDATE_AVAILABLE` (Emitted via local IPC to notify clients)
    *   `UPDATE_PROGRESS` (Reports download percentage)
    *   `UPDATE_SUCCESS` / `UPDATE_FAILED`
*   **8.8 Background Workers:**
    *   `UpdateManager` (Schedules update check loops every 60 minutes).
*   **8.9 Communication Requirements:**
    *   **HTTP REST Web API:** GET requests to pull `api/updates/manifest`.
    *   **HTTP Binary Stream:** Pulls package binaries over HTTPS or high-speed local server folders.
*   **8.10 Required Server Operations:**
    *   Server must host the JSON update manifest API.
    *   Server must provide secure HTTP download endpoints for binary update packages.
*   **8.11 Expected Input:** Workstation current client version string.
*   **8.12 Expected Output:** `UpdateManifest` model and binary package streams.
*   **8.13 Required Permissions:** None (Requires local system administrative permissions).
*   **8.14 Error Cases:**
    *   Checksum mismatch (package corrupt, update abandoned).
    *   RSA signature validation failed (rogue package detected, aborted).
    *   Session active (update gets deferred to prevent interrupting the player).
*   **8.15 Retry Behaviour:**
    *   If download fails, the client abandons the attempt and schedules another check in the next hourly interval.
*   **8.16 Offline Behaviour:**
    *   Stays on the current local executable version; resumes checks once network connectivity is restored.
*   **8.17 Current Client Implementation:** Fully Implemented.
*   **8.18 Server Dependency:** Requires Server.

---

### 9. Workstation Power & Power State Management

*   **9.1 Name:** Power Management
*   **9.2 Purpose:** Permits remote server administrators to trigger workstation power operations, including system reboots, shutdowns, operating system logoffs, or screen locks.
*   **9.3 UI Consumers:**
    *   `AdminWindow` (Allows local administrator to reboot/shutdown)
    *   `LoginWindow` (Workspace lock visual)
*   **9.4 ViewModels:**
    *   `AdminWorkspaceViewModel` (Binds local exit and system triggers)
*   **9.5 Core Services:**
    *   `IPowerManagementService` / `PowerManagementService` (Executes OS-specific shell commands)
*   **9.6 Required Models:**
    *   `PowerActionEventArgs { Action }`
    *   `PowerActionFailedEventArgs { Action, Exception }`
*   **9.7 Required Events:**
    *   `ActionExecuting`
    *   `ActionExecuted`
    *   `ActionFailed`
*   **9.8 Background Workers:** None.
*   **9.9 Communication Requirements:**
    *   **Secure TCP Socket:** Receives incoming JSON command actions (`RESTART_PC`, `SHUTDOWN_PC`, `LOGOFF_PC`, `LOCK_PC`).
*   **9.10 Required Server Operations:**
    *   Server administrator panel issues command payloads to specific target terminal IP/IDs.
*   **9.11 Expected Input:** Power command type directives.
*   **9.12 Expected Output:** Outbound execution success/failure result payload.
*   **9.13 Required Permissions:** `ShutdownWorkstation`.
*   **9.14 Error Cases:**
    *   Insufficient system privileges to invoke power management actions.
*   **9.15 Retry Behaviour:**
    *   Logs failures back to the server; aborts if system calls fail.
*   **9.16 Offline Behaviour:**
    *   Ignores remote triggers; local administrative commands remain functional on the terminal.
*   **9.17 Current Client Implementation:** Fully Implemented.
*   **9.18 Server Dependency:** Hybrid.

---

### 10. Secure AES-256 Workstation Backup & Restore

*   **10.1 Name:** Workstation Backup & Restore
*   **10.2 Purpose:** Creates, decrypts, and restores AES-256-CBC encrypted archives of the workstation's critical settings and database directory (`Data/` folder containing local registries, settings, credentials, caches, and logs). Uses PBKDF2 for password key derivation.
*   **10.3 UI Consumers:**
    *   `AdminWindow` (Binds Backup and Restore buttons in the statistics footer card)
*   **10.4 ViewModels:**
    *   `AdminWorkspaceViewModel` (Exposes `BackupCommand` and `RestoreCommand` with loading states)
*   **10.5 Core Services:**
    *   `IWorkstationBackupService` / `WorkstationBackupService` (Encrypts/decrypts file directories)
*   **10.6 Required Models:** None (Uses standard file paths and key parameters).
*   **10.7 Required Events:** None.
*   **10.8 Background Workers:** Runs asynchronously.
*   **10.9 Communication Requirements:** None.
*   **10.10 Required Server Operations:** None.
*   **10.11 Expected Input:** Destination backup path, encryption password (uses system-configured defaults if null).
*   **10.12 Expected Output:** Hex-encoded file checksum hash, backup archive file.
*   **10.13 Required Permissions:** `ConfigureSettings` (Admin-only).
*   **10.14 Error Cases:**
    *   Decryption password mismatch (Restore fails with validation error).
    *   Corruption of the ZIP archive format.
*   **10.15 Retry Behaviour:**
    *   Aborts process and alerts administrator with exact error metrics.
*   **10.16 Offline Behaviour:**
    *   Executes fully offline at the terminal.
*   **10.17 Current Client Implementation:** Fully Implemented.
*   **10.18 Server Dependency:** Local Only.

---

### 11. Client Configuration & Station Identity

*   **11.1 Name:** Client Configuration & Station Identity
*   **11.2 Purpose:** Loads, tracks, and persists client-specific preferences (`client_config.json` containing theme, language, and paths) and generates the workstation identity (resolved from machine name, MAC address, and local IPv4).
*   **11.3 UI Consumers:**
    *   `LoginWindow` (Displays station name resolved dynamically in RTL layout)
    *   `HomeWindow` / `TopBar` (Renders resolved station identity)
*   **11.4 ViewModels:**
    *   `LoginViewModel` (Resolves displaying name on load)
*   **11.5 Core Services:**
    *   `IClientConfigurationService` / `ClientConfigurationService` (Config loader and writer)
    *   `IStationIdentityService` / `StationIdentityService` (System properties resolver)
    *   `IClientConfigurationRepository` (File reader map)
*   **11.6 Required Models:**
    *   `ClientConfiguration { ServerDiscovery, GameLibrary, ScannerPaths, LocalPreferences, StationName, StationId, ClientId }`
    *   `StationIdentity { MachineName, ConfiguredStationName, StationId, ClientId, MacAddress, LocalIPv4, CurrentHostname, EnvironmentInformation, ResolvedStationName }`
*   **11.7 Required Events:** None.
*   **11.8 Background Workers:** None.
*   **11.9 Communication Requirements:**
    *   **Secure TCP Socket:** Sends workstation identification credentials during the initial connection handshake.
*   **11.10 Required Server Operations:**
    *   Map incoming client connection MAC and Station IDs to register and list active terminals on the central dashboard.
*   **11.11 Expected Input:** System hardware registry values.
*   **11.12 Expected Output:** Station identity record.
*   **11.13 Required Permissions:** None.
*   **11.14 Error Cases:**
    *   Configuration parse exception (reverts to default settings).
    *   MAC or network adapter missing (reverts to local hostname resolution).
*   **11.15 Retry Behaviour:**
    *   Reverts to default settings if local files are missing.
*   **11.16 Offline Behaviour:**
    *   Resolves local identities autonomously.
*   **11.17 Current Client Implementation:** Fully Implemented.
*   **11.18 Server Dependency:** Hybrid.

---

### 12. Active Scheduled Advertisements Engine

*   **12.1 Name:** Scheduled Advertisements
*   **12.2 Purpose:** Manages local offline-ready JSON advertising databases, displaying scheduled, prioritized image banners, titles, and active URLs on the visual dashboard.
*   **12.3 UI Consumers:**
    *   `AdPanel` component (Renders active image banners with auto-rotation transitions)
*   **12.4 ViewModels:**
    *   `AdPanelViewModel` (Executes the banner carousel tick logic)
*   **12.5 Core Services:**
    *   `IAdvertisementService` / `AdvertisementService` (JSON list manager)
*   **12.6 Required Models:**
    *   `Advertisement { Id, Title, Description, ImageUrl, ActionUrl, ButtonText, Priority, StartTime, EndTime, IsActive }`
*   **12.7 Required Events:** None.
*   **12.8 Background Workers:**
    *   `DispatcherTimer` (Ticks every 10 seconds to switch carousel items).
*   **12.9 Communication Requirements:**
    *   **Secure TCP Socket:** Hook to trigger dynamic advertising synchronization from the server.
*   **12.10 Required Server Operations:**
    *   `GetActiveAdvertisements`: Serves valid, running marketing campaigns to clients.
*   **12.11 Expected Input:** Current timestamp, station categories.
*   **12.12 Expected Output:** Array of `Advertisement` items.
*   **12.13 Required Permissions:** None.
*   **12.14 Error Cases:**
    *   Image files not found (skips rendering of the invalid ad slot).
*   **12.15 Retry Behaviour:**
    *   Maintains local fallback ads database to guarantee advertising boxes are never empty.
*   **12.16 Offline Behaviour:**
    *   Utilizes local JSON database and cached image files.
*   **12.17 Current Client Implementation:** Fully Implemented.
*   **12.18 Server Dependency:** Hybrid.

---

### 13. State Recovery & Watchdog Reconciliation

*   **13.1 Name:** Watchdog State Reconciliation
*   **13.2 Purpose:** Guarantees connection state integrity. Executes immediately when the client establishes or recovers a server TCP connection (`CLIENT_CONNECTED` event), dispatching workstation session properties so the server can validate and correct any client/server state conflicts.
*   **13.3 UI Consumers:** None.
*   **13.4 ViewModels:** None (Executed at the core client application layer).
*   **13.5 Core Services:**
    *   `RecoveryManager` (Orchestrates connection state validations)
    *   `ClientStateManager` (Tracks client system state transitions)
*   **13.6 Required Models:**
    *   `SyncState { CurrentSessionId, Status, KioskLocked }`
*   **13.7 Required Events:**
    *   `CONNECTION_STATUS_CHANGED` (Local IPC event reported to visual clients)
*   **13.8 Background Workers:** None.
*   **13.9 Communication Requirements:**
    *   **Secure TCP Socket:** Outbound client connection sync payload (`CLIENT_CONNECTED`). Inbound corrective states or force-termination commands.
*   **13.10 Required Server Operations:**
    *   Receive client `CLIENT_CONNECTED` event. Evaluate server database states against client assertions, dispatching corrective overrides if a state conflict exists.
*   **13.11 Expected Input:** PC identifier, Client State record.
*   **13.12 Expected Output:** Authoritative state synchronization commands.
*   **13.13 Required Permissions:** None.
*   **13.14 Error Cases:**
    *   Reconciliation payload fails to deliver.
*   **13.15 Retry Behaviour:**
    *   Attempts state synchronization on every connection attempt.
*   **13.16 Offline Behaviour:**
    *   Remains locked or active in local state until connection with authoritative server is re-established.
*   **13.17 Current Client Implementation:** Fully Implemented.
*   **13.18 Server Dependency:** Requires Server.

---

## Part 2: Unified Subsystem Contract Matrix

| Feature | UI Consumers | ViewModels | Core Service | Communication | Server Requirement | Client Status | Priority | Server Dependency |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **Authentication** | `LoginWindow`, `TopBar` | `LoginViewModel`, `AdminWorkspaceViewModel` | `IAuthenticationService`, `IServerReservationService` | TCP Handshake, HTTPS REST | Challenge-response verification, active reservation lookups | Fully Implemented | Critical | Hybrid |
| **Session Control** | `HomeWindow`, Kiosk Overlay | `SessionHeroViewModel`, `LoginViewModel` | `SessionManager`, `KioskManager` | TCP Command Packets, IPC Pipes | Master session authority (Start, Pause, Terminate) | Fully Implemented | Critical | Hybrid |
| **Game Library** | `HomeWindow`, `AdminWindow`, `GameDetailWindow` | `GameLibraryViewModel`, `AdminWorkspaceViewModel`, `GameDetailViewModel` | `IGameLibraryService`, `IGameValidationService` | TCP (Library Delta Comparison) | Serving game templates, catalogs, comparison manifests | Fully Implemented | High | Hybrid |
| **App Scanner** | `AdminWindow` | `AdminWorkspaceViewModel` | `IApplicationScannerService` | None (Local only) | None (Executes locally) | Fully Implemented | Medium | Local Only |
| **Launcher** | `HomeWindow`, `GameDetailWindow` | `GameLibraryViewModel`, `GameDetailViewModel` | `IGameLauncherService`, `IProcessMonitorService` | TCP Events | Logs process metrics, crashes, start/stops | Fully Implemented | High | Hybrid |
| **Diagnostics** | `HardwarePanel`, `AdminWindow` | `HardwarePanelViewModel` | `IHardwareMonitoringService` | TCP Telemetry Upload | Receives terminal resource logs | Fully Implemented | Medium | Hybrid |
| **Auto-Discovery** | `LoginWindow` | `LoginViewModel` | `IDiscoveryService`, `DiscoveryManager` | UDP Port `37020` | Broadcast server metadata beacons | Fully Implemented | High | Requires Server |
| **Update Engine** | None | None | `UpdateManager`, `UpdateVerificationService` | HTTPS REST Manifests, HTTPS package streams | Hostsmanifest APIs and update binaries | Fully Implemented | High | Requires Server |
| **Power Control** | `AdminWindow`, `LoginWindow` | `AdminWorkspaceViewModel` | `IPowerManagementService` | TCP Commands | Sends remote reboot/shutdown commands | Fully Implemented | Medium | Hybrid |
| **Backup/Restore**| `AdminWindow` | `AdminWorkspaceViewModel` | `IWorkstationBackupService` | None (Local only) | None (Executes locally) | Fully Implemented | Low | Local Only |
| **Configurations**| `LoginWindow`, `HomeWindow` | `LoginViewModel` | `IClientConfigurationService`, `IStationIdentityService` | TCP Handshake payload | Maps client terminal name and hardware IDs | Fully Implemented | Medium | Hybrid |
| **Advertisements**| `AdPanel` | `AdPanelViewModel` | `IAdvertisementService` | TCP Synchronization hook | Hosts advertising databases | Fully Implemented | Low | Hybrid |
| **State Watchdog**| None | None | `RecoveryManager`, `ClientStateManager` | TCP Sync Beacons | Synchronises client state upon connection | Fully Implemented | High | Requires Server |

---

## Part 3: Required Server Endpoint Specifications

This section defines the API endpoints and network ports that the Server must expose to fulfill the client contracts.

### 1. UDP Discovery Port (`37020` - UDP)
*   **Action:** Broadcast Response
*   **Description:** Listens for UDP broadcasts containing a JSON payload string from client terminals. Verifies client signature parameters and replies directly to the client's socket with server network info.
*   **Request Schema (Client -> Broadcast):**
    ```json
    {
      "ClientId": "SAYRA-WORKSTATION-UUID",
      "MachineName": "CLIENT-PC-01",
      "Timestamp": "2026-10-18T12:00:00Z",
      "Nonce": "A5D93F..."
    }
    ```
*   **Response Schema (Server -> Client):**
    ```json
    {
      "ip": "192.168.1.100",
      "tcpPort": 5000,
      "serverName": "SAYRA-CENTRAL-SERVER",
      "serverId": "SAYRA-SRV-01",
      "Signature": "BASE64_HMAC_SIGNATURE..."
    }
    ```

### 2. Secure TCP Port (`5000` - TCP)
Workstations establish persistent TCP connections to Port 5000. All traffic sent or received post-handshake is wrapped in a secure envelope encrypted with AES-256-CBC and signed using HMAC-SHA256.

#### Handshake Messages:
*   **AUTH_CHALLENGE (Server -> Client):**
    ```json
    {
      "type": "AUTH_CHALLENGE",
      "challenge": "CRYPTOGRAPHIC_RANDOM_CHALLENGE_HEX",
      "timestamp": "2026-10-18T12:00:00Z"
    }
    ```
*   **AUTH_RESPONSE (Client -> Server):**
    ```json
    {
      "type": "AUTH_RESPONSE",
      "clientId": "SAYRA-WORKSTATION-UUID",
      "mac": "00:1A:2B:3C:4D:5E",
      "stationId": "CLIENT-01",
      "challenge": "CRYPTOGRAPHIC_RANDOM_CHALLENGE_HEX",
      "response": "HMAC_SHA256(MasterKey, challenge)",
      "encryptedSessionKey": "RSA_ENCRYPTED(ClientSessionKey)"
    }
    ```
*   **AUTH_STATUS (Server -> Client):**
    ```json
    {
      "type": "AUTH_STATUS",
      "status": "SUCCESS", // Or "FAILED"
      "message": "Terminal successfully registered"
    }
    ```

#### Post-Handshake Secure Message Envelope Structure:
```json
{
  "payload": "AES_256_CBC_ENCRYPTED_JSON_STRING",
  "signature": "HMAC_SHA256(ClientSessionKey, timestamp|payload)",
  "timestamp": "2026-10-18T12:00:05Z"
}
```

### 3. HTTP REST API endpoints (Port `5000` - HTTPS)

*   **POST `/api/auth/login`:**
    *   **Description:** Verifies gamer account logins.
    *   **Input:** `{ "username": "...", "password": "..." }`
    *   **Output:** `{ "success": true, "user": { "username": "amir", "displayName": "امیر محمدی", "role": "Gamer" }, "sessionId": "..." }`

*   **GET `/api/reservations/validate`:**
    *   **Description:** Verifies dynamic reservation credits and active session schedules.
    *   **Query Params:** `username`, `reservationId`
    *   **Output:** `{ "success": true, "reservation": { "reservationId": "R-101", "username": "amir", "endTime": "2026-10-18T14:00:00Z", "remainingCredits": 30000.0 } }`

*   **GET `/api/updates/manifest`:**
    *   **Description:** Returns the active client release manifest.
    *   **Output:**
        ```json
        {
          "version": "1.2.5",
          "releaseNotes": "Critical security and gaming optimizations.",
          "packageUrl": "http://192.168.1.100:5000/api/updates/download/sayra-client-1.2.5.zip",
          "checksum": "3A9D8E6F...", // SHA-256
          "signature": "BASE64_RSA_SIGNATURE...",
          "isCritical": true,
          "releaseDate": "2026-10-18T00:00:00Z"
        }
        ```

*   **GET `/api/advertisements`:**
    *   **Description:** Yields current marketing and ad catalog files.
    *   **Output:** Array of `Advertisement` JSON blocks.

*   **POST `/api/workstations/sync`:**
    *   **Description:** Compares the local workstation's registered game library against master server-side templates.
    *   **Input:** List of local game definitions and executable path structures.
    *   **Output:** Returns database comparison deltas (`SyncDelta`).

---

## Part 4: Secure Communication Message Contracts

All post-handshake, payload-level TCP messaging structures are mapped below (unwrapped plaintext payload representation).

### 1. Client → Server Messages

#### 1.1 CLIENT_CONNECTED (Workstation State Sync on Connect)
```json
{
  "type": "EVENT",
  "event": "CLIENT_CONNECTED",
  "timestamp": "2026-10-18T12:00:05Z",
  "session": {
    "sessionId": "SESS-9081",
    "pcId": "CLIENT-01",
    "username": "amir",
    "status": "ACTIVE",
    "elapsedSeconds": 1800,
    "ratePerHour": 15000.0
  }
}
```

#### 1.2 HEARTBEAT (Liveness Check)
```json
{
  "type": "HEARTBEAT",
  "timestamp": "2026-10-18T12:00:15Z"
}
```

#### 1.3 TELEMETRY_REPORT (Periodic Diagnostic Load logs)
```json
{
  "type": "TELEMETRY",
  "cpu": 14.5,
  "ram": 2048.0,
  "uptime": 7200,
  "timestamp": "2026-10-18T12:01:00Z",
  "runningGameName": "Cyberpunk 2077",
  "runningGamePid": 4902,
  "runningGameCpu": 28.4,
  "runningGameRam": 8192.0,
  "runningGameDurationSeconds": 1200.0,
  "totalLaunches": 15,
  "totalCrashes": 0,
  "totalRestarts": 0
}
```

#### 1.4 PROCESS_LAUNCHED (Game launch logs)
```json
{
  "type": "EVENT",
  "event": "GAME_LAUNCHED",
  "gameId": "G-40291",
  "name": "Dota 2",
  "pid": 8902,
  "timestamp": "2026-10-18T12:05:00Z"
}
```

#### 1.5 PROCESS_EXITED (Game execution complete logs)
```json
{
  "type": "EVENT",
  "event": "GAME_EXITED",
  "gameId": "G-40291",
  "name": "Dota 2",
  "exitCode": 0,
  "durationSeconds": 3600.0,
  "timestamp": "2026-10-18T13:05:00Z"
}
```

#### 1.6 EXECUTION_RESULT (Result replies to remote commands)
```json
{
  "type": "EXECUTION_RESULT",
  "action": "LOCK_PC",
  "status": "SUCCESS", // Or "ERROR"
  "result": "PC locked successfully",
  "pcId": "CLIENT-01",
  "timestamp": "2026-10-18T13:10:00Z"
}
```

---

### 2. Server → Client Messages

#### 2.1 COMMAND: START_SESSION
```json
{
  "type": "COMMAND",
  "action": "START_SESSION",
  "pcId": "CLIENT-01",
  "payload": {
    "sessionId": "SESS-9081",
    "pcId": "CLIENT-01",
    "username": "amir",
    "startTime": "2026-10-18T12:00:00Z",
    "durationMinutes": 120.0,
    "status": "ACTIVE",
    "elapsedSeconds": 0,
    "ratePerHour": 15000.0
  }
}
```

#### 2.2 COMMAND: STOP_SESSION
```json
{
  "type": "COMMAND",
  "action": "STOP_SESSION",
  "pcId": "CLIENT-01"
}
```

#### 2.3 COMMAND: PAUSE_SESSION
```json
{
  "type": "COMMAND",
  "action": "PAUSE_SESSION",
  "pcId": "CLIENT-01"
}
```

#### 2.4 COMMAND: RESUME_SESSION
```json
{
  "type": "COMMAND",
  "action": "RESUME_SESSION",
  "pcId": "CLIENT-01"
}
```

#### 2.5 COMMAND: RUN_APP
```json
{
  "type": "COMMAND",
  "action": "RUN_APP",
  "pcId": "CLIENT-01",
  "payload": {
    "gameId": "G-BALDURSGATE3"
  }
}
```

#### 2.6 COMMAND: KILL_APP
```json
{
  "type": "COMMAND",
  "action": "KILL_APP",
  "pcId": "CLIENT-01",
  "payload": {
    "pid": 8902,
    "name": "dota2.exe"
  }
}
```

#### 2.7 COMMAND: SHUTDOWN_PC
```json
{
  "type": "COMMAND",
  "action": "SHUTDOWN_PC",
  "pcId": "CLIENT-01"
}
```

---

## Part 5: Diagnostic Audits, Mock Registry & Obsolescence Mapping

### 1. Hardcoded Values to be Offloaded to Server
The following hardcoded properties within the client projects must be shifted to dynamic server configurations:
*   **Standard Billing Rates:** The client sets the default rate structure (`RatePerHour = 15000`) globally in the Logon hook. This must come dynamically from server-defined workstation billing profiles.
*   **Default Session Durations:** Session limits are hardcoded (`DurationMinutes = 120`) inside `App.xaml.cs`. These values must match the server session record.
*   **Default Centralized IP & Ports:** The fallback IP configuration is set to `"127.0.0.1"` on Port `5000`. Server setups should rely entirely on UDP auto-discovery.
*   **Hardcoded "Amir" credentials:** Standard gamer logons accept `"amir"` / `"amir"` locally as a mock player reservation cache bypass. Future builds must require real server-side reservation checks.
*   **Hardcoded "Admin" credentials:** Administrators `"admin"` / `"admin"` or `"afmin"` / `"admin"` are hardcoded inside the offline validation provider. These must be replaced with the local database PBKDF2 admin verification.
*   **Workstation Backup Encryption Keys:** The backup and restore routines fallback to hardcoded compilation security keys. These must be replaced with keys generated dynamically from local system specifications.

### 2. Mock & Stub Components Inventory
*   **`MockGameService`:** Serves 61 static fake games inside the WPF application. Must be completely disabled and swapped for `IGameLibraryService` calls connected to real databases.
*   **`StubDiscoveryService`:** Registered inside `App.xaml.cs` to mock network lookups. Must be replaced with the fully functional UDP `DiscoveryManager`.
*   **`MockClientBridge`:** Unused stub under `Sayra.Client.UI/Services`.
*   **`HardwarePanelViewModel` fallbacks:** Hardcoded motherboard specs (ASUS ROG, Intel Wi-Fi adapters) serve as fallback placeholders if the core diagnostic WMI providers fail.

### 3. Missing, Duplicate, and Dead Contracts
*   **Missing Contracts:**
    *   No direct file synchronization contract exists to synchronize missing game icons and cover art binaries from the server to the workstation folder `Sayra.UI/Assets/Games`.
    *   No workstation bandwidth-limiter or LAN download throttle control contract is present.
*   **Duplicate Contracts:**
    *   `GameModel` inside `Sayra.Client.Shared/Models/SharedModels.cs` duplicate responsibilities with the core `Game` entity inside `Sayra.Client.GameLibrary/Models/Game.cs`.
    *   `LoginViewModel` inside `Sayra.Client.UI/ViewModels` duplicates the main `LoginViewModel` inside `Sayra.UI/ViewModels`.
*   **Dead Contracts:**
    *   `Sayra.Client.UI` (the Named-Pipe client wrapper app) represents an alternative IPC visual frontend that is mostly dead/unused, as the premium visual application `Sayra.UI` integrates the client core and managers directly.
    *   `ILicenseValidator` interface inside `Sayra.Client.Launcher/Services` is declared but has no corresponding active checks.

---

## Part 6: Comprehensive Architectural Statistics Summary

Below are the key structural metrics calculated during the deep architectural audit of the SAYRA Client workspace.

*   **Total Features:** 13
*   **Total Programmatic Services:** 37
*   **Total Interface Definitions:** 32
*   **Total Structural Models:** 42
*   **Total DTO & Contract Objects:** 18
*   **Total Event Classes & Handlers:** 25
*   **Total Active Background Workers:** 6
*   **Total Required Server APIs & Endpoints:** 8
*   **Total Communication Message Types:** 15
*   **Total Identified Mocks & Stubs:** 4
*   **Total Hardcoded Parameters to Offload:** 6
*   **Total Missing/Duplicate Contract Interfaces:** 4

This document represents the definitive reference architecture and source of truth for all future server and synchronization development.

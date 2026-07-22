# SAYRA CLIENT — GAME MODULE SERVER IMPLEMENTATION SPECIFICATION

## 1 Executive Summary

### Why Server Game Module is Required
The SAYRA Client Game Module is a central pillar of the workstation experience, responsible for organizing, validating, and initiating local process execution. However, a client-only library cannot operate in isolation. The server-side companion module is strictly required to:
*   Act as the authoritative database and repository for available game libraries and application profiles.
*   Enforce security policies, license validation, and workstation-specific deployment.
*   Track execution telemetry, gameplay statistics, and active process monitoring for invoicing, security auditing, and parental/owner controls.
*   Handle client-server configuration synchronization to prevent client-side data tampering.

### Client Dependency on Game System
The SAYRA Client depends fully on the Game system to populate its dark-themed dashboard. Without the server supplying valid metadata, categorized game databases, executable directories, and dynamic launch parameters, the client defaults to visual placeholder/mock fallbacks and prevents launching since offline verification relies on previously cached data.

### Current Client Expectations
The client expects the server to:
1.  Establish a master template library of games and application profiles.
2.  Provide sync delta endpoints to dynamically align the client's local JSON database (`games.json`) with server modifications.
3.  Process structural execution events (`GAME_STARTED`, `GAME_CLOSED`, `GAME_CRASHED`) to capture session durations, exit codes, and anomalies.
4.  Handle remote execution commands to initiate (`RUN_APP`) or terminate (`KILL_APP`) processes over a persistent secure TCP socket.

### Missing Server Responsibilities
Currently, the SAYRA Server has not implemented:
*   An active synchronization engine for `SyncDelta` payloads to handle bidirectional comparison.
*   Central APIs to serve metadata, categories, version tracking, and asset content directories.
*   Ingestion streams for process statistics, CPU/RAM telemetry, and crash reports.
*   A persistent database schema matching the client's complex game representation.

---

## 2 Current Client Game Architecture

### Projects
*   **`Sayra.Client.GameLibrary`**
    *   **Purpose:** Manages the local persistent store (`games.json`) and handles validation, CRUD operations, and category mappings.
    *   **Dependencies:** `System.IO`, `System.Text.Json`, `Microsoft.Extensions.Logging`.
*   **`Sayra.Client.Launcher`**
    *   **Purpose:** Encapsulates local application spawning, process handle monitoring, crash detection, auto-recovery retries, and publishes detailed lifecycle event packages.
    *   **Dependencies:** `System.Diagnostics`, `System.IO`, `Microsoft.Extensions.Logging`, `Sayra.Client.GameLibrary`.
*   **`SayraClient` (Headless Agent)**
    *   **Purpose:** Coordinates TCP messaging, listens for incoming remote server commands (like `RUN_APP`), and acts as the event dispatcher bridging the local launcher and the remote server.
    *   **Dependencies:** `Sayra.Client.Launcher`, `Sayra.Client.GameLibrary`, `Sayra.Client.Shared`.
*   **`Sayra.UI` (WPF Application)**
    *   **Purpose:** Binds ViewModels directly to Core interfaces for localized rendering and manual administrative game edits.
    *   **Dependencies:** `Sayra.Client.GameLibrary`, `Sayra.Client.Launcher`.

### Classes & Services
*   **`GameLibraryService`** (implements `IGameLibraryService`)
    *   **Purpose:** Exposes loading, adding, modifying, and deleting games inside the local workspace.
    *   **Responsibilities:** Ensures executable paths exist on additions/updates.
    *   **Dependencies:** `IGameLibraryRepository`.
*   **`GameValidationService`** (implements `IGameValidationService`)
    *   **Purpose:** Runs executable integrity validation.
    *   **Responsibilities:** Evaluates file presence, directory access permissions, launcher support, and updates validation status.
    *   **Dependencies:** `System.IO`.
*   **`GameLauncherService`** (implements `IGameLauncherService`)
    *   **Purpose:** Standard execution coordinator.
    *   **Responsibilities:** Validates active session state, verifies launcher licensing, configures ProcessStartInfo, and registers handles with the monitor.
    *   **Dependencies:** `IProcessMonitorService`, `ILauncherRecoveryService`, `ILicenseValidator`, `ISessionStateProvider`.
*   **`ProcessMonitorService`** (implements `IProcessMonitorService`)
    *   **Purpose:** Active process tracking loop.
    *   **Responsibilities:** Samples CPU and RAM usage percentage metrics every second, raises process exit hooks, and computes crash conditions.
    *   **Dependencies:** `System.Threading.Timer`, `System.Diagnostics.Process`.
*   **`LauncherIntegrationService`** (implements `IHostedService`)
    *   **Purpose:** Bridges background library events to the network layer.
    *   **Responsibilities:** Listens to launcher lifecycle events and translates them into TCP payloads sent to the server.
    *   **Dependencies:** `IGameLauncherService`, `IProcessMonitorService`, `SessionManager`, `IpcServer`, `TcpClientManager`.

### Repositories
*   **`GameLibraryRepository`** (implements `IGameLibraryRepository`)
    *   **Purpose:** Handles low-level file storage.
    *   **Responsibilities:** Performs atomic file replacements (`games.json` ↔ `games.json.bak`) with robust corruption recovery.
    *   **Dependencies:** `System.Text.Json`, `System.IO`.

### Models & DTOs
*   **`Game`** (Domain entity representing a workstation game)
*   **`Application`** (Domain entity representing standard non-game applications)
*   **`GameCategory`** (Sub-model mapping categorical descriptors)
*   **`LaunchProfile`** (Stores launch arguments, paths, and environment settings)
*   **`LaunchOptions`** (Parameters passed during initialization)
*   **`ProcessStatistics`** (Immutable snapshot of process performance)

### Events
*   **`GameLaunching`** (Fired on start request)
*   **`GameStarted`** (Fired once process ID is captured)
*   **`GameExited`** (Fired on clean termination)
*   **`GameCrashed`** (Fired on non-zero exit codes or startup failure)
*   **`GameRestarted`** (Fired during recovery loops)
*   **`GameKilled`** (Fired when forcefully killed)
*   **`LaunchFailed`** (Fired if file is locked or missing)

### Commands
*   **`RUN_APP`** (Remote TCP command initiating execution)
*   **`KILL_APP`** (Remote TCP command terminating process)
*   **`LIST_PROCESSES`** (Remote TCP command querying running games)

---

## 3 Complete Game Data Model

### Model: Game
*   **Name:** `Game`
*   **Fields:**
    *   **`Id`**
        *   Type: `string`
        *   Required: Yes
        *   Nullable: No
        *   Validation: Non-empty GUID or formatted ID
        *   Default Value: `string.Empty` (autogenerated as `Guid.NewGuid().ToString()` on Add)
    *   **`Name`**
        *   Type: `string`
        *   Required: Yes
        *   Nullable: No
        *   Validation: Length between 1 and 200 characters
        *   Default Value: `string.Empty`
    *   **`ExecutablePath`**
        *   Type: `string`
        *   Required: Yes
        *   Nullable: No
        *   Validation: Must be a structurally valid file path
        *   Default Value: `string.Empty`
    *   **`Arguments`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Validation: String format
        *   Default Value: `string.Empty`
    *   **`WorkingDirectory`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Validation: Must be a valid directory path if specified
        *   Default Value: `string.Empty`
    *   **`IconPath`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Validation: Image path format
        *   Default Value: `string.Empty`
    *   **`Category`**
        *   Type: `GameCategory`
        *   Required: Yes
        *   Nullable: No
        *   Validation: Inline object validation
        *   Default Value: New instance of `GameCategory`
    *   **`Enabled`**
        *   Type: `bool`
        *   Required: Yes
        *   Nullable: No
        *   Validation: Boolean values
        *   Default Value: `true`
    *   **`Source`**
        *   Type: `GameSource` (Enum: `Manual` = 0, `Scanner` = 1, `Server` = 2)
        *   Required: Yes
        *   Nullable: No
        *   Validation: Must exist in enum range
        *   Default Value: `GameSource.Manual`
    *   **`CreatedAt`**
        *   Type: `DateTime`
        *   Required: Yes
        *   Nullable: No
        *   Validation: Valid timestamp
        *   Default Value: `DateTime.UtcNow`
    *   **`UpdatedAt`**
        *   Type: `DateTime`
        *   Required: Yes
        *   Nullable: No
        *   Validation: Valid timestamp
        *   Default Value: `DateTime.UtcNow`
    *   **`LaunchProfile`**
        *   Type: `LaunchProfile`
        *   Required: No
        *   Nullable: Yes
        *   Validation: None
        *   Default Value: `null`
    *   **`LogoImage`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`BackgroundImage`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`Launcher`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Validation: Match against supported brands (Steam, Epic, Battle.net, Riot, Ubisoft, EA, Xbox, Custom, GOG)
        *   Default Value: `string.Empty`
    *   **`Developer`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`ReleaseYear`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`Description`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
*   **Relationships:** Has a 1-to-1 relationship with `GameCategory` and `LaunchProfile`.

### Model: Application
*   **Name:** `Application`
*   **Fields:**
    *   **`Id`**
        *   Type: `string`
        *   Required: Yes
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`Name`**
        *   Type: `string`
        *   Required: Yes
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`ExecutablePath`**
        *   Type: `string`
        *   Required: Yes
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`Publisher`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`Version`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`Type`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`

### Model: GameCategory
*   **Name:** `GameCategory`
*   **Fields:**
    *   **`Id`**
        *   Type: `string`
        *   Required: Yes
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`Name`**
        *   Type: `string`
        *   Required: Yes
        *   Nullable: No
        *   Default Value: `string.Empty`

### Model: LaunchProfile
*   **Name:** `LaunchProfile`
*   **Fields:**
    *   **`Arguments`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`WorkingDirectory`**
        *   Type: `string`
        *   Required: No
        *   Nullable: No
        *   Default Value: `string.Empty`
    *   **`EnvironmentVariables`**
        *   Type: `Dictionary<string, string>`
        *   Required: Yes
        *   Nullable: No
        *   Default Value: New empty dictionary

---

## 4 Client Required Server Data

| Data Field | Source in Client | Used By | Purpose |
| :--- | :--- | :--- | :--- |
| **`Id`** | `Game.Id` | `GameLauncherService` | Explicit unique identifier of the game profile. |
| **`Name`** | `Game.Name` | `GameLibraryViewModel` | Renders user-facing title. |
| **`ExecutablePath`** | `Game.ExecutablePath` | `GameValidationService` / `Launcher` | Targets execution binary. |
| **`Arguments`** | `Game.Arguments` | `GameLauncherService` | Custom runtime switches. |
| **`WorkingDirectory`**| `Game.WorkingDirectory` | `GameLauncherService` | Process directory boundary. |
| **`Enabled`** | `Game.Enabled` | `GameLibraryViewModel` / `Launcher` | Checks if workstation plays game. |
| **`Category`** | `Game.Category` | `GameLibraryViewModel` | Organizes games in grid filters. |
| **`Source`** | `Game.Source` | `AdminWorkspaceViewModel` | Audits origin (Server, Local Admin). |
| **`CoverImage`** | `Game.CoverImage` / `IconPath`| `GameLibraryViewModel` | Premium dashboard thumbnail. |
| **`LogoImage`** | `Game.LogoImage` | `GameDetailViewModel` | Game details layout header. |
| **`BackgroundImage`** | `Game.BackgroundImage`| `GameDetailViewModel` | Backdrop behind metadata panel. |
| **`Launcher`** | `Game.Launcher` | `GameLibraryViewModel` | Matches active brand overlay. |
| **`ReleaseYear`** | `Game.ReleaseYear` | `GameLibraryViewModel` | Detailed visual metadata. |
| **`Developer`** | `Game.Developer` | `GameLibraryViewModel` | Studio credit. |
| **`Description`** | `Game.Description` | `GameLibraryViewModel` | Gameplay summary string. |

---

## 5 Game Synchronization Contract

### How Client Receives Games from Server
The workstation and the server maintain synchronous alignments using a transactional delta engine.

### Sync Contract Parameters
*   **Direction:** Server → Client (Authoritative distribution), Client → Server (Local administrative additions)
*   **Data Flow:** Initiated via comparative checksum checks and resolved through list reconciliation payloads.
*   **Sync Trigger:** On successful TCP handshake authentication, workstation re-connection, or administrative refresh action.
*   **Initial Sync:** Server delivers the entire game library catalog template to the client to initialize local configurations.
*   **Incremental Sync:** Handled via the client-side contract model `SyncDelta`:
    *   Server compares list states based on `CalculatedAt` timestamps.
    *   Server compiles lists of ID categories: `AddedServerIds`, `UpdatedServerIds`, and `DeletedServerIds`.
*   **Update Sync:** Working profiles matching `UpdatedServerIds` are completely overwritten inside `games.json` using transactional file system operations.
*   **Delete Sync:** Client deletes local mappings for matches in `DeletedServerIds`.
*   **Failure Handling:** If the synchronisation process encounters a crash or timeout, the transaction rolls back, and the client restores `games.json.bak` automatically.
*   **Offline Behavior:** When offline, synchronization is suspended. The client falls back to locally persistent profiles and continues to run games in an isolated sandbox state.

---

## 6 Required Server APIs

### 1. GET /api/games
*   **API Name:** `GET /api/games`
*   **HTTP Method:** `GET`
*   **Purpose:** Fetches the entire authoritative game and application catalog templates.
*   **Request:** None.
*   **Response:**
    ```json
    [
      {
        "id": "game-101",
        "name": "Counter-Strike 2",
        "executablePath": "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Counter-Strike 2\\game\\bin\\win64\\cs2.exe",
        "arguments": "-secure -perfectworld",
        "workingDirectory": "",
        "iconPath": "assets/images/cs2_cover.jpg",
        "category": { "id": "shooter", "name": "Shooter" },
        "enabled": true,
        "source": 2,
        "logoImage": "assets/images/cs2_logo.png",
        "backgroundImage": "assets/images/cs2_bg.jpg",
        "launcher": "Steam",
        "developer": "Valve",
        "releaseYear": "2023",
        "description": "For over two decades, Counter-Strike has delivered an elite competitive experience..."
      }
    ]
    ```
*   **Authentication:** Requires valid station authorization token inside HTTP headers.
*   **Errors:** `401 Unauthorized` (Token expired or missing).
*   **Used By Client Component:** `GameLibraryViewModel` / `GameLibraryService` during synchronization.

### 2. POST /api/games/sync
*   **API Name:** `POST /api/games/sync`
*   **HTTP Method:** `POST`
*   **Purpose:** Exchanges synchronization delta packages between local clients and the master database.
*   **Request:** Contains a comparative transaction frame.
    ```json
    {
      "lastSyncTimestamp": "2026-10-18T12:00:00Z",
      "stationId": "SAYRA-WORKSTATION-01"
    }
    ```
*   **Response:** Returns a compiled list of additions, updates, and deletes.
    ```json
    {
      "addedServerIds": ["game-105"],
      "updatedServerIds": [],
      "deletedServerIds": ["game-99"],
      "calculatedAt": "2026-10-18T14:30:00Z"
    }
    ```
*   **Authentication:** Station authorization token.
*   **Errors:** `400 Bad Request` (Invalid timestamp sequence).
*   **Used By Client Component:** `IWorkstationSyncService` (via background scheduler).

---

## 7 TCP Commands Related To Games

### 1. Remote Start command: `RUN_APP`
*   **Command Name:** `RUN_APP`
*   **Direction:** Server → Client
*   **Payload:** Includes either a direct identifier or path fallback.
    ```json
    {
      "action": "RUN_APP",
      "payload": {
        "gameId": "game-101"
      }
    }
    ```
*   **Response:** Encrypted transport acknowledgment frame.
    ```json
    {
      "status": "SUCCESS",
      "action": "RUN_APP",
      "message": "Application started"
    }
    ```
*   **Timeout:** 10 Seconds.
*   **Retry:** No automatic retries.
*   **Client Handler:** `AppCommandHandler.HandleRunAppAsync()`
*   **Purpose:** Allows gaming center owners to launch games on target terminals remotely.

### 2. Remote Termination command: `KILL_APP`
*   **Command Name:** `KILL_APP`
*   **Direction:** Server → Client
*   **Payload:** Specifies a PID target or a process string name.
    ```json
    {
      "action": "KILL_APP",
      "payload": {
        "pid": 8412
      }
    }
    ```
*   **Response:**
    ```json
    {
      "status": "SUCCESS",
      "action": "KILL_APP"
    }
    ```
*   **Timeout:** 5 Seconds.
*   **Retry:** Immediate retry once on error.
*   **Client Handler:** `AppCommandHandler.HandleKillAppAsync()`
*   **Purpose:** Instantly closes active processes upon session timeouts or security concerns.

### 3. Remote Audit command: `LIST_PROCESSES`
*   **Command Name:** `LIST_PROCESSES`
*   **Direction:** Server → Client
*   **Payload:** None.
*   **Response:** Serialized array of active tracked process telemetry.
    ```json
    {
      "status": "SUCCESS",
      "action": "LIST_PROCESSES",
      "result": "[{\"Pid\":8412,\"GameId\":\"game-101\",\"Name\":\"cs2\",\"IsRunning\":true,\"CpuUsagePercentage\":14.2,\"RamUsageMb\":4200.5,\"RunningDuration\":\"01:15:20\"}]"
    }
    ```
*   **Timeout:** 5 Seconds.
*   **Retry:** None.
*   **Client Handler:** `AppCommandHandler.HandleListProcesses()`
*   **Purpose:** Scans running activities to ensure system integrity and licensing compliance.

---

## 8 Game Lifecycle Contract

The client process engine tracks game execution through distinct chronological phases:

```
[INSTALLED] ---> [LAUNCHING] ---> [RUNNING] ---> [STOPPING] ---> [STOPPED]
     ^                                                 |               ^
     |                                                 v               |
     +--- [CRASH_RECOVERING] (Retries < 3) <--- [CRASHED]              + (Normal Exit)
```

| State | Trigger | Owner | Event | Synchronization Requirement |
| :--- | :--- | :--- | :--- | :--- |
| **`Installed`** | Validation Passed | `ValidationService` | None | Updates status badge inside UI |
| **`Available`** | Check Playable | `ValidationService` | None | None |
| **`Downloading`** | UNKNOWN | UNKNOWN | UNKNOWN | UNKNOWN |
| **`Updating`** | UNKNOWN | UNKNOWN | UNKNOWN | UNKNOWN |
| **`Launching`** | Execution Request | `GameLauncherService` | `GameLaunching` | Broadcasts IPC frame |
| **`Running`** | Handle Captured | `ProcessMonitor` | `GameStarted` | Sends `GAME_STARTED` TCP event |
| **`Stopping`** | Shutdown Command | `GameLauncherService` | `GameKilled` | Broadcasts process termination |
| **`Stopped`** | Normal Process Exit | `ProcessMonitor` | `GameExited` | Sends `GAME_CLOSED` TCP event |
| **`Crashed`** | Unexpected Exit | `ProcessMonitor` | `GameCrashed` | Sends `GAME_CRASHED` TCP event |
| **`Repairing`** | UNKNOWN | UNKNOWN | UNKNOWN | UNKNOWN |
| **`Missing`** | Binary Missing | `ValidationService` | None | Local interface notification |

---

## 9 Game Launch Contract

### Who Starts Game?
The game launch sequence is initiated exclusively by the client machine, triggered either by physical player interactions on the WPF dashboard or by receiving a remote execution packet (`RUN_APP`) over the secure TCP link.

### Required Information
Before launching, the client verifies and passes the following parameters:
*   **Executable:** `Game.ExecutablePath` (must exist locally).
*   **Arguments:** `Game.Arguments` (e.g. `-novid -high`).
*   **Working Directory:** `Game.WorkingDirectory` (falls back to the binary folder if empty).
*   **Permissions:** Normal user context (unless administrative process demands elevate verb parameters).
*   **Environment:** Integrated custom dictionaries passed inside `LaunchProfile.EnvironmentVariables`.

### Response Mappings
The system maps runtime actions back to listeners:
*   **Success:** Spawns process, captures the PID, registers metrics tracking, and triggers the `GameStarted` event.
*   **Failure:** Fires `LaunchFailed` with detailed reason strings.
*   **Crash:** Triggers `GameCrashed` and delegates recovery handling to the watchdog.

---

## 10 Game Status Reporting

The client tracks and updates process telemetry metrics on every active loop iteration:

*   **Running Game:** Name of the current active process.
*   **Process ID:** Integer system handle (`Pid`).
*   **CPU Usage:** Percentage utilization (`CpuUsagePercentage`).
*   **RAM Usage:** Megabytes consumed in working set (`RamUsageMb`).
*   **Duration:** Elapsed running time (`RunningDuration`).
*   **Crash Count:** Total accumulated crash failures (`TotalCrashes`).
*   **Launch Count:** Total execution starts (`TotalLaunches`).

---

## 11 Events Contract

### Client Published Events

#### 1. `GAME_STARTED`
*   **Event Name:** `GAME_STARTED`
*   **Payload:** Contains execution details.
    ```json
    {
      "type": "EVENT",
      "event": "GAME_STARTED",
      "timestamp": "2026-10-18T12:00:05Z",
      "pcId": "SAYRA-WORKSTATION-01",
      "gameId": "game-101",
      "name": "cs2",
      "details": "PID: 8412, SessionId: SESS-1"
    }
    ```
*   **Trigger:** Raised once the game process handle is successfully registered.
*   **Consumer:** SAYRA Server (for audit trails and session tracking).

#### 2. `GAME_CLOSED`
*   **Event Name:** `GAME_CLOSED`
*   **Payload:** Duration metrics.
    ```json
    {
      "type": "EVENT",
      "event": "GAME_CLOSED",
      "timestamp": "2026-10-18T13:15:25Z",
      "pcId": "SAYRA-WORKSTATION-01",
      "gameId": "game-101",
      "name": "cs2",
      "details": "Duration: 4520 seconds, ExitCode: 0"
    }
    ```
*   **Trigger:** Raised on normal process exit.
*   **Consumer:** SAYRA Server.

#### 3. `GAME_CRASHED`
*   **Event Name:** `GAME_CRASHED`
*   **Payload:** Exit codes and crash description.
    ```json
    {
      "type": "EVENT",
      "event": "GAME_CRASHED",
      "timestamp": "2026-10-18T13:20:00Z",
      "pcId": "SAYRA-WORKSTATION-01",
      "gameId": "game-101",
      "name": "cs2",
      "details": "ExitCode: -1073741819, Reason: Unexpected exit code: -1073741819"
    }
    ```
*   **Trigger:** Process terminates with non-zero exit codes.
*   **Consumer:** SAYRA Server.

### Server Expected Events
The server is expected to process all incoming transactional events mentioned above and update real-time management dashboards accordingly.

---

## 12 Database Requirements For Server

Based strictly on client expectations, the server must support the following relational database schema:

```
+-------------------+             +-------------------+
|     Category      |             |       Game        |
+-------------------+             +-------------------+
| Id (PK)           |<----------- | Id (PK)           |
| Name              |             | Name              |
+-------------------+             | ExecutablePath    |
                                  | Arguments         |
                                  | WorkingDirectory  |
                                  | IconPath          |
                                  | Enabled           |
                                  | Source            |
                                  | CreatedAt         |
                                  | UpdatedAt         |
                                  | LogoImage         |
                                  | BackgroundImage   |
                                  | Launcher          |
                                  | Developer         |
                                  | ReleaseYear       |
                                  | Description       |
                                  | CategoryId (FK)   |
                                  +-------------------+
```

### Required Entities
1.  **`Game`** (Main profile storage entity)
2.  **`Category`** (Groups games and applications)
3.  **`Application`** (Separates system utilities)

### Required Fields
*   **Game:** `Id` (GUID), `Name` (NVARCHAR), `ExecutablePath` (NVARCHAR), `Arguments` (NVARCHAR), `WorkingDirectory` (NVARCHAR), `IconPath` (NVARCHAR), `Enabled` (BIT), `Source` (INT), `CreatedAt` (DATETIME), `UpdatedAt` (DATETIME), `LogoImage` (NVARCHAR), `BackgroundImage` (NVARCHAR), `Launcher` (NVARCHAR), `Developer` (NVARCHAR), `ReleaseYear` (NVARCHAR), `Description` (NVARCHAR), `CategoryId` (FK).
*   **Category:** `Id` (NVARCHAR), `Name` (NVARCHAR).
*   **Application:** `Id` (GUID), `Name` (NVARCHAR), `ExecutablePath` (NVARCHAR), `Publisher` (NVARCHAR), `Version` (NVARCHAR), `Type` (NVARCHAR).

---

## 13 Missing Server Capabilities

| Capability | Why Required | Client Reference | Current State | Implementation Priority |
| :--- | :--- | :--- | :--- | :--- |
| **Sync Delta Engine** | Aligns terminal configurations and game libraries dynamically. | `IWorkstationSyncService` | PARTIAL (Stubbed) | Critical |
| **Telemetry Ingest Stream** | Audits workstation performance and activity logging. | `TelemetryModel` | MISSING | High |
| **Central Assets Storage** | Distributes high-quality logo and cover image assets. | `Game.CoverImage` / `Logo` | MISSING | Medium |
| **Remote CLI Executor** | Dispatches process commands over persistent socket channels. | `AppCommandHandler` | PARTIAL | High |

---

## 14 Server Implementation Checklist

| Scope | Requirement | Status |
| :--- | :--- | :--- |
| **Database** | Complete schema creation for `Games`, `Categories`, and `Applications` | **MISSING** |
| **Models** | Define domain structures matching client representation | **MISSING** |
| **Repositories** | Manage persistent operations for entities | **MISSING** |
| **Services** | Implement sync comparison, remote execution, and telemetry capture | **MISSING** |
| **APIs** | Expose `/api/games` and `/api/games/sync` with authorization | **MISSING** |
| **TCP Commands** | Dispatch `RUN_APP` and `KILL_APP` through the secure transport layer | **PARTIAL** |
| **Synchronization** | Handle differential changes via `SyncDelta` packages | **MISSING** |
| **Events** | Ingest `GAME_STARTED`, `GAME_CLOSED`, and `GAME_CRASHED` streams | **MISSING** |
| **Telemetry** | Capture CPU/RAM performance diagnostics and execution logs | **MISSING** |
| **Testing** | Execute verification tests for all server game services | **MISSING** |

---

## 15 Final Implementation Specification

To make SAYRA Server fully compatible with the Client Game System, the server implementation must execute the following actions:

### Required Components

1.  **Authoritative Database Schema:**
    Establish a high-fidelity SQL/NoSQL schema representation capturing all metadata parameters defined in the domain models (such as `LogoImage`, `Launcher`, and `LaunchProfile`).
2.  **Comparative Differential Sync Engine:**
    Implement JSON sync endpoints (`/api/games/sync`) capable of accepting previous synchronization timestamps, resolving lists differences, and packaging comparative `SyncDelta` models cleanly.
3.  **Real-Time TCP Orchestrator:**
    Implement remote execution handlers over the persistent challenge-response socket connection to formulate and broadcast `RUN_APP` and `KILL_APP` command envelopes.
4.  **Telemetry Data Processing Pipeline:**
    Create asynchronous event handlers to digest and persist process activities, game launches, and execution crash events securely.

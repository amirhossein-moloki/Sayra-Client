# Commercial-Grade Game Launcher Engine
## Sayra Client Desktop Infrastructure (Phase 7 - Part 4)

This document provides a detailed architectural specification of the **Sayra Client Game Launcher Engine** (`Sayra.Client.Launcher`).

---

## 1. Architectural Overview

The Game Launcher Engine acts as the single authoritative component responsible for executing, monitoring, recovering, and controlling games and applications within the Sayra Client ecosystem. It encapsulates all low-level system process management, removing direct usage of `System.Diagnostics.Process.Start()` from other modules, and ensures strict security policies, session awareness, licensing checks, and graceful error recovery.

### Design Principles
- **SOLID Compliance**: High decoupling between systems. For example, `GameLauncherService` validates and starts processes, `ProcessMonitorService` tracks process lifecycles, and `LauncherRecoveryService` handles retries.
- **Decoupled Integrations**: Infrastructure services like IPC and network managers are completely decoupled via event-based subscribers (`LauncherIntegrationService`), resolving circular reference limits.
- **Offline-First & Thread-Safe**: Supports hundreds of concurrent/sequential launches in disconnected environments using thread-safe data structures (`ConcurrentDictionary` and `Interlocked` operations).

---

## 2. Dependency Graph

The module relationships inside the solution are structured as follows:

```
[ Sayra.Client.UI (WPF) ]
         |
         v
 [ SayraClient (Host) ] ---> [ Sayra.Client.Tests ]
         |                           |
         v                           v
[ Sayra.Client.Launcher ] ----------> [ Sayra.Client.GameLibrary ]
         |
         v
[ Sayra.Client.Shared ]
```

---

## 3. Class Diagram & Key Abstractions

### Key Interfaces

```
+---------------------------------------------------------------------------------+
|                                IGameLauncherService                             |
+---------------------------------------------------------------------------------+
| + GameLaunching : event                                                         |
| + GameStarted : event                                                           |
| + GameExited : event                                                            |
| + GameCrashed : event                                                           |
| + GameRestarted : event                                                         |
| + GameKilled : event                                                            |
| + LaunchFailed : event                                                          |
|---------------------------------------------------------------------------------+
| + LaunchGameAsync(gameId, cancellationToken) : Task<bool>                       |
| + LaunchApplicationAsync(path, args, workingDir, runAsAdmin, ct) : Task<bool>  |
| + StopGameAsync(gameId) : Task                                                  |
| + KillGameAsync(gameId) : Task                                                  |
| + RestartGameAsync(gameId) : Task                                               |
| + GetRunningGames() : IEnumerable<ProcessStatistics>                            |
| + GetProcessStatistics(gameId) : ProcessStatistics                              |
| + KillProcessAsync(pid) : Task                                                  |
| + KillProcessByNameAsync(name) : Task                                           |
+---------------------------------------------------------------------------------+

+---------------------------------------------------------------------------------+
|                             IProcessMonitorService                              |
+---------------------------------------------------------------------------------+
| + RegisterProcess(gameId, process, options)                                     |
| + UnregisterProcess(gameId)                                                     |
| + GetRunningProcesses() : IEnumerable<ProcessStatistics>                        |
| + GetProcessStatistics(gameId) : ProcessStatistics                              |
| + IsGameRunning(gameId) : bool                                                  |
| + GetTotalLaunches() : int                                                      |
| + GetTotalCrashes() : int                                                       |
| + GetTotalRestarts() : int                                                      |
+---------------------------------------------------------------------------------+

+---------------------------------------------------------------------------------+
|                             ILauncherRecoveryService                            |
+---------------------------------------------------------------------------------+
| + HandleLaunchFailureAsync(gameId, options, reason, cancellationToken) : Task   |
| + HandleGameCrashAsync(gameId, options, exitCode, cancellationToken) : Task     |
| + ResetRetries(gameId)                                                          |
+---------------------------------------------------------------------------------+
```

---

## 4. Execution Pipelines & Flows

### A. Launch Pipeline Sequence

```
User UI (WPF)            IpcServer             GameLauncherService       SessionState/License      ProcessMonitor
    |                       |                           |                         |                      |
    |--- LAUNCH_GAME ------>|                           |                         |                      |
    |    (GameId)           |--- LaunchGameAsync() ---->|                         |                      |
    |                       |                           |--- ValidateGame() ----->|                      |
    |                       |                           |<-- ValidationResult ----|                      |
    |                       |                           | (Passes: active, valid) |                      |
    |                       |                           |                         |                      |
    |                       |                           |--- ResolveProfile() --->|                      |
    |                       |                           |                         |                      |
    |                       |                           |--- StartProcess() ----->|                      |
    |                       |                           |                         |                      |
    |                       |                           |--- RegisterProcess() ------------------------->| (Start periodic tracking)
    |                       |                           |                         |                      |
    |                       |                           |--- Fire GameStarted --->|                      |
    |                       |<-- Success (True) --------|                         |                      |
    |<-- COMMAND_RESPONSE --|                           |                         |                      |
```

### B. Monitoring and Exit Detection Pipeline

The `ProcessMonitorService` polls the CPU/RAM metrics of registered active processes every 2 seconds. When the process terminates, it triggers the exit detection flow:

```
  Process terminates / Exited event
               |
               v
     Read Exit Code & Duration
               |
               +-----------------------+
               |                       |
      [ExitCode != 0 OR             [ExitCode == 0 AND
       Duration < 5 seconds]        Duration >= 5 seconds]
               |                       |
               v                       v
         (GameCrashed)            (GameExited)
               |                       |
               v                       v
     Increment Crash Count      Graceful Exit Logged
               |                       |
               v                       v
     Trigger RecoveryService     Remove from Active Monitor
               |                       |
               v                       v
      Relaunch Attempt up         Notify SessionManager
        to Max Limit
```

### C. Recovery and Relaunch Pipeline

```
     Crash detected for GameId
                |
                v
     Increment Game Retry Count
                |
                +-----------------------------------------+
                |                                         |
     [Retry Count <= MaxRetries]               [Retry Count > MaxRetries]
                |                                         |
                v                                         v
        Delay 2 Seconds                            Relaunch Aborted
                |                                         |
                v                                         v
     Fire GameRestarted Event                    Raise LaunchFailed Event
                |                                         |
                v                                         v
     Call GameLauncher.Launch()                     Graceful Failure
```

---

## 5. Integration Architecture

### SessionManager Integration
The `SessionManager` implements `ISessionStateProvider` to verify session status before executing a game.
A dedicated `LauncherIntegrationService` runs as a hosted background service to listen to C# events published by `IGameLauncherService`. On event trigger, it coordinates notifications between components:
- **Session Billing & Durations**: Updates active play times and states in `SessionManager`.
- **WPF UI Updates**: Broadcasts live game states over IPC pipes (`IpcServer.BroadcastEventAsync`).
- **Server Orchestration**: Forwards real-time events over the secure TCP Client Socket (`TcpClientManager.SendMessageAsync`) to the Server Admin panel.

### Diagnostics Integration
The `DiagnosticsService` queries `IProcessMonitorService` to fetch:
- Currently active running game's Name, PID, CPU usage, RAM usage, and Elapsed running duration.
- Lifetime launcher telemetry including total launch attempts, crashed games, and recovered restarts.
This satisfies the schema for monitoring synchronized real-time client statuses.

---

## 6. Extended IPC Protocol Specification

The client IPC protocol has been extended to support:
- `LAUNCH_GAME`: Launches game by Game ID.
- `STOP_GAME`: Gracefully stops the game window.
- `RESTART_GAME`: Safely stops and restarts the game pipeline.
- `GET_RUNNING_GAMES`: Returns all running games.
- `GET_RUNNING_STATISTICS`: Exposes detailed live metrics of CPU, RAM, and durations.
- `VALIDATE_EXECUTABLE`: Checks file existence and validity.
- `LAUNCHER_STATUS`: Exposes lifetime engine launches, crashes, and restarts.

---

## 7. Future Extension Points
1. **Dynamic Sandboxing & Resource Isolation**: Integrate custom Windows Job Objects to limit CPU cores or memory allocations of resource-intensive games.
2. **Third-Party Launchers Integration**: Custom profile decorators to hook deep process trees (e.g. tracking launcher wrappers of EA, Ubisoft, and GOG).
3. **Advanced Anti-Cheat & Process Injection Protection**: Intercept runtime API injection and debug hooks during pre-launch integrity phases.

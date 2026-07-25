# PHASE 4 — TRACK 4.2: SECURE GAME LAUNCH PIPELINE IMPLEMENTATION REPORT

**Title:** Technical Implementation & Security Audit Report for SAYRA Secure Game Launch Pipeline
**Track:** Phase 4 — Track 4.2
**Status:** **TRACK 4.2 COMPLETE**
**Date:** October 2024
**Author:** Principal Windows Internals Engineer, .NET 8 Enterprise Developer, and Windows Process Security Specialist

---

## 1. Executive Summary

This report documents the design, architecture, implementation, and testing of **Phase 4 - Track 4.2: Secure Game Launch Pipeline** for the SAYRA Enterprise Windows Client. This track replaces direct and unsafe `Process.Start()` execution of game binaries with an enterprise-grade Win32 process creation pipeline. By isolating low-privilege game execution using Windows access token duplication, secure interactive desktop routing (`winsta0\default`), and proper resource disposal via native `SafeHandle` encapsulation, the system completely seals the station escape boundary.

The implementation is integrated seamlessly with the existing **Track 4.1 (Runtime Foundation & State Machine)**, **Track 4.6 (Game Protection & Policy Evaluation)**, and **Track 4.7 (Kiosk Hardening)**. It includes dual Windows-native and cross-platform fallback pipelines, enabling 100% headless testing and CI runner compatibility.

---

## 2. Implemented Components

The following files and components have been successfully created and integrated in the repository:

### 2.1 Domain Layer (`Sayra.Client.Shared/Runtime/Launch/Domain/`)
*   **Models (`Models/`):**
    *   `LaunchRequest.cs`: Represents the launch command request payload (`GameId`, `ExecutablePath`, `Arguments`, `WorkingDirectory`, `UserId`, `RuntimeSessionId`).
    *   `LaunchProfile.cs`: Defines custom process options (`SandboxPath`, `VirtualRegistryKeys`, `EnvironmentVariables`, `Priority`, `LaunchTimeoutSeconds`).
    *   `LaunchResult.cs`: Represents process creation result metadata (`Success`, `ProcessId`, `ErrorMessage`).
*   **Exceptions (`Exceptions/`):**
    *   `LaunchException.cs`: Base exception for all launch pipeline failures.
    *   `LaunchValidationException.cs`: Raised when process validation (existence, extension, or policies) fails.
    *   `UserSessionUnavailableException.cs`: Raised when no active user console session is detected.
    *   `TokenCreationException.cs`: Raised during access token querying or duplication failures.
    *   `ProcessCreationException.cs`: Raised when native process spawning fails.
*   **Events (`Events/`):**
    *   `LaunchRequestedEvent.cs`: Published when a launch request is initiated.
    *   `LaunchStartedEvent.cs`: Published when the process begins starting.
    *   `LaunchCompletedEvent.cs`: Published when the process successfully launches on the user's desktop.
    *   `LaunchFailedEvent.cs`: Published when any step of the launch sequence fails.

### 2.2 Application Layer (`Sayra.Client.Shared/Runtime/Launch/Application/`)
*   **Interfaces (`Interfaces/`):**
    *   `ISecureLauncher.cs`: Authoritative service orchestrating the launch pipeline (validation, session detection, token duplication, process creation, event publishing, and state management).
    *   `IUserSessionProvider.cs`: Abstract contract for active interactive session discovery.
    *   `IUserTokenService.cs`: Abstract contract for interactive token lookup, validation, and release.
    *   `IProcessCreator.cs`: Abstract contract for platform-isolated low-level process spawning.
    *   `ILaunchValidator.cs`: Contract for pre-launch file-system, extension, integrity, and security policy checks.
    *   `ILaunchProfileProvider.cs`: Contract for resolving individual game launch profiles.
*   **Services (`Services/`):**
    *   `SecureLauncher.cs`: Orchestrator ensuring high-rigor, secure sequencing. It acts strictly as an orchestration service without containing direct Win32 logic.
    *   `LaunchValidator.cs`: Implements pre-launch integrity checks, extension constraints, and policy evaluations (hooking directly into Track 4.6 `IIntegrityValidator` and `IProcessPolicyEvaluator`).
    *   `LaunchProfileProvider.cs`: Builds and resolves default or customized game launch configurations.

### 2.3 Infrastructure Layer (`Sayra.Client.Shared/Runtime/Launch/Infrastructure/`)
*   **Tokens (`Windows/Tokens/`):**
    *   `SafeTokenHandle.cs`: Extends native .NET `SafeHandle` to wrap duplicated interactive primary user tokens, ensuring reliable garbage collection and zero handle leaks.
    *   `UserTokenService.cs`: Windows implementation retrieving interactive user tokens via `WTSQueryUserToken` and duplicating them using `DuplicateTokenEx` (with stubs for non-Windows).
*   **Sessions (`Windows/Sessions/`):**
    *   `UserSessionProvider.cs`: Resolves active user session ID and interactive user identity via `WTSGetActiveConsoleSessionId` and `WTSQuerySessionInformation` (with clean cross-platform stubs).
*   **Process (`Windows/Process/`):**
    *   `ProcessCreator.cs`: Windows-specific process creator that resolves the interactive token environment block via `CreateEnvironmentBlock` / `DestroyEnvironmentBlock`, configures routing parameters to the interactive window station (`lpDesktop = @"winsta0\default"`), and spawns the low-privilege game process directly within Session 1+ using the native `CreateProcessAsUser` API. It cleans up the native process and thread handles immediately. Includes standard `Process.Start` fallback for cross-platform compliance.
*   **Dependency Injection (`DependencyInjection/`):**
    *   `SecureLaunchExtensions.cs`: Provides `AddSecureLaunchServices()` to register all 6 secure launch services cleanly. Integrated directly into `SayraClient/Program.cs`.

### 2.4 Refactored Components
*   `GameLauncherService.cs` inside `Sayra.Client.Launcher`: Refactored to inject `ISecureLauncher` and `IRuntimeSessionManager`. Replaced insecure `Process.Start` with a call to our `ISecureLauncher` pipeline, retrieving the generated PID, and resolving it to a standard `Process` object via `Process.GetProcessById(pid)` to preserve compatibility with downstream monitoring tools.

---

## 3. Windows APIs Used

| API Function | Library | Purpose |
| :--- | :--- | :--- |
| `WTSGetActiveConsoleSessionId` | `kernel32.dll` | Identifies the active interactive user session ID. |
| `WTSQuerySessionInformation` | `wtsapi32.dll` | Retrieves the username and domain of the logged-on user. |
| `WTSQueryUserToken` | `wtsapi32.dll` | Obtains the primary access token of the logged-on interactive user. |
| `DuplicateTokenEx` | `advapi32.dll` | Duplicates the user token as a primary security token for process spawning. |
| `CreateEnvironmentBlock` | `userenv.dll` | Generates the environment block for the interactive user token to ensure subsystem variables map correctly. |
| `DestroyEnvironmentBlock` | `userenv.dll` | Frees the allocated environment block. |
| `CreateProcessAsUser` | `advapi32.dll` | Creates the low-privilege process directly in the target user's interactive context. |
| `CloseHandle` | `kernel32.dll` | Disposes of native process, thread, and duplicated token handles safely. |

---

## 4. Security Improvements

1.  **Elimination of Privilege Escalation Vectors:** Direct `Process.Start()` from a Session 0 Windows Service runs processes with high system-level administrative privileges (`NT AUTHORITY\SYSTEM`). This allows users to easily escape games and gain administrative control over the machine. Replaced with `CreateProcessAsUser` using the restricted user token of the interactive desktop (Session 1+).
2.  **No OS Handle Leaks:** Wrapped the raw token handle inside a secure `SafeTokenHandle` inheriting from `SafeHandle`. The `ProcessCreator` also explicitly closes the native process and thread handles immediately upon creation, eliminating OS handle leaks.
3.  **Strict Security Policy Hooks:** Every launch request is filtered against `IIntegrityValidator` (checking file modifications, open-locks, and signature validity) and `IProcessPolicyEvaluator` (evaluating against process blacklists/whitelists from Track 4.6) before process creation can proceed.
4.  **No Desktop Escapes:** Configured startup structures to map execution target to `winsta0\default`, ensuring games open inside the secure interactive kiosk container rather than background thread states.

---

## 5. Integration Points

*   **Runtime Foundation (Track 4.1):** Integrates directly with `IRuntimeSessionManager` and `IRuntimeStateManager` to transition the station's finite state machine sequentially (`Preparing` -> `Starting` -> `Running` or `Failed`), dispatching standard `RuntimeStateChangedEvent` alongside specialized launch lifecycle events.
*   **Game Protection (Track 4.6):** Leveraged by `LaunchValidator` to run real-time static checks and evaluation rules prior to process spawning.
*   **Process Supervisor (Track 4.3):** Built to be ready for the upcoming Process Supervisor. When the process is spawned, the resulting PID is returned to the supervisor for binding inside Win32 Job Objects.

---

## 6. Tests Added

A complete suite of high-fidelity automated tests has been created inside `Sayra.Client.Configuration.Tests/SecureLaunchTests.cs`:

*   `LaunchAsync_ValidExecutable_ShouldSucceed`: Validates successful secure launch sequence, state transitions, and event emission.
*   `LaunchAsync_MissingExecutable_ShouldThrowLaunchValidationException`: Verifies that launch is blocked and state transitioned to `Failed` if the executable is missing.
*   `LaunchAsync_InvalidPolicyDecision_ShouldBeBlocked`: Confirms that blacklisted applications are immediately rejected before process creation.
*   `LaunchAsync_UserSessionUnavailable_ShouldFailAndThrowCorrectException`: Validates that the system fails gracefully if no active console session is discovered.
*   `LaunchAsync_ProcessSpawningFailure_ShouldTransitionToFailed`: Assures that native process creation failure transitions the environment cleanly to the `Failed` state.

---

## 7. Limitations

*   **Process Supervisor & Job Objects (Track 4.3):** Setting core limits (CPU affinity, memory bounds) and assigning process trees to Job Objects is deferred to Track 4.3 in accordance with scope guidelines.
*   **Overlay Injection (Track 4.5):** Injecting overlays into game screen contexts remains out-of-scope for this track.

---

## 8. Completion Status

### **TRACK 4.2 COMPLETE**

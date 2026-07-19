# UI-Driven Core Development: Screen 01 — Login
## Core Gap Analysis & Implementation Final Report

---

## 1. Login Screen Analysis

The `LoginWindow` (`Sayra.UI/Views/LoginWindow.xaml` and `Sayra.UI/Views/LoginWindow.xaml.cs`) represents the entry portal of the SAYRA workstation terminal application.

### Presentation & Bindings:
- **DataContext**: Direct instantiation of `Sayra.UI.ViewModels.LoginViewModel` in XAML.
- **Visual Controls**:
  - `VideoBackground`: Live MP4 looped cinematic background.
  - `GlassInput`: Beautiful custom text fields with floating icons bound two-way to `Username` and `Password`.
  - `PrimaryButton`: Submits input, bound to `LoginCommand`. Disabled when `IsLoggingIn` is true.
  - `NotificationCard`: Displays toast warning/error/loading messages via `NotificationService.Instance`.
  - Window control stubs (Minimize/Close buttons) on top right.

### Operational Mechanics (Initial State):
Prior to this task, the authentication flow was completely mock-based:
- Standard user `"amir" / "amir"` and administrators `"admin" / "admin"` or `"afmin" / "admin"` were hardcoded inside `LoginViewModel.cs`.
- There was no configured Dependency Injection (DI) system inside the premium WPF client (`Sayra.UI`).
- Logging occurred through local custom static helpers (`GlobalExceptionHandler.LogTrace`).
- No sessions were registered or tracked inside the core `SessionManager` module during client login.

---

## 2. Missing Core Components

Below is the list of missing Core components identified between the finalized premium UI and the underlying .NET 8 Core architecture:

### 1. Client Dependency Injection System
- **Purpose**: Dynamically registers and resolves ViewModels, windows, and core backend service singletons/transients at runtime, ensuring loose coupling and adhering to SOLID.
- **Priority**: High (Blocker)
- **Layer**: Core Application Layer / Presentation Composition Root
- **Dependencies**: `Microsoft.Extensions.DependencyInjection`

### 2. Local Administrator Authentication Integration (`ILocalAdminService`)
- **Purpose**: Authenticates administrative staff using offline PBKDF2 hash matches, constant-time verification, and automatic security lockout thresholds (locked for 5 minutes after 5 failed attempts).
- **Priority**: High
- **Layer**: Local Administration Layer (`Sayra.Client.LocalAdmin`)
- **Dependencies**: `ILocalAdminRepository`, `IPasswordHasher`, `IAdminSessionManager`

### 3. Workstation Configuration Integration (`IClientConfigurationService`)
- **Purpose**: Loads persistent local workstation configurations (`client_config.json`) during application startup to determine local preferences (e.g., Kiosk lockdown, default language, themes).
- **Priority**: Medium
- **Layer**: Local Administration Layer (`Sayra.Client.LocalAdmin`)
- **Dependencies**: `IClientConfigurationRepository`

### 4. Active Station Session Tracking (`SessionManager`)
- **Purpose**: Initializes, pauses, resumes, and ends the active station usage duration, ticking incremental timers, computing elapsed cost rates, and triggering kiosk unlocks.
- **Priority**: High
- **Layer**: Core Session Service Layer (`SayraClient.Services`)
- **Dependencies**: `ILogger<SessionManager>`, `KioskManager`, `TcpClientManager`

### 5. Unified Logging & Serilog integration
- **Purpose**: Integrates the client's custom exception and trace handlers into a standardized rolling-file Serilog framework (`logs/client-.log`) to coordinate error tracing.
- **Priority**: Medium
- **Layer**: Shared Infrastructure Layer
- **Dependencies**: `Serilog`, `Serilog.Sinks.File`

### 6. Dynamic Language Localization Engine (Remaining Work)
- **Purpose**: Translates the Persian/English static strings in the Login window dynamically based on `LocalPreferences.Language` stored in configuration.
- **Priority**: Low
- **Layer**: Presentation Layer
- **Dependencies**: `IClientConfigurationService`

### 7. Active Directory/Server Session Sync Protocol (Remaining Work)
- **Purpose**: Replaces the hardcoded `"amir"` user login with a dynamic remote query to the central LAN server to retrieve active session reservations.
- **Priority**: Medium
- **Layer**: Network Transport Layer
- **Dependencies**: `TcpClientManager`

---

## 3. Implemented Items

During this task, the first **five** highest-priority Core components were successfully implemented and integrated:

### 1. Robust WPF Dependency Injection System
- **Why Selected**: It is the foundation of Clean Architecture. Without it, loose coupling cannot be achieved, and core services cannot be cleanly injected into viewmodels.
- **Problem Solved**: Replaced direct parameterless constructor dependencies with standard constructor injection. Configured Microsoft Dependency Injection container in `App.xaml.cs` to manage the lifecycle of repositories, services, and viewmodels.

### 2. Local Administrator Authentication Integration
- **Why Selected**: Secures local administrative workspace access against brute force attacks.
- **Problem Solved**: Connected the administrative login flow inside `LoginViewModel` directly to `ILocalAdminService`. The application now authenticates staff against the secure cryptographically salted PBKDF2 database on disk and enforces account locking.

### 3. Core Configuration Engine Integration (`IClientConfigurationService`)
- **Why Selected**: Empowers the UI to follow settings stored in the central client configuration rather than hardcoding.
- **Problem Solved**: Added startup configuration parsing in `App.xaml.cs` to load `client_config.json` via `IClientConfigurationService` and log workstation preferences.

### 4. Session Manager Integration (`SessionManager`)
- **Why Selected**: Tracks active usage and ensures automated station lockdown.
- **Problem Solved**: Integrated `"amir"` standard gamer logins to invoke `SessionManager.StartSession`. This starts the session, sets up the default session rate, and triggers local Kiosk lockdown policies.

### 5. Serilog Unified Logger Integration
- **Why Selected**: Simplifies tracing across background services and UI layers.
- **Problem Solved**: Unifed WPF unhandled exception and trace handling (`GlobalExceptionHandler.cs`) to write directly to the central rolling log files (`logs/client-.log`).

---

## 4. Remaining Work

The following components still need to be implemented in future phases to make the Login screen fully integrated:

1. **Remote Session Authenticator**:
   Replace the hardcoded offline gamer account `"amir"` with a real-time authentication call via `TcpClientManager` to validate active central database reservations.
2. **Server Availability Status Widget**:
   A visual indicator on the login screen to render whether the PC client is currently online/connected to the central server (via `ClientStateManager`).
3. **Dynamic Client Theme Switcher**:
   A visual theme loading module that applies Dark/Light skins during start-up according to `LocalPreferences.Theme`.
4. **Multilingual Localization Support**:
   A resource dictionary selector to translate the Persian labels based on `LocalPreferences.Language`.

---

## 5. Validation Results

### Build Status:
- **Debug Configuration**: Compiled with **0 errors** and **0 warnings**.
- **Release Configuration**: Compiled with **0 errors** and **0 warnings**.

### Test Status:
- **Total Tests**: 46
- **Passed**: 46
- **Failed**: 0
- **Duration**: ~10 seconds
- **Validation**: All local admin PBKDF2 hashing, locking, and file persistence tests pass successfully with 100% success rate.

### Architectural Observations:
- **WPF Lifecycle Integrity**: Avoided dangerous `async void OnStartup` anti-patterns by executing asynchronous startup database and configuration loads synchronously using safe `.GetAwaiter().GetResult()` offloading.
- **SOLID Compliance**: Used a constructor dependency injection pattern to supply dependencies to `LoginViewModel`, while exposing parameterless constructor overloads that resolve from the composition root, preserving 100% backward compatibility for XAML designers.
- **Zero Fragile Stubs**: Eliminated fragile `null!` stubbing of `TcpClientManager` by configuring real instance registrations in `App.xaml.cs`'s DI container.

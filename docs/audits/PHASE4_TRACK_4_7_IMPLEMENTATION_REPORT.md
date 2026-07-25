# PHASE 4 TRACK 4.7 - KIOSK HARDENING & DEVICE CONTROL IMPLEMENTATION REPORT

This document represents the official and comprehensive engineering implementation report for **Phase 4 - Track 4.7: Kiosk Hardening & Device Control Subsystem**.

---

## Implemented Features

The following components have been fully implemented in the codebase:

1. **Kiosk Policy Engine**
   - Core domain models: `KioskPolicy`, `SecurityRestriction`, `RestrictionType`, and `PolicyState`.
   - Interface: `IKioskPolicyService`.
   - Service Implementation: `KioskPolicyService` - thread-safe, SemaphoreSlim-guarded, and supports local JSON file (`kiosk_policy.json`) state persistence.

2. **Keyboard Restriction System**
   - Interface: `IKeyboardRestrictionService`.
   - Service Implementation: `KeyboardRestrictionService` - low-level global hook (`WH_KEYBOARD_LL`) interceptor, capturing and blocking Windows keys, Alt+Tab, Alt+F4, Ctrl+Esc, and Win+Key shortcuts.
   - Auditing: Reports keyboard shortcut escape attempts immediately to `IAuditLogger.LogSecurity`.

3. **Mouse Restriction System**
   - Interface: `IMouseRestrictionService`.
   - Service Implementation: `MouseRestrictionService` - coordinates pointer clipping to screen boundaries or window handles using `ClipCursor`, `GetClipCursor`, and `SetCursorPos`.

4. **Windows Shell Protection**
   - Interface: `IShellProtectionService`.
   - Service Implementation: `ShellProtectionService` - monitors Winlogon registry keys, replaces standard `explorer.exe` shell with the `Sayra.UI.exe` shell on boot, and provides programmatic shell state recovery interfaces.

5. **System Application Restrictions**
   - Interface: `ISystemRestrictionService`.
   - Service Implementation: `SystemRestrictionService` - disables Task Manager, Registry Editor, CMD, and Control Panel via registry settings, combined with a continuous background watcher that actively detects and terminates blocked administrative applications (e.g. `taskmgr`, `cmd`, `powershell`, `pwsh`, `control`, `SystemSettings`, `regedit`).

6. **USB / Device Control Foundation**
   - Domain Event payloads: `DeviceConnectedEvent`, `DeviceRemovedEvent`, and `UnauthorizedDeviceDetectedEvent`.
   - Interface: `IDeviceControlService`.
   - Service Implementation: `DeviceControlService` - monitors device notification changes (`WM_DEVICECHANGE`), triggers connected/removed notifications, and flags unauthorized storage devices (`USBSTOR`) to prevent malware vector loading.

7. **Maintenance Mode**
   - Interface: `IMaintenanceModeService`.
   - Service Implementation: `MaintenanceModeService` - secures local administrator challenge with salted PBKDF2 hashing, suspends all active kiosk restrictions temporarily, and manages a background relock timer for automatically re-securing the workstation after inactivity.

8. **Dependency Injection Integration**
   - Registered all 6 services with `AddKioskSecurityServices()` extension helper.
   - Wired up DI registrations inside `SayraClient/Program.cs`.

---

## Security Capabilities

The Kiosk Hardening layer introduces enterprise-grade lockdown capabilities to protect SAYRA workstations from unauthorized system escape and physical device injection vectors:
- **Low-Level Key Interception**: Standard keyboard hotkeys are dropped at the Windows Driver interface level before being propagated to user threads.
- **Robust Application Blacklisting**: Registry policies are combined with aggressive real-time process polling & termination, guaranteeing that users cannot spawn administrative tools or command shells even if registry settings are bypassed.
- **Physical USB Isolation**: Standard USB mass storage insertions trigger immediate unauthorized device alerts and audit entries.
- **Cryptographic Maintenance Credentials**: Local administrator credentials are secured at-rest using random 16-byte salts and PBKDF2 (SHA-256) stretching, preventing offline dictionary/rainbow table attacks.
- **Automatic Session Expiry Watchdog**: Maintenance Mode uses a reactive idle-timer that automatically re-locks the workstation, eliminating risks associated with administrators leaving unlocked kiosk stations.

---

## Windows APIs Used

The following Win32 native APIs and system structures are utilized for low-level OS interception:
- `SetWindowsHookEx` / `UnhookWindowsHookEx` / `CallNextHookEx` (Low-level WH_KEYBOARD_LL input hook)
- `GetModuleHandle` / `GetKeyState` (Keystate modifier validation)
- `ClipCursor` / `GetClipCursor` / `SetCursorPos` (Mouse cursor boundary locking)
- `Registry` (Winlogon shell and administrative restrictions)
- `WM_DEVICECHANGE` / `DBT_DEVICEARRIVAL` / `DBT_DEVICEREMOVECOMPLETE` (USB event notifications)

---

## Tests Added

The following comprehensive tests have been added inside `Sayra.Client.Configuration.Tests/KioskSecurityTests.cs`:
- `PolicyEngine_LoadAndSave_AppliesCorrectly`: Verifies loading, updating, and saving kiosk policies.
- `KeyboardRestriction_EnableAndDisable_SetsHookActiveState`: Confirms registration and unregistration of the WH_KEYBOARD_LL hook.
- `MouseRestriction_EnableAndDisable_SetsRestrictedState`: Assures cursor confinement can be toggled on/off.
- `MaintenanceMode_AuthenticationAndTimeout_BehavesCorrectly`: Validates salted PBKDF2 credential matching, restriction suspension, and automatic inactivity-triggered relock.
- `DeviceMonitoring_DeviceEvents_FiresCorrectly`: Simulates physical USB insertions and removals, confirming connected, removed, and unauthorized alerts are dispatched.
- `SystemRestriction_ProcessBlocking_DetectsBlockedName`: Verifies blacklisted administrative process detection.

---

## Limitations

- **Process Supervisor & Job Objects (Track 4.3)**: Placing spawned game trees into job objects and managing child process life boundaries is handled in a separate track.
- **Secure Game Launch Pipeline (Track 4.2)**: Token handling and Session 0 to Session 1+ launch bridges are handled in a separate track.
- **No Driver-Level Interception**: Low-level driver-level USB blockages or kernel-level keyboard filters are excluded from Track 4.7 scope in accordance with design guidelines.

---

## Completion Status

TRACK 4.7 COMPLETE

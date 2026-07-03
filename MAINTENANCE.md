# Sayra Client - Maintenance & Watchdog

## Watchdog Service

The `WatchdogService` is a background service within the Sayra Client application that ensures:
1. System state is recovered on startup (e.g., reapplying kiosk lockdown if a session was active).
2. Periodic health checks are performed to ensure the system remains in the desired state.

## Auto-Restart Mechanism

To ensure the client process auto-restarts if it crashes, it is designed to run as a **Windows Service**.

### Configuration for Auto-Restart:
When installing the service, configure the Service Control Manager (SCM) recovery options:
- **First failure:** Restart the Service
- **Second failure:** Restart the Service
- **Subsequent failures:** Restart the Service
- **Reset fail count after:** 1 day
- **Restart service after:** 1 minute

### Installation Example (PowerShell):
```powershell
New-Service -Name "SayraClient" -BinaryPathName "C:\Path\To\SayraClient.exe" -DisplayName "Sayra Client Service" -StartupType Automatic
sc.exe failure "SayraClient" reset= 86400 actions= restart/60000/restart/60000/restart/60000
```

## State Persistence

Session state is persisted in `session_state.json` in the application directory. This allows the `RecoveryManager` to restore the active session even after a complete process restart or system reboot.

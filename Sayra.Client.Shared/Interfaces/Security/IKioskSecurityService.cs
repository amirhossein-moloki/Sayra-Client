using System.Threading.Tasks;

namespace Sayra.Client.Shared.Interfaces.Security;

/// <summary>
/// Enforces local system shell locks, keyboard hooks, and secure desktops.
/// </summary>
public interface IKioskSecurityService
{
    bool IsLocked();
    void Lockdown();
    void Unlock();
    void ReapplyPolicies();

    Task EnableKioskLockdownAsync();
    Task DisableKioskLockdownAsync();
    bool IsKeyboardShortcutBlocked(int virtualKeyCode, int modifiers);
    void SpawnSecureDesktop();
    void ReleaseSecureDesktop();

    void EnableKioskMode();
    void DisableKioskMode();
    bool ValidateSecurityState();
    void RepairSecurityPolicy();
}

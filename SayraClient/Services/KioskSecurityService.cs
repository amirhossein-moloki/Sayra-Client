using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Sayra.Client.Shared.Interfaces.Security;
using SayraClient.Security.Windows;

namespace SayraClient.Services;

public class KioskSecurityService : IKioskSecurityService
{
    private readonly ILogger<KioskSecurityService> _logger;
    private readonly SecureDesktopManager _desktopManager;
    private readonly DesktopSessionManager _sessionManager;
    private readonly DesktopSecurityPolicy _securityPolicy;
    private bool _isLocked;

    // Keyboard Hook Fields
    private LowLevelKeyboardProc? _hookCallback;
    private IntPtr _hookId = IntPtr.Zero;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

    // Win32 Structs/PInvokes for keyboard hooks
    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public int vkCode;
        public int scanCode;
        public int flags;
        public int time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    private static extern short GetKeyState(int keyCode);

    public KioskSecurityService(ILogger<KioskSecurityService> logger, IIntegrityValidator? integrityValidator = null)
    {
        _logger = logger;

        var desktopLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<SecureDesktopManager>();
        var sessionLogger = LoggerFactory.Create(builder => builder.AddConsole())
            .CreateLogger<DesktopSessionManager>();

        _securityPolicy = new DesktopSecurityPolicy();
        _desktopManager = new SecureDesktopManager(desktopLogger);

        var validator = integrityValidator ?? new IntegrityValidator(
            LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<IntegrityValidator>(),
            new SessionKeyManager());

        _sessionManager = new DesktopSessionManager(sessionLogger, _desktopManager, _securityPolicy, validator);
    }

    public bool IsLocked() => _isLocked;

    public void Lockdown()
    {
        _logger.LogInformation("Enabling Kiosk/Lockdown mode and applying security barriers...");
        ApplyRestrictions(true);
        RegisterKeyboardHook();
        _isLocked = true;
    }

    public void Unlock()
    {
        _logger.LogInformation("Disabling Kiosk/Lockdown mode and removing security barriers...");
        ApplyRestrictions(false);
        UnregisterKeyboardHook();
        _isLocked = false;

        if (_sessionManager.IsRunning)
        {
            _sessionManager.StopSession();
        }
    }

    public void ReapplyPolicies()
    {
        if (_isLocked)
        {
            _logger.LogDebug("Self-healing: Re-applying kiosk security policies...");
            ApplyRestrictions(true);

            if (_hookId == IntPtr.Zero)
            {
                _logger.LogWarning("Self-healing: Keyboard hook was removed. Re-registering...");
                RegisterKeyboardHook();
            }
        }
    }

    private void ApplyRestrictions(bool lockDown)
    {
        SetTaskManagerDisabled(lockDown);
        SetRegistryEditorDisabled(lockDown);
        SetCmdDisabled(lockDown);
        SetPowerShellDisabled(lockDown);
        SetExplorerRestrictions(lockDown);
    }

    private void SetTaskManagerDisabled(bool disabled)
    {
        SetRegistryPolicy(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableTaskMgr", disabled ? 1 : 0);
    }

    private void SetRegistryEditorDisabled(bool disabled)
    {
        SetRegistryPolicy(@"Software\Microsoft\Windows\CurrentVersion\Policies\System", "DisableRegistryTools", disabled ? 1 : 0);
    }

    private void SetCmdDisabled(bool disabled)
    {
        SetRegistryPolicy(@"Software\Policies\Microsoft\Windows\System", "DisableCMD", disabled ? 1 : 0);
    }

    private void SetExplorerRestrictions(bool disabled)
    {
        SetRegistryPolicy(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoRun", disabled ? 1 : 0);
        SetRegistryPolicy(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoFind", disabled ? 1 : 0);
        SetRegistryPolicy(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoClose", disabled ? 1 : 0);
    }

    private void SetPowerShellDisabled(bool disabled)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            const string keyPath = @"Software\Microsoft\PowerShell\1\ShellIds\Microsoft.PowerShell";

            RegistryKey? key = null;
            bool isHklm = true;
            try
            {
                key = Registry.LocalMachine.CreateSubKey(keyPath, true);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("HKLM write access denied. Falling back to HKCU for PowerShell execution policy.");
                key = Registry.CurrentUser.CreateSubKey(keyPath, true);
                isHklm = false;
            }

            using (key)
            {
                if (key != null)
                {
                    if (disabled)
                    {
                        key.SetValue("ExecutionPolicy", "Restricted", RegistryValueKind.String);
                        _logger.LogInformation("PowerShell policy set to Restricted in {Store}", isHklm ? "HKLM" : "HKCU");
                    }
                    else
                    {
                        key.DeleteValue("ExecutionPolicy", false);
                        _logger.LogInformation("PowerShell policy removed from {Store}", isHklm ? "HKLM" : "HKCU");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set PowerShell execution policy registry keys.");
        }
    }

    private void SetRegistryPolicy(string keyPath, string valueName, int value)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            RegistryKey? key = null;
            bool isHklm = true;
            try
            {
                key = Registry.LocalMachine.CreateSubKey(keyPath, true);
            }
            catch (UnauthorizedAccessException)
            {
                _logger.LogWarning("HKLM write access denied for {Path}. Falling back to HKCU.", keyPath);
                key = Registry.CurrentUser.CreateSubKey(keyPath, true);
                isHklm = false;
            }

            using (key)
            {
                if (key != null)
                {
                    if (value == 0)
                    {
                        if (key.GetValue(valueName) != null)
                        {
                            key.DeleteValue(valueName);
                            _logger.LogInformation("Policy {Policy} removed from {Store}.", valueName, isHklm ? "HKLM" : "HKCU");
                        }
                    }
                    else
                    {
                        var current = key.GetValue(valueName);
                        if (current == null || (int)current != value)
                        {
                            key.SetValue(valueName, value, RegistryValueKind.DWord);
                            _logger.LogInformation("Policy {Policy} set to {Value} in {Store}.", valueName, value, isHklm ? "HKLM" : "HKCU");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set registry policy {Policy} in {Path}.", valueName, keyPath);
        }
    }

    // --- Keyboard Hook Integration ---

    private void RegisterKeyboardHook()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation("[CI/Linux] Keyboard hook registration simulated.");
            return;
        }

        try
        {
            if (_hookId != IntPtr.Zero) return;

            _hookCallback = HookCallback;
            using var currentProcess = Process.GetCurrentProcess();
            using var currentModule = currentProcess.MainModule;
            if (currentModule != null)
            {
                var hMod = GetModuleHandle(currentModule.ModuleName);
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookCallback, hMod, 0);
                if (_hookId == IntPtr.Zero)
                {
                    _logger.LogError("Failed to register low-level keyboard hook. Error: {Error}", Marshal.GetLastWin32Error());
                }
                else
                {
                    _logger.LogInformation("Low-level keyboard hook registered successfully.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering low-level keyboard hook.");
        }
    }

    private void UnregisterKeyboardHook()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

        try
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                _hookCallback = null;
                _logger.LogInformation("Low-level keyboard hook unregistered successfully.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unregistering low-level keyboard hook.");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vkCode = hookStruct.vkCode;

            int modifiers = 0;
            if (IsKeyDown(VK_MENU)) modifiers |= 1;
            if (IsKeyDown(VK_CONTROL)) modifiers |= 2;
            if (IsKeyDown(VK_SHIFT)) modifiers |= 4;

            if (IsKeyboardShortcutBlocked(vkCode, modifiers))
            {
                _logger.LogWarning("Security barrier: Blocked escape shortcut KeyCode={Key}, Modifiers={Mods}", vkCode, modifiers);
                return (IntPtr)1; // Block the propagation
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private static bool IsKeyDown(int vk)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        return (GetKeyState(vk) & 0x8000) != 0;
    }

    // --- IKioskSecurityService required members ---

    public Task EnableKioskLockdownAsync()
    {
        Lockdown();
        return Task.CompletedTask;
    }

    public Task DisableKioskLockdownAsync()
    {
        Unlock();
        return Task.CompletedTask;
    }

    public bool IsKeyboardShortcutBlocked(int virtualKeyCode, int modifiers)
    {
        if (!_isLocked) return false;
        return _securityPolicy.IsShortcutBlocked(virtualKeyCode, modifiers);
    }

    public void SpawnSecureDesktop()
    {
        _logger.LogInformation("Spawning Secure Desktop and launching session...");
        _sessionManager.StartSession("Sayra.UI.exe", "", IntPtr.Zero, () => RepairSecurityPolicy());
    }

    public void ReleaseSecureDesktop()
    {
        _logger.LogInformation("Releasing Secure Desktop...");
        _sessionManager.StopSession();
    }

    public void EnableKioskMode()
    {
        Lockdown();
    }

    public void DisableKioskMode()
    {
        Unlock();
    }

    public bool ValidateSecurityState()
    {
        if (_isLocked)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (_hookId == IntPtr.Zero)
                {
                    _logger.LogWarning("Security validation failed: Low-level keyboard hook was lost.");
                    return false;
                }
            }
        }
        return true;
    }

    public void RepairSecurityPolicy()
    {
        _logger.LogWarning("RepairSecurityPolicy invoked. Re-applying security restrictions...");
        ReapplyPolicies();
    }
}

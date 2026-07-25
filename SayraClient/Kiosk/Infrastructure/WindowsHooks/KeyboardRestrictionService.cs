using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SayraClient.Kiosk.Application.Interfaces;
using SayraClient.Kiosk.Domain.Models;
using Sayra.Client.Shared.Interfaces;

namespace SayraClient.Kiosk.Infrastructure.WindowsHooks;

public class KeyboardRestrictionService : IKeyboardRestrictionService, IDisposable
{
    private readonly IAuditLogger _auditLogger;
    private readonly IKioskPolicyService _policyService;
    private bool _isEnabled;

    private LowLevelKeyboardProc? _hookCallback;
    private IntPtr _hookId = IntPtr.Zero;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_F4 = 0x70;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    private const int VK_SHIFT = 0x10;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12;

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

    public KeyboardRestrictionService(IAuditLogger auditLogger, IKioskPolicyService policyService)
    {
        _auditLogger = auditLogger;
        _policyService = policyService;
    }

    public void EnableKeyboardRestrictions()
    {
        if (_isEnabled) return;
        _isEnabled = true;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            RegisterKeyboardHook();
        }
        else
        {
            _auditLogger.LogOperational("Keyboard hook registration simulated (non-Windows).");
        }

        _auditLogger.LogSecurity("[Kiosk Security] Keyboard restrictions enabled.");
    }

    public void DisableKeyboardRestrictions()
    {
        if (!_isEnabled) return;
        _isEnabled = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            UnregisterKeyboardHook();
        }

        _auditLogger.LogSecurity("[Kiosk Security] Keyboard restrictions disabled.");
    }

    public bool IsKeyboardHookActive()
    {
        return _isEnabled && (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || _hookId != IntPtr.Zero);
    }

    private void RegisterKeyboardHook()
    {
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
                    _auditLogger.LogOperational($"Failed to register low-level keyboard hook. Error: {Marshal.GetLastWin32Error()}");
                }
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Error registering low-level keyboard hook: {ex.Message}");
        }
    }

    private void UnregisterKeyboardHook()
    {
        try
        {
            if (_hookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                _hookCallback = null;
            }
        }
        catch (Exception ex)
        {
            _auditLogger.LogOperational($"Error unregistering low-level keyboard hook: {ex.Message}");
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vkCode = hookStruct.vkCode;

            bool alt = (GetKeyState(VK_MENU) & 0x8000) != 0;
            bool ctrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
            bool win = ((GetKeyState(VK_LWIN) & 0x8000) != 0) || ((GetKeyState(VK_RWIN) & 0x8000) != 0);

            bool isEscapeAttempt = false;
            string shortcutName = "";

            if (_policyService.IsRestrictionEnabled(RestrictionType.Keyboard))
            {
                // Win key
                if (vkCode == VK_LWIN || vkCode == VK_RWIN)
                {
                    isEscapeAttempt = true;
                    shortcutName = "WinKey";
                }
                // Alt + Tab
                else if (vkCode == VK_TAB && alt)
                {
                    isEscapeAttempt = true;
                    shortcutName = "Alt+Tab";
                }
                // Alt + F4
                else if (vkCode == VK_F4 && alt)
                {
                    isEscapeAttempt = true;
                    shortcutName = "Alt+F4";
                }
                // Ctrl + Esc
                else if (vkCode == VK_ESCAPE && ctrl)
                {
                    isEscapeAttempt = true;
                    shortcutName = "Ctrl+Esc";
                }
                // Win + Key (R, E, D, etc.)
                else if (win)
                {
                    isEscapeAttempt = true;
                    shortcutName = $"Win+{(char)vkCode}";
                }
            }

            if (isEscapeAttempt)
            {
                _auditLogger.LogSecurity($"[Kiosk Security] Blocked key/combination: {shortcutName} at {DateTime.UtcNow}");
                return (IntPtr)1; // Consume key press
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        DisableKeyboardRestrictions();
    }
}

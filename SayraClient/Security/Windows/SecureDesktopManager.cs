using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SayraClient.Security.Windows;

public class SecureDesktopManager : IDisposable
{
    private readonly ILogger<SecureDesktopManager> _logger;
    private IntPtr _hSecureDesktop = IntPtr.Zero;
    private IntPtr _hDefaultDesktop = IntPtr.Zero;
    private readonly string _desktopName = "SAYRA_SECURE_DESKTOP";
    private readonly object _lock = new();

    // Win32 Constants
    private const uint DESKTOP_ALL_ACCESS = 0x01FF;
    private const int STARTF_USESHOWWINDOW = 0x00000001;
    private const short SW_SHOW = 5;

    public SecureDesktopManager(ILogger<SecureDesktopManager> logger)
    {
        _logger = logger;
    }

    // P/Invokes
    [DllImport("user32.dll", EntryPoint = "CreateDesktopW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateDesktop(
        string lpszDesktop,
        IntPtr lpszDevice,
        IntPtr pDevMode,
        int dwFlags,
        uint dwDesiredAccess,
        IntPtr lpsa);

    [DllImport("user32.dll", EntryPoint = "OpenDesktopW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr OpenDesktop(
        string lpszDesktop,
        int dwFlags,
        bool fInherit,
        uint dwDesiredAccess);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SwitchDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseDesktop(IntPtr hDesktop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetThreadDesktop(int dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetThreadDesktop(IntPtr hDesktop);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int GetCurrentThreadId();

    [DllImport("advapi32.dll", EntryPoint = "CreateProcessAsUserW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string? lpApplicationName,
        string? lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", EntryPoint = "CreateProcessW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(
        string? lpApplicationName,
        string? lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    public bool CreateSecureDesktop()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation("[CI/Linux] Secure desktop simulated successfully.");
            return true;
        }

        lock (_lock)
        {
            try
            {
                if (_hSecureDesktop != IntPtr.Zero)
                {
                    _logger.LogWarning("Secure desktop already exists.");
                    return true;
                }

                _hDefaultDesktop = OpenDesktop("Default", 0, false, DESKTOP_ALL_ACCESS);
                if (_hDefaultDesktop == IntPtr.Zero)
                {
                    _logger.LogError("Failed to open Default desktop. Error: {Error}", Marshal.GetLastWin32Error());
                }

                _hSecureDesktop = CreateDesktop(_desktopName, IntPtr.Zero, IntPtr.Zero, 0, DESKTOP_ALL_ACCESS, IntPtr.Zero);
                if (_hSecureDesktop == IntPtr.Zero)
                {
                    _hSecureDesktop = OpenDesktop(_desktopName, 0, false, DESKTOP_ALL_ACCESS);
                }

                if (_hSecureDesktop == IntPtr.Zero)
                {
                    _logger.LogError("Failed to create or open secure desktop. Error: {Error}", Marshal.GetLastWin32Error());
                    return false;
                }

                _logger.LogInformation("Secure desktop '{Desktop}' created or opened successfully.", _desktopName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while creating secure desktop.");
                return false;
            }
        }
    }

    public bool SwitchToSecureDesktop()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation("[CI/Linux] Switched thread and view to simulated secure desktop.");
            return true;
        }

        lock (_lock)
        {
            if (_hSecureDesktop == IntPtr.Zero)
            {
                _logger.LogError("Secure desktop is not initialized. Cannot switch.");
                return false;
            }

            try
            {
                bool threadSwitched = SetThreadDesktop(_hSecureDesktop);
                if (!threadSwitched)
                {
                    _logger.LogWarning("Failed to set thread desktop affinity. Error: {Error}", Marshal.GetLastWin32Error());
                }

                bool desktopSwitched = SwitchDesktop(_hSecureDesktop);
                if (!desktopSwitched)
                {
                    _logger.LogError("Failed to switch active user desktop view. Error: {Error}", Marshal.GetLastWin32Error());
                    return false;
                }

                _logger.LogInformation("Switched to secure desktop context successfully.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception switching to secure desktop.");
                return false;
            }
        }
    }

    public bool SwitchToDefaultDesktop()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation("[CI/Linux] Switched back to Default desktop.");
            return true;
        }

        lock (_lock)
        {
            try
            {
                IntPtr hDefault = _hDefaultDesktop;
                if (hDefault == IntPtr.Zero)
                {
                    hDefault = OpenDesktop("Default", 0, false, DESKTOP_ALL_ACCESS);
                }

                if (hDefault != IntPtr.Zero)
                {
                    SetThreadDesktop(hDefault);
                    SwitchDesktop(hDefault);
                    _logger.LogInformation("Switched back to Default desktop view.");
                    return true;
                }
                else
                {
                    _logger.LogError("Default desktop handle is invalid. Cannot switch back.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception switching back to default desktop.");
                return false;
            }
        }
    }

    public bool LaunchProcessInSecureDesktop(string executablePath, string arguments, IntPtr userToken, out int spawnedPid)
    {
        spawnedPid = 0;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation("[CI/Linux] Spawning process '{Path}' {Args} in simulated secure desktop.", executablePath, arguments);
            // Simulate a dummy PID for testing
            spawnedPid = 9999;
            return true;
        }

        lock (_lock)
        {
            try
            {
                var si = new STARTUPINFO();
                si.cb = Marshal.SizeOf(typeof(STARTUPINFO));
                si.lpDesktop = _desktopName;
                si.dwFlags = STARTF_USESHOWWINDOW;
                si.wShowWindow = SW_SHOW;

                var pi = new PROCESS_INFORMATION();

                bool success;
                if (userToken != IntPtr.Zero)
                {
                    success = CreateProcessAsUser(
                        userToken,
                        executablePath,
                        arguments,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        0,
                        IntPtr.Zero,
                        System.IO.Path.GetDirectoryName(executablePath),
                        ref si,
                        out pi);
                }
                else
                {
                    // Correct desktop assignment using native CreateProcess with lpDesktop structure
                    success = CreateProcess(
                        executablePath,
                        arguments,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        false,
                        0,
                        IntPtr.Zero,
                        System.IO.Path.GetDirectoryName(executablePath),
                        ref si,
                        out pi);
                }

                if (!success)
                {
                    _logger.LogError("Failed to launch process '{Path}' in secure desktop. Error: {Error}", executablePath, Marshal.GetLastWin32Error());
                    return false;
                }

                spawnedPid = pi.dwProcessId;
                _logger.LogInformation("Successfully spawned process PID {Pid} inside secure desktop '{Desktop}'.", spawnedPid, _desktopName);

                if (pi.hProcess != IntPtr.Zero) CloseHandle(pi.hProcess);
                if (pi.hThread != IntPtr.Zero) CloseHandle(pi.hThread);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception spawning process in secure desktop.");
                return false;
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    public void DestroySecureDesktop()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        lock (_lock)
        {
            try
            {
                SwitchToDefaultDesktop();

                if (_hSecureDesktop != IntPtr.Zero)
                {
                    if (_hDefaultDesktop != IntPtr.Zero)
                    {
                        SetThreadDesktop(_hDefaultDesktop);
                    }

                    bool closed = CloseDesktop(_hSecureDesktop);
                    if (!closed)
                    {
                        _logger.LogWarning("Failed to close secure desktop handle. Error: {Error}", Marshal.GetLastWin32Error());
                    }
                    _hSecureDesktop = IntPtr.Zero;
                }

                if (_hDefaultDesktop != IntPtr.Zero)
                {
                    CloseDesktop(_hDefaultDesktop);
                    _hDefaultDesktop = IntPtr.Zero;
                }

                _logger.LogInformation("Secure desktop context cleaned up safely.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during secure desktop destruction.");
            }
        }
    }

    public void Dispose()
    {
        DestroySecureDesktop();
    }
}

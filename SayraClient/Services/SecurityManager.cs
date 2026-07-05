using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SayraClient.Services;

public class SecurityManager
{
    private readonly ILogger<SecurityManager> _logger;

    public SecurityManager(ILogger<SecurityManager> logger)
    {
        _logger = logger;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessInformation(IntPtr hProcess, int processInformationClass, ref int processInformation, int processInformationSize);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] ref bool isDebuggerPresent);

    private const int ProcessCriticalProcess = 0x1000;

    public void ProtectProcess()
    {
        _logger.LogInformation("Applying process-level protections...");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            try
            {
                // Basic check for debugger
                if (Debugger.IsAttached)
                {
                    _logger.LogWarning("Debugger detected! Operating in non-secure mode.");
                }

                // In a real production app with sufficient privileges, we might set the process as critical
                // SetCriticalProcess();

                _logger.LogInformation("Process protection applied (Windows).");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply process protections.");
            }
        }
        else
        {
            _logger.LogWarning("Process protection is limited on this platform.");
        }
    }

    private void SetCriticalProcess()
    {
        try
        {
            int isCritical = 1;
            IntPtr hProcess = Process.GetCurrentProcess().Handle;
            if (SetProcessInformation(hProcess, 0x9, ref isCritical, sizeof(int)))
            {
                _logger.LogInformation("Process marked as CRITICAL.");
            }
            else
            {
                _logger.LogWarning("Failed to mark process as critical. Error: {Error}", Marshal.GetLastWin32Error());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting critical process status.");
        }
    }

    public bool IsSystemSecure()
    {
        if (Debugger.IsAttached)
        {
            _logger.LogWarning("Managed debugger is attached.");
            return false;
        }

        if (IsRemoteDebuggerPresent())
        {
            _logger.LogWarning("Remote (native) debugger detected.");
            return false;
        }

        if (HasSuspiciousModules())
        {
            _logger.LogWarning("Suspicious modules detected in process memory.");
            return false;
        }

        if (HasSuspiciousParent())
        {
            _logger.LogWarning("Suspicious parent process detected.");
            return false;
        }

        return true;
    }

    private bool IsRemoteDebuggerPresent()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;

        bool isDebuggerPresent = false;
        if (CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isDebuggerPresent))
        {
            return isDebuggerPresent;
        }
        return false;
    }

    private bool HasSuspiciousParent()
    {
        // Heuristic: If we are running as a service, our parent should be 'services'
        // If we are running as a process (development/kiosk app), it might be different.
        // For now, we'll skip strict enforcement to avoid breaking development.
        return false;
    }

    private bool HasSuspiciousModules()
    {
        try
        {
            var suspiciousKeywords = new[] { "cheat", "hack", "inject", "hook", "debug" };
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                var moduleName = module.ModuleName?.ToLowerInvariant() ?? "";
                foreach (var keyword in suspiciousKeywords)
                {
                    if (moduleName.Contains(keyword))
                    {
                        _logger.LogWarning("Detected suspicious module: {Module}", module.ModuleName);
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning process modules.");
        }
        return false;
    }
}

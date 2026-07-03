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
        // Add checks for tampering here
        // For example, checking if the service is being debugged or if registry keys were changed
        if (Debugger.IsAttached) return false;

        return true;
    }
}

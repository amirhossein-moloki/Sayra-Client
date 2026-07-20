using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class PowerManagementService : IPowerManagementService
{
    private readonly ILogger<PowerManagementService> _logger;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool LockWorkStation();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

    public event EventHandler<PowerActionEventArgs>? ActionExecuting;
    public event EventHandler<PowerActionEventArgs>? ActionExecuted;
    public event EventHandler<PowerActionFailedEventArgs>? ActionFailed;

    public PowerManagementService(ILogger<PowerManagementService> logger)
    {
        _logger = logger;
    }

    private void OnActionExecuting(string action)
    {
        _logger.LogInformation("Executing power action: {Action}", action);
        ActionExecuting?.Invoke(this, new PowerActionEventArgs(action));
    }

    private void OnActionExecuted(string action)
    {
        _logger.LogInformation("Power action completed successfully: {Action}", action);
        ActionExecuted?.Invoke(this, new PowerActionEventArgs(action));
    }

    private void OnActionFailed(string action, Exception ex)
    {
        _logger.LogError(ex, "Power action failed: {Action}", action);
        ActionFailed?.Invoke(this, new PowerActionFailedEventArgs(action, ex));
    }

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        OnActionExecuting("RESTART");
        try
        {
            await Task.Delay(500, cancellationToken);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunCommand("shutdown", "/r /f /t 0");
            }
            else
            {
                _logger.LogWarning("Restart is only fully supported on Windows.");
            }
            OnActionExecuted("RESTART");
        }
        catch (Exception ex)
        {
            OnActionFailed("RESTART", ex);
            throw;
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken = default)
    {
        OnActionExecuting("SHUTDOWN");
        try
        {
            await Task.Delay(500, cancellationToken);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunCommand("shutdown", "/s /f /t 0");
            }
            else
            {
                _logger.LogWarning("Shutdown is only fully supported on Windows.");
            }
            OnActionExecuted("SHUTDOWN");
        }
        catch (Exception ex)
        {
            OnActionFailed("SHUTDOWN", ex);
            throw;
        }
    }

    public async Task LogoffAsync(CancellationToken cancellationToken = default)
    {
        OnActionExecuting("LOGOFF");
        try
        {
            await Task.Delay(500, cancellationToken);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // EWX_LOGOFF = 0, EWX_FORCE = 4
                if (!ExitWindowsEx(0 | 4, 0))
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error, "Failed to log off workstation via Win32 API.");
                }
            }
            else
            {
                _logger.LogWarning("Logoff is only fully supported on Windows.");
            }
            OnActionExecuted("LOGOFF");
        }
        catch (Exception ex)
        {
            OnActionFailed("LOGOFF", ex);
            throw;
        }
    }

    public async Task LockWorkstationAsync(CancellationToken cancellationToken = default)
    {
        OnActionExecuting("LOCK");
        try
        {
            await Task.Delay(500, cancellationToken);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!LockWorkStation())
                {
                    int error = Marshal.GetLastWin32Error();
                    throw new System.ComponentModel.Win32Exception(error, "Failed to lock workstation via Win32 API.");
                }
            }
            else
            {
                _logger.LogWarning("Lock workstation is only fully supported on Windows.");
            }
            OnActionExecuted("LOCK");
        }
        catch (Exception ex)
        {
            OnActionFailed("LOCK", ex);
            throw;
        }
    }

    private void RunCommand(string filename, string arguments)
    {
        var psi = new ProcessStartInfo(filename, arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(psi);
    }
}

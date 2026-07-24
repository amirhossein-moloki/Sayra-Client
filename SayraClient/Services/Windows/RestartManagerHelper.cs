using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services.Windows;

public interface IRestartManagerHelper
{
    void RegisterForRestart(string commandLineArguments = "");
}

public class RestartManagerHelper : IRestartManagerHelper
{
    private readonly ILogger<RestartManagerHelper> _logger;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegisterApplicationRestart(string pwzCommandLine, uint dwFlags);

    public RestartManagerHelper(ILogger<RestartManagerHelper> logger)
    {
        _logger = logger;
    }

    public void RegisterForRestart(string commandLineArguments = "")
    {
        if (!OperatingSystem.IsWindows())
        {
            _logger.LogInformation("Not running on Windows. Skipping Windows Restart Manager registration.");
            return;
        }

        try
        {
            // Register application restart with Windows Restart Manager
            // dwFlags = 0 allows restart on crash, hang, updates, and reboots.
            int hr = RegisterApplicationRestart(commandLineArguments, 0);
            if (hr == 0)
            {
                _logger.LogInformation("Successfully registered SAYRA Client with Windows Restart Manager. Args: '{Args}'", commandLineArguments);
            }
            else
            {
                _logger.LogWarning("Failed to register with Windows Restart Manager. HRESULT: {HResult}", hr);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error registering with Windows Restart Manager.");
        }
    }
}

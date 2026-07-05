using Microsoft.Extensions.Logging;
using SayraClient.Models;

namespace SayraClient.Services;

public class RecoveryManager
{
    private readonly ILogger<RecoveryManager> _logger;
    private readonly SessionManager _sessionManager;
    private readonly KioskManager _kioskManager;

    public RecoveryManager(ILogger<RecoveryManager> logger, SessionManager sessionManager, KioskManager kioskManager)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _kioskManager = kioskManager;
    }

    public void RecoverState()
    {
        _logger.LogInformation("Recovering system state...");

        if (_sessionManager.CurrentStatus == "ACTIVE")
        {
            _logger.LogInformation("Active session detected, reapplying lockdown.");
            _kioskManager.Lockdown();
        }
        else
        {
            _logger.LogInformation("No active session, ensuring system is unlocked.");
            _kioskManager.Unlock();
        }

        // Re-enforce other security settings if necessary
        EnforceSecuritySettings();
    }

    private void EnforceSecuritySettings()
    {
        _logger.LogInformation("Enforcing security settings...");
        // This is where we ensure registry keys, file permissions, etc., are correct.
        // KioskManager already handles the registry for Task Manager in Lockdown/Unlock.
    }
}

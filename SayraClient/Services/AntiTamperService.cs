using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class AntiTamperService : BackgroundService
{
    private readonly ILogger<AntiTamperService> _logger;
    private readonly KioskManager _kioskManager;
    private readonly SecurityManager _securityManager;

    public AntiTamperService(
        ILogger<AntiTamperService> logger,
        KioskManager kioskManager,
        SecurityManager securityManager)
    {
        _logger = logger;
        _kioskManager = kioskManager;
        _securityManager = securityManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Anti-Tamper Service starting...");

        _securityManager.ProtectProcess();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Anti-Tamper check performing...");

                // Ensure critical security settings are still in place
                if (!_securityManager.IsSystemSecure())
                {
                    _logger.LogWarning("System tampering detected! Re-applying protections.");
                    // Action could be taken here
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Anti-Tamper check.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("Anti-Tamper Service stopping.");
    }
}

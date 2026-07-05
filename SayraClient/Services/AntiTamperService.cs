using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class AntiTamperService : BackgroundService
{
    private readonly ILogger<AntiTamperService> _logger;
    private readonly KioskManager _kioskManager;
    private readonly SecurityManager _securityManager;
    private readonly IntegrityValidator _integrityValidator;
    private readonly IpcServer _ipcServer;

    public AntiTamperService(
        ILogger<AntiTamperService> logger,
        KioskManager kioskManager,
        SecurityManager securityManager,
        IntegrityValidator integrityValidator,
        IpcServer ipcServer)
    {
        _logger = logger;
        _kioskManager = kioskManager;
        _securityManager = securityManager;
        _integrityValidator = integrityValidator;
        _ipcServer = ipcServer;
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
                    _logger.LogWarning("System tampering detected!");
                    await _ipcServer.BroadcastEventAsync(Sayra.Client.Shared.Ipc.IpcMessageType.SECURITY_BREACH_DETECTED, new Sayra.Client.Shared.Models.SecurityEventPayload
                    {
                        EventType = "TAMPER_DETECTED",
                        Severity = "High",
                        Description = "Process security check failed (Debugger or suspicious modules detected)."
                    });
                }

                // Integrity check for core binary (simplified example)
                // In production, the expected hash would be fetched from a secure remote or local manifest
                // string corePath = typeof(Program).Assembly.Location;
                // _integrityValidator.VerifyFileIntegrity(corePath, "EXPECTED_SHA256_HASH");

                _kioskManager.ReapplyPolicies();
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

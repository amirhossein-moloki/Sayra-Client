using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SayraClient.Services;

public class DependencyValidator : IDependencyValidator
{
    private readonly ILogger<DependencyValidator> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;

    public DependencyValidator(
        ILogger<DependencyValidator> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    public Task ValidateDependenciesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Beginning dependency environment validation...");

        // 1. Verify and create required directories
        string[] criticalDirectories = { "logs", "Data", "Data/Backups" };
        foreach (var dir in criticalDirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string path = Path.Combine(AppContext.BaseDirectory, dir);
            try
            {
                if (!Directory.Exists(path))
                {
                    _logger.LogInformation("Required directory '{Directory}' missing. Creating...", dir);
                    Directory.CreateDirectory(path);
                }
                else
                {
                    _logger.LogDebug("Verified directory '{Directory}' exists.", dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FATAL: Failed to verify/create critical directory '{Directory}'.", path);
                throw new IOException($"Critical environment folder '{path}' could not be initialized.", ex);
            }
        }

        // 2. Validate Windows permissions & capabilities (if on Windows)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _logger.LogInformation("Verifying Windows capabilities and administrative privileges...");
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);

                if (!isAdmin)
                {
                    _logger.LogWarning("ALERT: Application is not executing with Administrator privileges. Some kiosk and process locking operations may fail.");
                    // In real enterprise deployment, we can choose to throw or just log a strong warning:
                    // throw new UnauthorizedAccessException("SAYRA Kiosk Client requires elevated Administrator permissions.");
                }
                else
                {
                    _logger.LogInformation("Success: Administrative privilege verified.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to inspect Windows security roles.");
            }
        }
        else
        {
            _logger.LogWarning("Running on non-Windows OS platform '{OS}'. Bypassing Windows capability validation.", RuntimeInformation.OSDescription);
        }

        // 3. Verify that critical dependent services exist in DI container
        _logger.LogInformation("Verifying DI registration of critical core dependency services...");
        Type[] criticalCoreServices = {
            typeof(IConfiguration),
            typeof(ClientStateManager),
            typeof(SessionManager),
            typeof(KioskManager)
        };

        foreach (var svcType in criticalCoreServices)
        {
            var resolved = _serviceProvider.GetService(svcType);
            if (resolved == null)
            {
                _logger.LogCritical("FATAL: Required service '{ServiceType}' is not registered in the DI Container.", svcType.Name);
                throw new InvalidOperationException($"Critical startup service dependency '{svcType.Name}' is missing from container registration.");
            }
        }

        _logger.LogInformation("Dependency environment validation completed successfully.");
        return Task.CompletedTask;
    }

    public Task ValidateConfigurationAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Beginning application configuration schema validation...");

        // 1. Verify Server Discovery Configuration
        var udpPortStr = _configuration["ServerDiscovery:UdpPort"];
        if (string.IsNullOrWhiteSpace(udpPortStr) || !int.TryParse(udpPortStr, out int udpPort) || udpPort <= 0 || udpPort > 65535)
        {
            _logger.LogError("FATAL CONFIG: ServerDiscovery:UdpPort is invalid or out of range: '{Value}'", udpPortStr);
            throw new ArgumentOutOfRangeException("ServerDiscovery:UdpPort", "UdpPort must be a valid integer port between 1 and 65535.");
        }

        // 2. Verify Heartbeat Interval
        var heartbeatSecondsStr = _configuration["ServerConfig:HeartbeatIntervalSeconds"];
        if (string.IsNullOrWhiteSpace(heartbeatSecondsStr) || !int.TryParse(heartbeatSecondsStr, out int seconds) || seconds <= 0)
        {
            _logger.LogWarning("CONFIG WARNING: ServerConfig:HeartbeatIntervalSeconds is invalid or negative: '{Value}'. Defaulting to 10s.", heartbeatSecondsStr);
        }

        _logger.LogInformation("Application configuration validation completed successfully.");
        return Task.CompletedTask;
    }
}

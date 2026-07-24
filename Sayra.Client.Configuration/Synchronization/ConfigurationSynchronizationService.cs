using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Configuration.Conflict;
using Sayra.Client.Configuration.Models;
using Sayra.Client.Configuration.Rollback;
using Sayra.Client.Configuration.Storage;
using Sayra.Client.Configuration.Validation;
using Sayra.Client.Configuration.Versioning;
using Sayra.Client.LocalAdmin.Models;
using Sayra.Client.LocalAdmin.Storage;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.OfflineQueue;
using Sayra.Client.OfflineQueue.Models;

namespace Sayra.Client.Configuration.Synchronization;

public class ConfigurationSynchronizationService : IConfigurationSynchronizationService
{
    private readonly IConfigurationApiClient _apiClient;
    private readonly ConfigurationValidator _validator;
    private readonly ConfigurationSignatureValidator _signatureValidator;
    private readonly ConfigurationVersionManager _versionManager;
    private readonly ConfigurationDeltaEngine _deltaEngine;
    private readonly ConfigurationConflictResolver _conflictResolver;
    private readonly ConfigurationRollbackManager _rollbackManager;
    private readonly ConfigurationApplyService _applyService;
    private readonly IClientConfigurationRepository _localRepository;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConfigurationSynchronizationService>? _logger;

    private readonly string _activePath;
    private readonly string _backupPath;
    private readonly string _tempPath;

    public event EventHandler? SyncStarted;
    public event EventHandler? SyncCompleted;
    public event EventHandler<string>? SyncFailed;

    public ConfigurationSynchronizationService(
        IConfigurationApiClient apiClient,
        ConfigurationValidator validator,
        ConfigurationSignatureValidator signatureValidator,
        ConfigurationVersionManager versionManager,
        ConfigurationDeltaEngine deltaEngine,
        ConfigurationConflictResolver conflictResolver,
        ConfigurationRollbackManager rollbackManager,
        ConfigurationApplyService applyService,
        IClientConfigurationRepository localRepository,
        IServiceProvider serviceProvider,
        ILogger<ConfigurationSynchronizationService>? logger = null,
        string? activePath = null,
        string? backupPath = null,
        string? tempPath = null)
    {
        _apiClient = apiClient;
        _validator = validator;
        _signatureValidator = signatureValidator;
        _versionManager = versionManager;
        _deltaEngine = deltaEngine;
        _conflictResolver = conflictResolver;
        _rollbackManager = rollbackManager;
        _applyService = applyService;
        _localRepository = localRepository;
        _serviceProvider = serviceProvider;
        _logger = logger;

        string baseDir = Path.Combine(AppContext.BaseDirectory, "Data", "Configuration");
        _activePath = activePath ?? Path.Combine(baseDir, "client_config.json");
        _backupPath = backupPath ?? Path.Combine(baseDir, "client_config.json.bak");
        _tempPath = tempPath ?? Path.Combine(baseDir, "client_config.json.tmp");
    }

    private void LogAudit(string eventType, string message, Dictionary<string, object>? properties = null)
    {
        try
        {
            var auditLogger = _serviceProvider.GetService<IAuditLogger>();
            if (auditLogger != null)
            {
                var props = properties ?? new Dictionary<string, object>();
                props["Event"] = eventType;
                auditLogger.LogAudit($"{eventType}: {message}", props);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, $"Failed to log audit event '{eventType}' via IAuditLogger.");
        }
    }

    private async Task QueueSyncFailureEventAsync(string errorReason)
    {
        try
        {
            var offlineQueue = _serviceProvider.GetService<IOfflineQueueManager>();
            if (offlineQueue != null)
            {
                var clientEvent = new ClientEvent
                {
                    EventType = "CONFIG_SYNC_FAILED",
                    Payload = JsonSerializer.Serialize(new
                    {
                        Reason = errorReason,
                        Timestamp = DateTime.UtcNow,
                        ActiveVersion = _versionManager.CurrentVersion
                    }),
                    Priority = QueuePriority.HIGH
                };
                await offlineQueue.AddEventAsync(clientEvent);
                _logger?.LogInformation("Queued CONFIG_SYNC_FAILED event inside Offline Queue.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to queue sync failure event inside Offline Queue.");
        }
    }

    public async Task<bool> PushAndApplyAsync(ConfigurationPackage package, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Initiating configuration push application process...");
        SyncStarted?.Invoke(this, EventArgs.Empty);
        LogAudit("CONFIG_SYNC_STARTED", $"Push synchronization started for Version: {package?.Version}");

        if (package == null)
        {
            string error = "Configuration package is null.";
            SyncFailed?.Invoke(this, error);
            LogAudit("CONFIG_VALIDATION_FAILED", error);
            return false;
        }

        // 1. Digital Signature Verification
        if (!_signatureValidator.VerifySignature(package))
        {
            string error = $"RSA digital signature verification failed for package version '{package.Version}'.";
            SyncFailed?.Invoke(this, error);
            LogAudit("CONFIG_SIGNATURE_FAILED", error, new() { { "Version", package.Version } });
            await QueueSyncFailureEventAsync(error);
            return false;
        }

        // 2. Version Verification
        if (!_versionManager.ValidateVersion(package.Version, out var versionError))
        {
            string error = $"Version verification failed: {versionError}";
            SyncFailed?.Invoke(this, error);
            // Don't treat "already up to date" as a severe validation failure audit, but log if downgrade attack
            if (versionError.Contains("Downgrade"))
            {
                LogAudit("CONFIG_VALIDATION_FAILED", error, new() { { "Version", package.Version } });
            }
            return false;
        }

        try
        {
            // Load current config to merge or patch against
            ClientConfiguration currentConfig = await _localRepository.LoadConfigurationAsync() ?? new ClientConfiguration();

            ClientConfiguration finalConfig;

            if (string.Equals(package.PayloadType, "Full", StringComparison.OrdinalIgnoreCase))
            {
                // 3. Schema Validation on Full Payload
                if (!_validator.Validate(package.Payload, out var schemaError))
                {
                    string error = $"Schema validation failed for full configuration payload: {schemaError}";
                    SyncFailed?.Invoke(this, error);
                    LogAudit("CONFIG_VALIDATION_FAILED", error, new() { { "Version", package.Version } });
                    await QueueSyncFailureEventAsync(error);
                    return false;
                }

                var serverConfig = JsonSerializer.Deserialize<ClientConfiguration>(package.Payload);
                if (serverConfig == null)
                {
                    string error = "Deserialized server configuration is null.";
                    SyncFailed?.Invoke(this, error);
                    LogAudit("CONFIG_VALIDATION_FAILED", error);
                    return false;
                }

                // 4. Conflict Resolution
                _logger?.LogInformation("Resolving configuration policy conflicts...");
                LogAudit("CONFIG_CONFLICT", "Resolving Server vs Local policy conflicts");
                finalConfig = _conflictResolver.Resolve(currentConfig, serverConfig);
            }
            else if (string.Equals(package.PayloadType, "Delta", StringComparison.OrdinalIgnoreCase))
            {
                _logger?.LogInformation("Processing delta configuration update...");
                var deltas = JsonSerializer.Deserialize<List<ConfigurationDelta>>(package.Payload);
                if (deltas == null || deltas.Count == 0)
                {
                    _logger?.LogInformation("Empty delta payload. Merging successfully.");
                    finalConfig = currentConfig;
                }
                else
                {
                    // Clone active config so we don't mutate local repository state in-memory before safe apply
                    var clonedConfig = JsonSerializer.Deserialize<ClientConfiguration>(JsonSerializer.Serialize(currentConfig)) ?? new ClientConfiguration();

                    if (!_deltaEngine.ApplyDeltas(clonedConfig, deltas, out var deltaError))
                    {
                        _logger?.LogWarning($"Failed to apply deltas: {deltaError}. Falling back to full synchronization...");
                        // Request full config from server
                        var fullPackage = await _apiClient.FetchLatestPackageAsync(0, cancellationToken);
                        if (fullPackage != null && string.Equals(fullPackage.PayloadType, "Full", StringComparison.OrdinalIgnoreCase))
                        {
                            return await PushAndApplyAsync(fullPackage, cancellationToken);
                        }

                        string error = $"Delta sync failed and fallback full sync was unavailable: {deltaError}";
                        SyncFailed?.Invoke(this, error);
                        LogAudit("CONFIG_VALIDATION_FAILED", error);
                        await QueueSyncFailureEventAsync(error);
                        return false;
                    }

                    finalConfig = clonedConfig;
                }
            }
            else
            {
                string error = $"Unsupported payload type '{package.PayloadType}'.";
                SyncFailed?.Invoke(this, error);
                LogAudit("CONFIG_VALIDATION_FAILED", error);
                return false;
            }

            // 5. Atomic Apply
            bool applied = await _applyService.ApplyAtomicAsync(_activePath, _backupPath, _tempPath, finalConfig, cancellationToken);

            if (applied)
            {
                // Record the applied version
                long oldVersion = _versionManager.CurrentVersion;
                _versionManager.RecordVersionChange(package.Version, "Applied");

                LogAudit("CONFIG_VERSION_CHANGED", $"Configuration version changed from {oldVersion} to {package.Version}", new()
                {
                    { "OldVersion", oldVersion },
                    { "NewVersion", package.Version }
                });

                LogAudit("CONFIG_SYNC_COMPLETED", $"Configuration applied successfully. Active version is {package.Version}.");
                _logger?.LogInformation($"Successfully completed synchronization process to version: {package.Version}");
                SyncCompleted?.Invoke(this, EventArgs.Empty);
                return true;
            }
            else
            {
                // Trigger auto-rollback if apply failed
                _logger?.LogWarning("Atomic apply failed. Executing automatic rollback...");
                LogAudit("CONFIG_ROLLBACK", "Atomic apply failed. Triggering automatic rollback.");

                if (_rollbackManager.Rollback(_activePath, _backupPath, out var rollbackError))
                {
                    _logger?.LogInformation("Rollback executed successfully.");
                }
                else
                {
                    _logger?.LogError($"Rollback failed: {rollbackError}");
                }

                string error = "Failed to atomically apply configuration.";
                SyncFailed?.Invoke(this, error);
                await QueueSyncFailureEventAsync(error);
                return false;
            }
        }
        catch (Exception ex)
        {
            string error = $"Unexpected error during configuration sync: {ex.Message}";
            _logger?.LogError(ex, error);
            SyncFailed?.Invoke(this, error);
            await QueueSyncFailureEventAsync(error);
            return false;
        }
    }

    public async Task<bool> PullAndApplyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            long currentVersion = _versionManager.CurrentVersion;
            _logger?.LogInformation($"Checking with server for configuration updates. Current local version: {currentVersion}");

            var package = await _apiClient.FetchLatestPackageAsync(currentVersion, cancellationToken);
            if (package == null)
            {
                _logger?.LogInformation("Workstation configuration is already up to date.");
                return true;
            }

            return await PushAndApplyAsync(package, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or System.Net.Http.HttpRequestException or System.Net.Sockets.SocketException)
        {
            string error = $"Network connection failure during Pull Sync: {ex.Message}";
            _logger?.LogWarning(error);
            SyncFailed?.Invoke(this, error);
            await QueueSyncFailureEventAsync(error);
            return false;
        }
        catch (Exception ex)
        {
            string error = $"Unexpected error during Pull Sync: {ex.Message}";
            _logger?.LogError(ex, error);
            SyncFailed?.Invoke(this, error);
            await QueueSyncFailureEventAsync(error);
            return false;
        }
    }

    public Task<bool> ManualSyncAsync(CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Manual configuration sync triggered.");
        return PullAndApplyAsync(cancellationToken);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Sayra.Client.Shared.Models.Recovery.Policies;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Thread-safe, production-grade configuration provider for resilience subsystems.
    /// Manages file load/save, atomic dynamic memory replacements, environment overrides, reloads, validation, and migration.
    /// </summary>
    public class ResilienceConfigurationProvider : IResilienceConfigurationProvider, IPolicyProvider, IConfigurationReloadService
    {
        private readonly ILogger<ResilienceConfigurationProvider> _logger;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly IConfigurationValidator _validator;
        private readonly string _configFilePath;
        private readonly string _backupFilePath;
        private readonly object _stateLock = new();

        private ResilienceConfiguration _currentConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="ResilienceConfigurationProvider"/> class.
        /// </summary>
        public ResilienceConfigurationProvider(
            ILogger<ResilienceConfigurationProvider> logger,
            IEventDispatcher eventDispatcher,
            IConfigurationValidator validator,
            string? configFilePath = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
            _validator = validator ?? throw new ArgumentNullException(nameof(validator));

            var path = configFilePath ?? Path.Combine(AppContext.BaseDirectory, "Data", "resilience_config.json");
            _configFilePath = NormalizeAndValidatePath(path);
            _backupFilePath = _configFilePath + ".bak";

            // Load initial configuration following deterministic precedence:
            // 1. Built-in defaults
            // 2. JSON configuration file
            // 3. Environment variables
            _currentConfiguration = LoadInitialConfiguration();
        }

        /// <inheritdoc />
        public ResilienceConfiguration CurrentConfiguration
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentConfiguration;
                }
            }
        }

        /// <inheritdoc />
        public async Task UpdateConfigurationAsync(ResilienceConfiguration configuration, CancellationToken cancellationToken = default)
        {
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            _logger.LogInformation("Administrative update triggered for resilience configuration...");

            // Apply precedence: Built-in and JSON are already merged into 'configuration',
            // now apply environment overrides and then apply runtime overrides (this is the configuration we are updating)
            ApplyEnvironmentOverrides(configuration);

            var validationResult = _validator.Validate(configuration);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors);
                _logger.LogError("Validation failed for administrative configuration update: {Errors}", errors);
                _eventDispatcher.Dispatch(new ConfigurationValidationFailedEvent(errors, Guid.NewGuid().ToString(), DateTime.UtcNow));
                throw new InvalidOperationException($"Invalid configuration: {errors}");
            }

            lock (_stateLock)
            {
                _currentConfiguration = configuration;
            }

            await SaveConfigurationWithAtomicReplaceAsync(configuration, cancellationToken);

            var correlationId = Guid.NewGuid().ToString();
            _eventDispatcher.Dispatch(new ConfigurationReloadedEvent(configuration, correlationId, DateTime.UtcNow));
            _logger.LogInformation("Administrative configuration update applied atomically and persisted.");
        }

        /// <inheritdoc />
        public Task<RecoveryPolicy> GetPolicyAsync(string subsystemName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(subsystemName)) throw new ArgumentException("Subsystem name cannot be empty.", nameof(subsystemName));

            lock (_stateLock)
            {
                var policy = _currentConfiguration.SelfHealing.SubsystemPolicies?
                    .FirstOrDefault(p => p.SubsystemName.Equals(subsystemName, StringComparison.OrdinalIgnoreCase));

                if (policy != null) return Task.FromResult(policy);

                policy = _currentConfiguration.RecoveryPolicy.CustomPolicies?
                    .FirstOrDefault(p => p.SubsystemName.Equals(subsystemName, StringComparison.OrdinalIgnoreCase));

                if (policy != null) return Task.FromResult(policy);

                var defaultPolicy = _currentConfiguration.SelfHealing.SubsystemPolicies?
                    .FirstOrDefault(p => p.SubsystemName.Equals("Default", StringComparison.OrdinalIgnoreCase))
                    ?? new RecoveryPolicy
                    {
                        SubsystemName = "Default",
                        IsEnabled = true,
                        Priority = RecoveryPriority.Normal,
                        DefaultAction = RecoveryActionType.RestartWorker,
                        Retry = new RetryPolicy()
                    };

                return Task.FromResult(defaultPolicy);
            }
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<RecoveryPolicy>> GetAllPoliciesAsync(CancellationToken cancellationToken = default)
        {
            lock (_stateLock)
            {
                var list = new List<RecoveryPolicy>();
                if (_currentConfiguration.SelfHealing.SubsystemPolicies != null)
                {
                    list.AddRange(_currentConfiguration.SelfHealing.SubsystemPolicies);
                }
                if (_currentConfiguration.RecoveryPolicy.CustomPolicies != null)
                {
                    foreach (var p in _currentConfiguration.RecoveryPolicy.CustomPolicies)
                    {
                        if (!list.Any(existing => existing.SubsystemName.Equals(p.SubsystemName, StringComparison.OrdinalIgnoreCase)))
                        {
                            list.Add(p);
                        }
                    }
                }
                return Task.FromResult<IReadOnlyList<RecoveryPolicy>>(list);
            }
        }

        /// <inheritdoc />
        public async Task SavePolicyAsync(RecoveryPolicy policy, CancellationToken cancellationToken = default)
        {
            if (policy == null) throw new ArgumentNullException(nameof(policy));
            if (string.IsNullOrWhiteSpace(policy.SubsystemName)) throw new ArgumentException("Policy SubsystemName cannot be empty.", nameof(policy));

            _logger.LogInformation("Dynamic policy update requested for subsystem '{SubsystemName}'", policy.SubsystemName);

            ResilienceConfiguration configCopy;
            lock (_stateLock)
            {
                var json = JsonSerializer.Serialize(_currentConfiguration);
                configCopy = JsonSerializer.Deserialize<ResilienceConfiguration>(json) ?? new ResilienceConfiguration();
            }

            if (configCopy.SelfHealing.SubsystemPolicies == null) configCopy.SelfHealing.SubsystemPolicies = new List<RecoveryPolicy>();
            var index = configCopy.SelfHealing.SubsystemPolicies.FindIndex(p => p.SubsystemName.Equals(policy.SubsystemName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                configCopy.SelfHealing.SubsystemPolicies[index] = policy;
            }
            else
            {
                if (configCopy.RecoveryPolicy.CustomPolicies == null) configCopy.RecoveryPolicy.CustomPolicies = new List<RecoveryPolicy>();
                var customIndex = configCopy.RecoveryPolicy.CustomPolicies.FindIndex(p => p.SubsystemName.Equals(policy.SubsystemName, StringComparison.OrdinalIgnoreCase));
                if (customIndex >= 0)
                {
                    configCopy.RecoveryPolicy.CustomPolicies[customIndex] = policy;
                }
                else
                {
                    configCopy.SelfHealing.SubsystemPolicies.Add(policy);
                }
            }

            var validationResult = _validator.Validate(configCopy);
            if (!validationResult.IsValid)
            {
                var errors = string.Join("; ", validationResult.Errors);
                _logger.LogError("Policy update rejected due to validation failure: {Errors}", errors);
                _eventDispatcher.Dispatch(new ConfigurationValidationFailedEvent(errors, Guid.NewGuid().ToString(), DateTime.UtcNow));
                throw new InvalidOperationException($"Invalid policy: {errors}");
            }

            lock (_stateLock)
            {
                _currentConfiguration = configCopy;
            }

            await SaveConfigurationWithAtomicReplaceAsync(configCopy, cancellationToken);

            var correlationId = Guid.NewGuid().ToString();
            _eventDispatcher.Dispatch(new PolicyUpdatedEvent(policy.SubsystemName, policy, correlationId, DateTime.UtcNow));
            _logger.LogInformation("Policy for '{SubsystemName}' updated and applied atomically.", policy.SubsystemName);
        }

        /// <inheritdoc />
        public async Task<bool> ReloadAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Configuration reload requested...");

            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _logger.LogWarning("Resilience configuration file does not exist at {Path}. Reverting to built-in defaults...", _configFilePath);
                    var defaults = CreateDefaultConfiguration();
                    ApplyEnvironmentOverrides(defaults);

                    await SaveConfigurationWithAtomicReplaceAsync(defaults, cancellationToken);
                    lock (_stateLock)
                    {
                        _currentConfiguration = defaults;
                    }
                    _eventDispatcher.Dispatch(new ConfigurationLoadedEvent(defaults, Guid.NewGuid().ToString(), DateTime.UtcNow));
                    return true;
                }

                string content = await File.ReadAllTextAsync(_configFilePath, cancellationToken);
                var loaded = JsonSerializer.Deserialize<ResilienceConfiguration>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    WriteIndented = true
                });

                if (loaded == null)
                {
                    throw new InvalidOperationException("Failed to deserialize configuration. JSON was empty or malformed.");
                }

                // 2. Validate version compatibility and apply migration pipeline
                loaded = MigrateConfigurationIfRequired(loaded);

                // 3. Apply Environment Variable overrides
                ApplyEnvironmentOverrides(loaded);

                // 4. Validate first!
                var validationResult = _validator.Validate(loaded);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join("; ", validationResult.Errors);
                    _logger.LogError("Configuration reload failed due to validation errors: {Errors}. Keeping previous valid configuration active.", errors);
                    _eventDispatcher.Dispatch(new ConfigurationValidationFailedEvent(errors, Guid.NewGuid().ToString(), DateTime.UtcNow));
                    return false;
                }

                // 5. Replace atomically
                lock (_stateLock)
                {
                    _currentConfiguration = loaded;
                }

                var correlationId = Guid.NewGuid().ToString();
                // 6. Publish event only after successful replacement
                _eventDispatcher.Dispatch(new ConfigurationReloadedEvent(loaded, correlationId, DateTime.UtcNow));
                _logger.LogInformation("Resilience configuration reloaded successfully. Applied atomically.");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to reload resilience configuration. Keeping previous valid configuration active.");
                _eventDispatcher.Dispatch(new ConfigurationValidationFailedEvent(ex.Message, Guid.NewGuid().ToString(), DateTime.UtcNow));
                return false;
            }
        }

        private ResilienceConfiguration LoadInitialConfiguration()
        {
            _logger.LogInformation("Loading initial resilience configuration...");

            try
            {
                string dir = Path.GetDirectoryName(_configFilePath) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // Precedence 1: Built-in defaults
                var config = CreateDefaultConfiguration();

                if (File.Exists(_configFilePath))
                {
                    // Precedence 2: JSON configuration file
                    try
                    {
                        string content = File.ReadAllText(_configFilePath);
                        var jsonConfig = JsonSerializer.Deserialize<ResilienceConfiguration>(content, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (jsonConfig != null)
                        {
                            config = MigrateConfigurationIfRequired(jsonConfig);
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        _logger.LogError(jsonEx, "Failed to read JSON config file on startup. Trying backup...");
                        if (File.Exists(_backupFilePath))
                        {
                            try
                            {
                                string content = File.ReadAllText(_backupFilePath);
                                var backupConfig = JsonSerializer.Deserialize<ResilienceConfiguration>(content, new JsonSerializerOptions
                                {
                                    PropertyNameCaseInsensitive = true
                                });
                                if (backupConfig != null)
                                {
                                    config = MigrateConfigurationIfRequired(backupConfig);
                                    _logger.LogInformation("Restored configuration from backup file on startup.");
                                }
                            }
                            catch (Exception backupEx)
                            {
                                _logger.LogError(backupEx, "Failed to load backup file on startup. Using default.");
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("No existing resilience configuration file. Creating and persisting defaults...");
                    // Persist initial default configuration
                    string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_configFilePath, json);
                }

                // Precedence 3: Environment variables overrides
                ApplyEnvironmentOverrides(config);

                var validationResult = _validator.Validate(config);
                if (!validationResult.IsValid)
                {
                    var errors = string.Join("; ", validationResult.Errors);
                    _logger.LogError("Initial resilience configuration validation failed: {Errors}. Reverting to built-in defaults.", errors);
                    var defaults = CreateDefaultConfiguration();
                    ApplyEnvironmentOverrides(defaults);
                    _eventDispatcher.Dispatch(new ConfigurationLoadedEvent(defaults, Guid.NewGuid().ToString(), DateTime.UtcNow));
                    return defaults;
                }

                _eventDispatcher.Dispatch(new ConfigurationLoadedEvent(config, Guid.NewGuid().ToString(), DateTime.UtcNow));
                _logger.LogInformation("Initial resilience configuration loaded and validated successfully.");
                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during initial configuration loading. Using built-in fallback defaults.");
                var defaults = CreateDefaultConfiguration();
                _eventDispatcher.Dispatch(new ConfigurationLoadedEvent(defaults, Guid.NewGuid().ToString(), DateTime.UtcNow));
                return defaults;
            }
        }

        private async Task SaveConfigurationWithAtomicReplaceAsync(ResilienceConfiguration configuration, CancellationToken cancellationToken)
        {
            var tempPath = _configFilePath + ".tmp";
            try
            {
                string dir = Path.GetDirectoryName(_configFilePath) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                // 1. Write to temporary file first
                string json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempPath, json, cancellationToken);

                // 2. Generate backup of currently existing file if it exists
                if (File.Exists(_configFilePath))
                {
                    File.Copy(_configFilePath, _backupFilePath, true);
                }

                // 3. Atomically replace/rename temporary file to original file
                if (File.Exists(tempPath))
                {
                    File.Move(tempPath, _configFilePath, true);
                }

                _logger.LogInformation("Resilience configuration atomically persisted and backed up successfully.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to atomically persist configuration to file '{Path}'. Preserving backup.", _configFilePath);
                try
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                }
                catch { /* Ignore */ }
            }
        }

        private ResilienceConfiguration MigrateConfigurationIfRequired(ResilienceConfiguration loaded)
        {
            if (string.IsNullOrWhiteSpace(loaded.SchemaVersion))
            {
                _logger.LogWarning("Schema version missing in loaded configuration. Upgrading automatically to 1.0.0.");
                loaded.SchemaVersion = "1.0.0";
                return loaded;
            }

            var cleanVersion = loaded.SchemaVersion.Trim();
            if (cleanVersion == "1.0.0")
            {
                return loaded;
            }

            // Older supported versions should be upgraded automatically
            if (cleanVersion == "0.9.0" || cleanVersion == "0.8.0")
            {
                _logger.LogWarning("Older schema version found: '{FoundVersion}'. Migrating and upgrading automatically to '1.0.0'.", cleanVersion);
                loaded.SchemaVersion = "1.0.0";
                loaded.Description += " (Migrated to 1.0.0)";
                return loaded;
            }

            // Unknown future versions must fail safely
            throw new InvalidOperationException($"Incompatible configuration version '{cleanVersion}'. Migration pipeline only supports versions <= 1.0.0.");
        }

        private void ApplyEnvironmentOverrides(ResilienceConfiguration configuration)
        {
            _logger.LogInformation("Applying environment overrides...");

            var envDeduction = Environment.GetEnvironmentVariable("SAYRA_RESILIENCE_HEALTHMONITOR_BASE_DEDUCTION");
            if (!string.IsNullOrEmpty(envDeduction) && double.TryParse(envDeduction, out var val))
            {
                _logger.LogWarning("Overriding HealthMonitor.BaseFailureDeduction via Environment: {Val}", val);
                configuration.HealthMonitor.BaseFailureDeduction = val;
            }

            var envAttempts = Environment.GetEnvironmentVariable("SAYRA_RESILIENCE_SELFHEALING_MAX_ATTEMPTS");
            if (!string.IsNullOrEmpty(envAttempts) && int.TryParse(envAttempts, out var attempts))
            {
                _logger.LogWarning("Overriding SelfHealing.MaxAttempts via Environment: {Val}", attempts);
                configuration.SelfHealing.MaxAttempts = attempts;
            }
        }

        private string NormalizeAndValidatePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) throw new ArgumentException("Configuration file path cannot be empty.");

            // Validate path normalization and prevent directory traversal (such as containing "..")
            var fullPath = Path.GetFullPath(rawPath);

            if (rawPath.Contains(".."))
            {
                throw new UnauthorizedAccessException($"Directory traversal attack blocked! Path cannot contain relative navigation: {rawPath}");
            }

            return fullPath;
        }

        private ResilienceConfiguration CreateDefaultConfiguration()
        {
            var config = new ResilienceConfiguration
            {
                SchemaVersion = "1.0.0",
                Description = "Default Production Resilience Configuration Profile",
                HealthMonitor = new HealthMonitorOptions(),
                SelfHealing = new SelfHealingOptions
                {
                    IsEnabled = true,
                    MaxAttempts = 5,
                    AttemptsResetDuration = TimeSpan.FromMinutes(10),
                    SubsystemPolicies = new List<RecoveryPolicy>
                    {
                        new()
                        {
                            SubsystemName = "Database",
                            IsEnabled = true,
                            Priority = RecoveryPriority.Critical,
                            DefaultAction = RecoveryActionType.ReconnectDatabase,
                            Retry = new RetryPolicy { MaxRetries = 3, InitialDelay = TimeSpan.FromSeconds(1), BackoffStrategy = BackoffStrategy.ExponentialWithJitter },
                            Cooldown = new CooldownPolicy { CooldownDuration = TimeSpan.FromSeconds(5), EvaluationWindow = TimeSpan.FromSeconds(30), FailureThreshold = 2 }
                        },
                        new()
                        {
                            SubsystemName = "Network",
                            IsEnabled = true,
                            Priority = RecoveryPriority.High,
                            DefaultAction = RecoveryActionType.ReconnectTcp,
                            Retry = new RetryPolicy { MaxRetries = 2, InitialDelay = TimeSpan.FromSeconds(2), BackoffStrategy = BackoffStrategy.Linear }
                        },
                        new()
                        {
                            SubsystemName = "PolicyEngine",
                            IsEnabled = true,
                            Priority = RecoveryPriority.Normal,
                            DefaultAction = RecoveryActionType.ReloadConfiguration,
                            Dependency = new DependencyPolicy
                            {
                                PreRecoveryDependencies = new List<string> { "Database" },
                                FailClosedOnDependencyFailure = true
                            }
                        },
                        new()
                        {
                            SubsystemName = "FleetManager",
                            IsEnabled = true,
                            Priority = RecoveryPriority.Normal,
                            DefaultAction = RecoveryActionType.RestartBackgroundServices
                        },
                        new()
                        {
                            SubsystemName = "Default",
                            IsEnabled = true,
                            Priority = RecoveryPriority.Normal,
                            DefaultAction = RecoveryActionType.RestartWorker
                        }
                    }
                },
                RecoveryPolicy = new RecoveryPolicyOptions(),
                CrashRecovery = new CrashRecoveryOptions(),
                ResourceMonitor = new ResourceMonitorOptions(),
                SecurityHardening = new SecurityHardeningOptions(),
                GracefulShutdown = new GracefulShutdownOptions
                {
                    StopWorkTimeout = TimeSpan.FromMilliseconds(5),
                    StopDownloadsTimeout = TimeSpan.FromMilliseconds(5),
                    DrainQueuesTimeout = TimeSpan.FromMilliseconds(5),
                    FlushLogsTimeout = TimeSpan.FromMilliseconds(5),
                    PersistStatesTimeout = TimeSpan.FromMilliseconds(5),
                    StopWorkersTimeout = TimeSpan.FromMilliseconds(5),
                    CloseDatabaseTimeout = TimeSpan.FromMilliseconds(5),
                    OverallTimeout = TimeSpan.FromSeconds(10)
                },
                Diagnostics = new RecoveryDiagnosticsOptions(),
                Watchdog = new WatchdogOptions()
            };

            return config;
        }
    }
}

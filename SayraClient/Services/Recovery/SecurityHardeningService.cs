using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Recovery;
using Sayra.Client.Shared.Interfaces.Security;
using Sayra.Client.Shared.Models.Recovery;
using Sayra.Client.Shared.Models.Recovery.Events;
using Sayra.Client.Shared.UpdatePlatform.Application.Interfaces;

namespace SayraClient.Services.Recovery
{
    /// <summary>
    /// Enterprise-grade security hardening and integrity validation service.
    /// This engine continuously verifies the cryptographic integrity and authenticity of the SAYRA client and its trusted assets.
    /// </summary>
    public class SecurityHardeningService : ISecurityHardeningService
    {
        private readonly ILogger<SecurityHardeningService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IEventDispatcher _eventDispatcher;

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityHardeningService"/> class.
        /// </summary>
        public SecurityHardeningService(
            ILogger<SecurityHardeningService> logger,
            IServiceProvider serviceProvider,
            IEventDispatcher eventDispatcher)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
        }

        #region Legacy Compatibility Methods

        /// <inheritdoc />
        public async Task<bool> VerifySystemIntegrityAsync(CancellationToken cancellationToken = default)
        {
            var results = await RunFullValidationAsync(cancellationToken);
            return results.All(r => r.ValidationState == SecurityValidationState.Passed || r.ValidationState == SecurityValidationState.Warning);
        }

        /// <inheritdoc />
        public async Task<bool> VerifyDatabaseIntegrityAsync(CancellationToken cancellationToken = default)
        {
            var result = await ValidateDatabaseAsync(cancellationToken);
            return result.ValidationState == SecurityValidationState.Passed || result.ValidationState == SecurityValidationState.Warning;
        }

        /// <inheritdoc />
        public async Task<bool> VerifyAuditIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verifying Audit Chain cryptographic integrity...");
            try
            {
                var auditService = _serviceProvider.GetService<IAuditService>();
                if (auditService == null) return true;

                return await auditService.VerifyAuditChainIntegrityAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify Audit Chain integrity.");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> VerifyPolicyIntegrityAsync(CancellationToken cancellationToken = default)
        {
            var result = await ValidatePolicyAsync(cancellationToken);
            return result.ValidationState == SecurityValidationState.Passed || result.ValidationState == SecurityValidationState.Warning;
        }

        /// <inheritdoc />
        public async Task<bool> VerifyConfigurationIntegrityAsync(CancellationToken cancellationToken = default)
        {
            var result = await ValidateConfigurationAsync(cancellationToken);
            return result.ValidationState == SecurityValidationState.Passed || result.ValidationState == SecurityValidationState.Warning;
        }

        /// <inheritdoc />
        public async Task<bool> VerifyDownloadedMediaIntegrityAsync(CancellationToken cancellationToken = default)
        {
            var result = await ValidateMediaAsync(cancellationToken);
            return result.ValidationState == SecurityValidationState.Passed || result.ValidationState == SecurityValidationState.Warning;
        }

        /// <inheritdoc />
        public async Task<bool> VerifyCommandHistoryIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verifying remote command history records integrity...");
            try
            {
                var repo = _serviceProvider.GetService<IRemoteCommandRepository>();
                if (repo == null) return true;

                var dbService = _serviceProvider.GetService<ILocalDatabaseService>();
                if (dbService == null) return true;

                using var conn = dbService.CreateConnection();
                await conn.OpenAsync(cancellationToken);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM RemoteCommandHistory WHERE Signature IS NULL OR Signature = '';";
                var invalidCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
                if (invalidCount > 0)
                {
                    _logger.LogCritical("COMMAND HISTORY TAMPERED: Detected {Count} commands with missing/empty signature!", invalidCount);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify remote command history integrity.");
                return false;
            }
        }

        #endregion

        #region New Security Hardening Validation APIs

        /// <inheritdoc />
        public async Task<SecurityValidationResult> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("[{CorrelationId}] Starting configuration validation...", correlationId);
            _eventDispatcher.Dispatch(new SecurityValidationStartedEvent(correlationId, "Configuration", "appsettings.json", DateTime.UtcNow));

            SecurityValidationState state = SecurityValidationState.Passed;
            string message = "Configuration validation passed.";
            string? expectedSig = null;
            string? computedSig = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(configPath))
                {
                    state = SecurityValidationState.Failed;
                    message = "Configuration file missing: appsettings.json";
                    _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Configuration", "appsettings.json", stopwatch.Elapsed, message, DateTime.UtcNow));
                    _eventDispatcher.Dispatch(new ValidationWarningEvent(correlationId, "Configuration", "appsettings.json", message, DateTime.UtcNow));
                }
                else
                {
                    string content = await File.ReadAllTextAsync(configPath, cancellationToken);
                    string configVersion = "1.0.0";
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("ConfigVersion", out var vProp) ||
                            doc.RootElement.TryGetProperty("Version", out vProp))
                        {
                            configVersion = vProp.GetString() ?? "1.0.0";
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse configuration version, using default.");
                    }

                    computedSig = await ComputeSha256Async(configPath, cancellationToken);

                    string sigPath = configPath + ".sig";
                    if (File.Exists(sigPath))
                    {
                        expectedSig = await File.ReadAllTextAsync(sigPath, cancellationToken);
                        var sigVerifier = _serviceProvider.GetService<Sayra.Client.Shared.Interfaces.Security.ISignatureVerifier>();
                        string publicKeyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");

                        if (sigVerifier != null && File.Exists(publicKeyPath))
                        {
                            string publicKey = await File.ReadAllTextAsync(publicKeyPath, cancellationToken);
                            bool isValid = sigVerifier.VerifySignature(content, expectedSig, publicKey);
                            if (!isValid)
                            {
                                state = SecurityValidationState.Tampered;
                                message = "Configuration signature verification failed!";
                                _eventDispatcher.Dispatch(new SignatureValidationFailedEvent(correlationId, "Configuration", "appsettings.json", message, DateTime.UtcNow));
                                _eventDispatcher.Dispatch(new TamperDetectedEvent(correlationId, "Configuration", "appsettings.json", message, DateTime.UtcNow));
                            }
                        }
                    }

                    if (state == SecurityValidationState.Passed)
                    {
                        if (string.IsNullOrEmpty(configVersion))
                        {
                            state = SecurityValidationState.Failed;
                            message = "Configuration version is empty or invalid.";
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                state = SecurityValidationState.Failed;
                message = "Validation cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                state = SecurityValidationState.Failed;
                message = $"Configuration validation failed: {ex.Message}";
                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Configuration", "appsettings.json", stopwatch.Elapsed, message, DateTime.UtcNow));
            }

            stopwatch.Stop();
            var result = new SecurityValidationResult
            {
                CheckId = Guid.NewGuid(),
                TargetName = "Configuration",
                ValidationState = state,
                CheckedAt = DateTime.UtcNow,
                ExpectedSignature = expectedSig,
                ComputedSignature = computedSig,
                Message = message
            };

            if (state == SecurityValidationState.Passed)
            {
                _eventDispatcher.Dispatch(new SecurityValidationCompletedEvent(correlationId, "Configuration", "appsettings.json", stopwatch.Elapsed, state, DateTime.UtcNow));
            }
            else if (state == SecurityValidationState.Tampered)
            {
                _eventDispatcher.Dispatch(new IntegrityViolationDetectedEvent(correlationId, "Configuration", "appsettings.json", expectedSig ?? "", computedSig ?? "", DateTime.UtcNow));
            }

            _logger.LogInformation("Security Validation Log - CorrelationId: {CorrelationId}, Type: {Type}, Target: {Target}, Operation: {Operation}, Duration: {Duration}ms, Result: {Result}, Severity: {Severity}, Exception: {Exception}",
                correlationId, "Configuration", "appsettings.json", "ValidateConfiguration", stopwatch.ElapsedMilliseconds, state, state == SecurityValidationState.Passed ? "Info" : "Critical", null);

            return result;
        }

        /// <inheritdoc />
        public async Task<SecurityValidationResult> ValidatePolicyAsync(CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("[{CorrelationId}] Starting policy validation...", correlationId);
            _eventDispatcher.Dispatch(new SecurityValidationStartedEvent(correlationId, "Policy", "LocalPolicies", DateTime.UtcNow));

            SecurityValidationState state = SecurityValidationState.Passed;
            string message = "Policy validation passed.";
            string? expectedSig = null;
            string? computedSig = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var policyRepo = _serviceProvider.GetService<IPolicyRepository>();
                var sigVerifier = _serviceProvider.GetService<Sayra.Client.Shared.Interfaces.Security.ISignatureVerifier>();

                if (policyRepo == null)
                {
                    state = SecurityValidationState.Failed;
                    message = "Policy repository is not available.";
                    _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Policy", "LocalPolicies", stopwatch.Elapsed, message, DateTime.UtcNow));
                }
                else
                {
                    var policies = await policyRepo.GetActivePoliciesAsync();
                    if (policies == null || !policies.Any())
                    {
                        message = "No active policies found to validate.";
                    }
                    else
                    {
                        string publicKeyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
                        foreach (var policy in policies)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (policy.Rules == null || string.IsNullOrEmpty(policy.PolicyId))
                            {
                                state = SecurityValidationState.Failed;
                                message = $"Policy schema validation failed for policy ID '{policy.PolicyId}'.";
                                break;
                            }

                            if (File.Exists(publicKeyPath) && sigVerifier != null)
                            {
                                string publicKey = await File.ReadAllTextAsync(publicKeyPath, cancellationToken);
                                string serializedRules = JsonSerializer.Serialize(policy.Rules);

                                bool isValid = sigVerifier.VerifySignature(serializedRules, policy.Signature, publicKey);
                                if (!isValid)
                                {
                                    state = SecurityValidationState.Tampered;
                                    message = $"Policy tampering detected: Policy ID '{policy.PolicyId}' signature verification failed!";
                                    expectedSig = policy.Signature;
                                    computedSig = "INVALID";

                                    _eventDispatcher.Dispatch(new SignatureValidationFailedEvent(correlationId, "Policy", policy.PolicyId, message, DateTime.UtcNow));
                                    _eventDispatcher.Dispatch(new TamperDetectedEvent(correlationId, "Policy", policy.PolicyId, message, DateTime.UtcNow));
                                    break;
                                }
                            }

                            if (policy.Version <= 0)
                            {
                                state = SecurityValidationState.Failed;
                                message = $"Policy version mismatch: policy '{policy.PolicyId}' version is invalid ({policy.Version}).";
                                _eventDispatcher.Dispatch(new ValidationWarningEvent(correlationId, "Policy", policy.PolicyId, message, DateTime.UtcNow));
                                break;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                state = SecurityValidationState.Failed;
                message = "Validation cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                state = SecurityValidationState.Failed;
                message = $"Policy validation failed: {ex.Message}";
                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Policy", "LocalPolicies", stopwatch.Elapsed, message, DateTime.UtcNow));
            }

            stopwatch.Stop();
            var result = new SecurityValidationResult
            {
                CheckId = Guid.NewGuid(),
                TargetName = "Policy",
                ValidationState = state,
                CheckedAt = DateTime.UtcNow,
                ExpectedSignature = expectedSig,
                ComputedSignature = computedSig,
                Message = message
            };

            if (state == SecurityValidationState.Passed)
            {
                _eventDispatcher.Dispatch(new SecurityValidationCompletedEvent(correlationId, "Policy", "LocalPolicies", stopwatch.Elapsed, state, DateTime.UtcNow));
            }
            else if (state == SecurityValidationState.Tampered)
            {
                _eventDispatcher.Dispatch(new IntegrityViolationDetectedEvent(correlationId, "Policy", "LocalPolicies", expectedSig ?? "", computedSig ?? "", DateTime.UtcNow));
            }

            _logger.LogInformation("Security Validation Log - CorrelationId: {CorrelationId}, Type: {Type}, Target: {Target}, Operation: {Operation}, Duration: {Duration}ms, Result: {Result}, Severity: {Severity}, Exception: {Exception}",
                correlationId, "Policy", "LocalPolicies", "ValidatePolicy", stopwatch.ElapsedMilliseconds, state, state == SecurityValidationState.Passed ? "Info" : "Critical", null);

            return result;
        }

        /// <inheritdoc />
        public async Task<SecurityValidationResult> ValidateDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("[{CorrelationId}] Starting database validation...", correlationId);
            _eventDispatcher.Dispatch(new SecurityValidationStartedEvent(correlationId, "Database", "SQLCipherLocalDB", DateTime.UtcNow));

            SecurityValidationState state = SecurityValidationState.Passed;
            string message = "Database validation passed.";

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var dbService = _serviceProvider.GetService<ILocalDatabaseService>();
                if (dbService == null)
                {
                    state = SecurityValidationState.Failed;
                    message = "Local database service is not available.";
                    _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Database", "SQLCipherLocalDB", stopwatch.Elapsed, message, DateTime.UtcNow));
                }
                else
                {
                    using var conn = dbService.CreateConnection();
                    await conn.OpenAsync(cancellationToken);

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "PRAGMA integrity_check;";
                    var checkResult = await cmd.ExecuteScalarAsync(cancellationToken);

                    if (checkResult == null || !checkResult.ToString()?.Equals("ok", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        state = SecurityValidationState.Tampered;
                        message = $"Database integrity check failed: {checkResult}";
                        _eventDispatcher.Dispatch(new TamperDetectedEvent(correlationId, "Database", "SQLCipherLocalDB", message, DateTime.UtcNow));
                    }
                    else
                    {
                        cmd.CommandText = "PRAGMA user_version;";
                        var userVersion = await cmd.ExecuteScalarAsync(cancellationToken);

                        cmd.CommandText = "PRAGMA foreign_key_check;";
                        var fkCheckResult = await cmd.ExecuteScalarAsync(cancellationToken);
                        if (fkCheckResult != null)
                        {
                            state = SecurityValidationState.Warning;
                            message = $"Database foreign key constraints violation detected: {fkCheckResult}";
                            _eventDispatcher.Dispatch(new ValidationWarningEvent(correlationId, "Database", "SQLCipherLocalDB", message, DateTime.UtcNow));
                        }
                        else
                        {
                            message = $"Database validation passed. Integrity: ok, User Version: {userVersion}.";
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                state = SecurityValidationState.Failed;
                message = "Validation cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                state = SecurityValidationState.Failed;
                message = $"Database corruption or validation failure: {ex.Message}";
                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Database", "SQLCipherLocalDB", stopwatch.Elapsed, message, DateTime.UtcNow));
            }

            stopwatch.Stop();
            var result = new SecurityValidationResult
            {
                CheckId = Guid.NewGuid(),
                TargetName = "Database",
                ValidationState = state,
                CheckedAt = DateTime.UtcNow,
                Message = message
            };

            if (state == SecurityValidationState.Passed)
            {
                _eventDispatcher.Dispatch(new SecurityValidationCompletedEvent(correlationId, "Database", "SQLCipherLocalDB", stopwatch.Elapsed, state, DateTime.UtcNow));
            }

            _logger.LogInformation("Security Validation Log - CorrelationId: {CorrelationId}, Type: {Type}, Target: {Target}, Operation: {Operation}, Duration: {Duration}ms, Result: {Result}, Severity: {Severity}, Exception: {Exception}",
                correlationId, "Database", "SQLCipherLocalDB", "ValidateDatabase", stopwatch.ElapsedMilliseconds, state, state == SecurityValidationState.Passed ? "Info" : "Critical", null);

            return result;
        }

        /// <inheritdoc />
        public async Task<SecurityValidationResult> ValidateMediaAsync(CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("[{CorrelationId}] Starting media assets validation...", correlationId);
            _eventDispatcher.Dispatch(new SecurityValidationStartedEvent(correlationId, "Media", "MediaAssets", DateTime.UtcNow));

            SecurityValidationState state = SecurityValidationState.Passed;
            string message = "Media assets validation passed.";
            string? expectedSig = null;
            string? computedSig = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var repo = _serviceProvider.GetService<IAdvertisementRepository>();
                if (repo == null)
                {
                    message = "Advertisement repository not available.";
                }
                else
                {
                    var list = await repo.GetDownloadedMediaListAsync(cancellationToken);
                    if (list == null || !list.Any())
                    {
                        message = "No downloaded media assets found.";
                    }
                    else
                    {
                        var uniqueHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var media in list)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (!File.Exists(media.MediaPath))
                            {
                                state = SecurityValidationState.Failed;
                                message = $"Ad media file missing: {media.MediaPath} for campaign {media.CampaignId}";
                                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Media", media.MediaPath, stopwatch.Elapsed, message, DateTime.UtcNow));
                                break;
                            }

                            if (!uniqueHashes.Add(media.Checksum))
                            {
                                state = SecurityValidationState.Warning;
                                message = $"Duplicate checksum hash detected for media: {media.MediaPath} (Hash: {media.Checksum})";
                                _eventDispatcher.Dispatch(new ValidationWarningEvent(correlationId, "Media", media.MediaPath, message, DateTime.UtcNow));
                            }

                            string computed = await ComputeSha256Async(media.MediaPath, cancellationToken);
                            if (!computed.Equals(media.Checksum, StringComparison.OrdinalIgnoreCase))
                            {
                                state = SecurityValidationState.Tampered;
                                message = $"MEDIA TAMPERING DETECTED: Checksum mismatch for media {media.MediaPath}. Expected '{media.Checksum}' but computed '{computed}'.";
                                expectedSig = media.Checksum;
                                computedSig = computed;

                                _eventDispatcher.Dispatch(new IntegrityViolationDetectedEvent(correlationId, "Media", media.MediaPath, media.Checksum, computed, DateTime.UtcNow));
                                _eventDispatcher.Dispatch(new TamperDetectedEvent(correlationId, "Media", media.MediaPath, message, DateTime.UtcNow));
                                break;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                state = SecurityValidationState.Failed;
                message = "Validation cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                state = SecurityValidationState.Failed;
                message = $"Media validation failed: {ex.Message}";
                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Media", "MediaAssets", stopwatch.Elapsed, message, DateTime.UtcNow));
            }

            stopwatch.Stop();
            var result = new SecurityValidationResult
            {
                CheckId = Guid.NewGuid(),
                TargetName = "Media",
                ValidationState = state,
                CheckedAt = DateTime.UtcNow,
                ExpectedSignature = expectedSig,
                ComputedSignature = computedSig,
                Message = message
            };

            if (state == SecurityValidationState.Passed)
            {
                _eventDispatcher.Dispatch(new SecurityValidationCompletedEvent(correlationId, "Media", "MediaAssets", stopwatch.Elapsed, state, DateTime.UtcNow));
            }

            _logger.LogInformation("Security Validation Log - CorrelationId: {CorrelationId}, Type: {Type}, Target: {Target}, Operation: {Operation}, Duration: {Duration}ms, Result: {Result}, Severity: {Severity}, Exception: {Exception}",
                correlationId, "Media", "MediaAssets", "ValidateMedia", stopwatch.ElapsedMilliseconds, state, state == SecurityValidationState.Passed ? "Info" : "Critical", null);

            return result;
        }

        /// <inheritdoc />
        public async Task<SecurityValidationResult> ValidatePluginsAsync(CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("[{CorrelationId}] Starting plugins validation...", correlationId);
            _eventDispatcher.Dispatch(new SecurityValidationStartedEvent(correlationId, "Plugins", "PluginsDirectory", DateTime.UtcNow));

            SecurityValidationState state = SecurityValidationState.Passed;
            string message = "Plugins validation passed.";

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
                if (!Directory.Exists(pluginsDir))
                {
                    message = "Plugins directory does not exist. No plugins loaded.";
                }
                else
                {
                    var manifestFiles = Directory.GetFiles(pluginsDir, "plugin.json", SearchOption.AllDirectories);
                    if (manifestFiles.Length == 0)
                    {
                        message = "No plugins registered in plugins directory.";
                    }
                    else
                    {
                        foreach (var manifestPath in manifestFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            string content = await File.ReadAllTextAsync(manifestPath, cancellationToken);
                            using var doc = JsonDocument.Parse(content);
                            var root = doc.RootElement;

                            if (!root.TryGetProperty("Id", out _) || !root.TryGetProperty("Version", out _) || !root.TryGetProperty("EntryPoint", out _))
                            {
                                state = SecurityValidationState.Failed;
                                message = $"Invalid plugin manifest structure: {manifestPath}";
                                _eventDispatcher.Dispatch(new ValidationWarningEvent(correlationId, "Plugins", manifestPath, message, DateTime.UtcNow));
                                break;
                            }

                            string entryPoint = root.GetProperty("EntryPoint").GetString() ?? "";
                            string assemblyPath = Path.Combine(Path.GetDirectoryName(manifestPath) ?? "", entryPoint);
                            if (!File.Exists(assemblyPath))
                            {
                                state = SecurityValidationState.Failed;
                                message = $"Plugin entry point assembly missing: {entryPoint}";
                                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Plugins", manifestPath, stopwatch.Elapsed, message, DateTime.UtcNow));
                                break;
                            }

                            var authenticodeVerifier = _serviceProvider.GetService<IAuthenticodeVerifier>();
                            if (authenticodeVerifier != null)
                            {
                                var authResult = await authenticodeVerifier.VerifyFileAsync(assemblyPath, cancellationToken);
                                if (!authResult.Success)
                                {
                                    state = SecurityValidationState.Untrusted;
                                    message = $"Plugin '{entryPoint}' signature check failed: {authResult.ErrorMessage}";
                                    _eventDispatcher.Dispatch(new SignatureValidationFailedEvent(correlationId, "Plugins", assemblyPath, message, DateTime.UtcNow));
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                state = SecurityValidationState.Failed;
                message = "Validation cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                state = SecurityValidationState.Failed;
                message = $"Plugin validation failed: {ex.Message}";
                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Plugins", "PluginsDirectory", stopwatch.Elapsed, message, DateTime.UtcNow));
            }

            stopwatch.Stop();
            var result = new SecurityValidationResult
            {
                CheckId = Guid.NewGuid(),
                TargetName = "Plugins",
                ValidationState = state,
                CheckedAt = DateTime.UtcNow,
                Message = message
            };

            if (state == SecurityValidationState.Passed)
            {
                _eventDispatcher.Dispatch(new SecurityValidationCompletedEvent(correlationId, "Plugins", "PluginsDirectory", stopwatch.Elapsed, state, DateTime.UtcNow));
            }

            _logger.LogInformation("Security Validation Log - CorrelationId: {CorrelationId}, Type: {Type}, Target: {Target}, Operation: {Operation}, Duration: {Duration}ms, Result: {Result}, Severity: {Severity}, Exception: {Exception}",
                correlationId, "Plugins", "PluginsDirectory", "ValidatePlugins", stopwatch.ElapsedMilliseconds, state, state == SecurityValidationState.Passed ? "Info" : "Critical", null);

            return result;
        }

        /// <inheritdoc />
        public async Task<SecurityValidationResult> ValidatePackagesAsync(CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("[{CorrelationId}] Starting package validation...", correlationId);
            _eventDispatcher.Dispatch(new SecurityValidationStartedEvent(correlationId, "Packages", "UpdatePackages", DateTime.UtcNow));

            SecurityValidationState state = SecurityValidationState.Passed;
            string message = "Package validation passed.";

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var packageVerifier = _serviceProvider.GetService<IPackageVerifier>();
                string packagesDir = Path.Combine(AppContext.BaseDirectory, "packages");

                if (Directory.Exists(packagesDir))
                {
                    var spkFiles = Directory.GetFiles(packagesDir, "*.spk", SearchOption.AllDirectories);
                    if (spkFiles.Length > 0 && packageVerifier != null)
                    {
                        foreach (var spkPath in spkFiles)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            string sigPath = spkPath + ".sig";
                            if (File.Exists(sigPath))
                            {
                                string expectedSignature = await File.ReadAllTextAsync(sigPath, cancellationToken);
                                bool isValid = await packageVerifier.VerifyFileSignatureAsync(spkPath, expectedSignature, cancellationToken);
                                if (!isValid)
                                {
                                    state = SecurityValidationState.Tampered;
                                    message = $"Package file '{spkPath}' signature validation failed!";
                                    _eventDispatcher.Dispatch(new SignatureValidationFailedEvent(correlationId, "Packages", spkPath, message, DateTime.UtcNow));
                                    _eventDispatcher.Dispatch(new TamperDetectedEvent(correlationId, "Packages", spkPath, message, DateTime.UtcNow));
                                    break;
                                }
                            }
                            else
                            {
                                state = SecurityValidationState.Untrusted;
                                message = $"Package file '{spkPath}' is missing a required digital signature.";
                                _eventDispatcher.Dispatch(new SignatureValidationFailedEvent(correlationId, "Packages", spkPath, message, DateTime.UtcNow));
                                break;
                            }
                        }
                    }
                    else
                    {
                        message = "No packages found to validate.";
                    }
                }
                else
                {
                    message = "Packages directory does not exist.";
                }
            }
            catch (OperationCanceledException)
            {
                state = SecurityValidationState.Failed;
                message = "Validation cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                state = SecurityValidationState.Failed;
                message = $"Package validation failed: {ex.Message}";
                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Packages", "UpdatePackages", stopwatch.Elapsed, message, DateTime.UtcNow));
            }

            stopwatch.Stop();
            var result = new SecurityValidationResult
            {
                CheckId = Guid.NewGuid(),
                TargetName = "Packages",
                ValidationState = state,
                CheckedAt = DateTime.UtcNow,
                Message = message
            };

            if (state == SecurityValidationState.Passed)
            {
                _eventDispatcher.Dispatch(new SecurityValidationCompletedEvent(correlationId, "Packages", "UpdatePackages", stopwatch.Elapsed, state, DateTime.UtcNow));
            }

            _logger.LogInformation("Security Validation Log - CorrelationId: {CorrelationId}, Type: {Type}, Target: {Target}, Operation: {Operation}, Duration: {Duration}ms, Result: {Result}, Severity: {Severity}, Exception: {Exception}",
                correlationId, "Packages", "UpdatePackages", "ValidatePackages", stopwatch.ElapsedMilliseconds, state, state == SecurityValidationState.Passed ? "Info" : "Critical", null);

            return result;
        }

        /// <inheritdoc />
        public async Task<SecurityValidationResult> ValidateExecutableAsync(CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _logger.LogInformation("[{CorrelationId}] Starting executable validation...", correlationId);
            _eventDispatcher.Dispatch(new SecurityValidationStartedEvent(correlationId, "Executable", "SayraClientExecutable", DateTime.UtcNow));

            SecurityValidationState state = SecurityValidationState.Passed;
            string message = "Executable validation passed.";
            string? computedSig = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var mainModulePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(mainModulePath) || !File.Exists(mainModulePath))
                {
                    mainModulePath = typeof(SecurityHardeningService).Assembly.Location;
                }

                if (string.IsNullOrEmpty(mainModulePath) || !File.Exists(mainModulePath))
                {
                    state = SecurityValidationState.Failed;
                    message = "Unable to resolve executing binary path.";
                    _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Executable", "SayraClientExecutable", stopwatch.Elapsed, message, DateTime.UtcNow));
                }
                else
                {
                    computedSig = await ComputeSha256Async(mainModulePath, cancellationToken);

                    var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(mainModulePath);
                    string productVersion = versionInfo.ProductVersion ?? "1.0.0";

                    var authenticodeVerifier = _serviceProvider.GetService<IAuthenticodeVerifier>();
                    if (authenticodeVerifier != null)
                    {
                        var authResult = await authenticodeVerifier.VerifyFileAsync(mainModulePath, cancellationToken);
                        if (!authResult.Success)
                        {
                            state = SecurityValidationState.Untrusted;
                            message = $"Executable Authenticode verification failed: {authResult.ErrorMessage}";
                            _eventDispatcher.Dispatch(new SignatureValidationFailedEvent(correlationId, "Executable", mainModulePath, message, DateTime.UtcNow));
                        }
                        else
                        {
                            message = $"Executable validated successfully. Publisher: {authResult.Publisher}, Version: {productVersion}";
                        }
                    }
                    else
                    {
                        message = $"Executable validation completed. Version: {productVersion}";
                    }
                }
            }
            catch (OperationCanceledException)
            {
                state = SecurityValidationState.Failed;
                message = "Validation cancelled.";
                throw;
            }
            catch (Exception ex)
            {
                state = SecurityValidationState.Failed;
                message = $"Executable validation failed: {ex.Message}";
                _eventDispatcher.Dispatch(new SecurityValidationFailedEvent(correlationId, "Executable", "SayraClientExecutable", stopwatch.Elapsed, message, DateTime.UtcNow));
            }

            stopwatch.Stop();
            var result = new SecurityValidationResult
            {
                CheckId = Guid.NewGuid(),
                TargetName = "Executable",
                ValidationState = state,
                CheckedAt = DateTime.UtcNow,
                ComputedSignature = computedSig,
                Message = message
            };

            if (state == SecurityValidationState.Passed)
            {
                _eventDispatcher.Dispatch(new SecurityValidationCompletedEvent(correlationId, "Executable", "SayraClientExecutable", stopwatch.Elapsed, state, DateTime.UtcNow));
            }

            _logger.LogInformation("Security Validation Log - CorrelationId: {CorrelationId}, Type: {Type}, Target: {Target}, Operation: {Operation}, Duration: {Duration}ms, Result: {Result}, Severity: {Severity}, Exception: {Exception}",
                correlationId, "Executable", "SayraClientExecutable", "ValidateExecutable", stopwatch.ElapsedMilliseconds, state, state == SecurityValidationState.Passed ? "Info" : "Critical", null);

            return result;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<SecurityValidationResult>> RunFullValidationAsync(CancellationToken cancellationToken = default)
        {
            var correlationId = Guid.NewGuid().ToString();
            _logger.LogInformation("[{CorrelationId}] Starting system-wide security validation run.", correlationId);

            _eventDispatcher.Dispatch(new SecurityValidationStartedEvent(correlationId, "Full", "System", DateTime.UtcNow));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var configTask = ValidateConfigurationAsync(cancellationToken);
            var policyTask = ValidatePolicyAsync(cancellationToken);
            var dbTask = ValidateDatabaseAsync(cancellationToken);
            var mediaTask = ValidateMediaAsync(cancellationToken);
            var pluginTask = ValidatePluginsAsync(cancellationToken);
            var packageTask = ValidatePackagesAsync(cancellationToken);
            var exeTask = ValidateExecutableAsync(cancellationToken);

            await Task.WhenAll(configTask, policyTask, dbTask, mediaTask, pluginTask, packageTask, exeTask);

            var results = new List<SecurityValidationResult>
            {
                await configTask,
                await policyTask,
                await dbTask,
                await mediaTask,
                await pluginTask,
                await packageTask,
                await exeTask
            };

            stopwatch.Stop();

            var overallState = SecurityValidationState.Passed;
            if (results.Any(r => r.ValidationState == SecurityValidationState.Tampered))
                overallState = SecurityValidationState.Tampered;
            else if (results.Any(r => r.ValidationState == SecurityValidationState.Untrusted))
                overallState = SecurityValidationState.Untrusted;
            else if (results.Any(r => r.ValidationState == SecurityValidationState.Failed))
                overallState = SecurityValidationState.Failed;
            else if (results.Any(r => r.ValidationState == SecurityValidationState.Warning))
                overallState = SecurityValidationState.Warning;

            _logger.LogInformation("[{CorrelationId}] System-wide security validation run completed in {Duration}ms with state: {State}",
                correlationId, stopwatch.ElapsedMilliseconds, overallState);

            _eventDispatcher.Dispatch(new SecurityValidationCompletedEvent(correlationId, "Full", "System", stopwatch.Elapsed, overallState, DateTime.UtcNow));

            return results;
        }

        #endregion

        #region Helpers

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            byte[] hashBytes = await sha.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(hashBytes).ToLower();
        }

        #endregion
    }
}

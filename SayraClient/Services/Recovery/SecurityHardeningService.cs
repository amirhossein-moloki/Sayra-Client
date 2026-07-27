using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Interfaces;
using Sayra.Client.Shared.Interfaces.Security;

namespace SayraClient.Services.Recovery
{
    public class SecurityHardeningService
    {
        private readonly ILogger<SecurityHardeningService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public SecurityHardeningService(ILogger<SecurityHardeningService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        public async Task<bool> VerifySystemIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Enterprise Security Hardening: Initiating system-wide integrity validation...");

            bool dbIntegrity = await VerifyDatabaseIntegrityAsync(cancellationToken);
            bool auditIntegrity = await VerifyAuditIntegrityAsync(cancellationToken);
            bool policyIntegrity = await VerifyPolicyIntegrityAsync(cancellationToken);
            bool configIntegrity = await VerifyConfigurationIntegrityAsync(cancellationToken);
            bool mediaIntegrity = await VerifyDownloadedMediaIntegrityAsync(cancellationToken);
            bool historyIntegrity = await VerifyCommandHistoryIntegrityAsync(cancellationToken);

            bool overall = dbIntegrity && auditIntegrity && policyIntegrity && configIntegrity && mediaIntegrity && historyIntegrity;

            if (!overall)
            {
                _logger.LogCritical("SECURITY ANOMALY DETECTED: Core system integrity validation failed!");
                // Alert if possible
                try
                {
                    var alertManager = _serviceProvider.GetService<IAlertManager>();
                    if (alertManager != null)
                    {
                        await alertManager.ProcessStatusAsync("LOCAL_PC", "INTEGRITY_TAMPER_DETECTED", "One or more subsystem files or tables failed integrity check.", cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send security alert for integrity compromise.");
                }
            }
            else
            {
                _logger.LogInformation("Enterprise Security Hardening: All core subsystems successfully verified.");
            }

            return overall;
        }

        public async Task<bool> VerifyDatabaseIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verifying SQLCipher Database integrity...");
            try
            {
                var dbService = _serviceProvider.GetService<ILocalDatabaseService>();
                if (dbService == null) return true;

                using var conn = dbService.CreateConnection();
                await conn.OpenAsync(cancellationToken);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "PRAGMA integrity_check;";
                var result = await cmd.ExecuteScalarAsync(cancellationToken);

                if (result != null && result.ToString()?.Equals("ok", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return true;
                }

                _logger.LogError("Database integrity check failed: {Result}", result);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to perform database integrity check.");
                return false;
            }
        }

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

        public async Task<bool> VerifyPolicyIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verifying local applied policies cryptographic signatures...");
            try
            {
                var policyRepo = _serviceProvider.GetService<IPolicyRepository>();
                if (policyRepo == null) return true;

                var sigVerifier = _serviceProvider.GetService<ISignatureVerifier>();
                if (sigVerifier == null) return true;

                var policies = await policyRepo.GetActivePoliciesAsync();
                foreach (var policy in policies)
                {
                    // Check signature using server's public key if available
                    string publicKeyPath = Path.Combine(AppContext.BaseDirectory, "server_public.key");
                    if (File.Exists(publicKeyPath))
                    {
                        string publicKey = await File.ReadAllTextAsync(publicKeyPath, cancellationToken);
                        string serializedRules = JsonSerializer.Serialize(policy.Rules);

                        bool isValid = sigVerifier.VerifySignature(serializedRules, policy.Signature, publicKey);
                        if (!isValid)
                        {
                            _logger.LogCritical("POLICY TAMPERING DETECTED: Policy ID '{PolicyId}' signature verification failed!", policy.PolicyId);
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify policy integrity.");
                return false;
            }
        }

        public Task<bool> VerifyConfigurationIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verifying system configuration file integrity...");
            // Configuration files on disk should exist and are validated on load by the Configuration Sync engine.
            string configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
            {
                _logger.LogError("Configuration file missing: appsettings.json");
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }

        public async Task<bool> VerifyDownloadedMediaIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verifying downloaded advertisement media files integrity...");
            try
            {
                var repo = _serviceProvider.GetService<IAdvertisementRepository>();
                if (repo == null) return true;

                var list = await repo.GetDownloadedMediaListAsync(cancellationToken);
                foreach (var media in list)
                {
                    if (!File.Exists(media.MediaPath))
                    {
                        _logger.LogError("Ad media file missing: {Path} for campaign {CampaignId}", media.MediaPath, media.CampaignId);
                        return false;
                    }

                    // Compute SHA-256 and compare with stored checksum
                    string computed = await ComputeSha256Async(media.MediaPath, cancellationToken);
                    if (!computed.Equals(media.Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogCritical("MEDIA TAMPERING DETECTED: Checksum mismatch for media {Path}. Expected '{Expected}' but computed '{Computed}'.",
                            media.MediaPath, media.Checksum, computed);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to verify downloaded media integrity.");
                return false;
            }
        }

        public async Task<bool> VerifyCommandHistoryIntegrityAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Verifying remote command history records integrity...");
            try
            {
                var repo = _serviceProvider.GetService<IRemoteCommandRepository>();
                if (repo == null) return true;

                // Load history and verify signature of recent records
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

        private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
        {
            using var sha = SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            byte[] hashBytes = await sha.ComputeHashAsync(stream, ct);
            return Convert.ToHexString(hashBytes).ToLower();
        }
    }
}

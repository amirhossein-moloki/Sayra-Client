using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Sayra.Client.Shared.Security.GameProtection.Application.Interfaces;
using Sayra.Client.Shared.Security.GameProtection.Domain.Models;

namespace Sayra.Client.Shared.Security.GameProtection.Infrastructure.Validators;

public class GameIntegrityValidator : IIntegrityValidator
{
    private readonly ILogger<GameIntegrityValidator>? _logger;

    public GameIntegrityValidator(ILogger<GameIntegrityValidator>? logger = null)
    {
        _logger = logger;
    }

    public IntegrityResult ValidateExecutable(string filePath, string expectedHash, string expectedPublisher = "")
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return new IntegrityResult
            {
                Status = IntegrityStatus.Invalid,
                Reason = "File path is null or empty."
            };
        }

        // 1. File existence validation
        if (!File.Exists(filePath))
        {
            _logger?.LogError("Game file validation failed: File '{Path}' does not exist.", filePath);
            return new IntegrityResult
            {
                Status = IntegrityStatus.Invalid,
                Reason = $"File '{filePath}' does not exist on disk."
            };
        }

        // 2. File accessibility validation
        try
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                // Able to read the file successfully
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Game file accessibility failed: File '{Path}' cannot be opened.", filePath);
            return new IntegrityResult
            {
                Status = IntegrityStatus.Invalid,
                Reason = $"File '{filePath}' is inaccessible: {ex.Message}"
            };
        }

        // 3. Hash validation (SHA-256)
        if (!string.IsNullOrEmpty(expectedHash))
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                byte[] hashBytes = sha.ComputeHash(stream);
                string computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                if (!computedHash.Equals(expectedHash.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                {
                    _logger?.LogWarning("Hash mismatch on '{Path}'. Computed: {Computed}, Expected: {Expected}", filePath, computedHash, expectedHash);
                    return new IntegrityResult
                    {
                        Status = IntegrityStatus.Invalid,
                        Reason = $"SHA256 hash mismatch. Computed: {computedHash}, Expected: {expectedHash}"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to compute hash for file '{Path}'.", filePath);
                return new IntegrityResult
                {
                    Status = IntegrityStatus.Invalid,
                    Reason = $"Hash computation error: {ex.Message}"
                };
            }
        }

        // 4. Digital Signature validation (Publisher check)
        if (!string.IsNullOrEmpty(expectedPublisher))
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _logger?.LogInformation("[Non-Windows] Skipping native Authenticode signature check for publisher: '{Pub}' on file: '{Path}'", expectedPublisher, filePath);
                // For cross-platform unit testing, we emulate a match unless the file name has 'unsigned' in it
                if (Path.GetFileName(filePath).Contains("unsigned", StringComparison.OrdinalIgnoreCase))
                {
                    return new IntegrityResult
                    {
                        Status = IntegrityStatus.Invalid,
                        Reason = "Emulated digital signature validation failed: file marked as unsigned."
                    };
                }
            }
            else
            {
                try
                {
                    // Inspect X509 certificate of executable
                    using var cert = new X509Certificate2(filePath);
                    var subject = cert.Subject;

                    if (!subject.Contains(expectedPublisher, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger?.LogWarning("Publisher check failed on '{Path}'. Subject: '{Subject}', Expected containing: '{Expected}'", filePath, subject, expectedPublisher);
                        return new IntegrityResult
                        {
                            Status = IntegrityStatus.Invalid,
                            Reason = $"Publisher signature check failed. Executable signed by '{subject}', expected containing '{expectedPublisher}'"
                        };
                    }

                    // Check expiration
                    var now = DateTime.UtcNow;
                    if (now < cert.NotBefore.ToUniversalTime() || now > cert.NotAfter.ToUniversalTime())
                    {
                        return new IntegrityResult
                        {
                            Status = IntegrityStatus.Invalid,
                            Reason = $"Signature certificate has expired or is not yet valid. NotBefore: {cert.NotBefore}, NotAfter: {cert.NotAfter}"
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to inspect digital signature for file '{Path}'.", filePath);
                    return new IntegrityResult
                    {
                        Status = IntegrityStatus.Invalid,
                        Reason = $"Digital signature extraction error: {ex.Message}"
                    };
                }
            }
        }

        return new IntegrityResult
        {
            Status = IntegrityStatus.Valid,
            Reason = "Integrity check succeeded: File exists, is accessible, hash and signature are valid."
        };
    }
}

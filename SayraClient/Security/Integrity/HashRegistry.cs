using System;
using System.Collections.Concurrent;
using System.IO;

namespace SayraClient.Security.Integrity;

/// <summary>
/// A centralized thread-safe registry maintaining expected hashes for critical executable, DLL, and configuration assets.
/// Supports SHA-256, SHA-384, and SHA-512 algorithms.
/// </summary>
public class HashRegistry
{
    private readonly ConcurrentDictionary<string, string> _sha256Registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _sha384Registry = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _sha512Registry = new(StringComparer.OrdinalIgnoreCase);

    public HashRegistry()
    {
        // Pre-populate standard known values for testing and production baselines.
        // In real deployments, this metadata can be loaded dynamically from a cryptographically signed manifest.
        RegisterHash("SayraClient.exe", "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", "SHA-256"); // empty file SHA-256 example
        RegisterHash("Sayra.Client.Shared.dll", "da39a3ee5e6b4b0d3255bfef95601890afd80709", "SHA-256");
        RegisterHash("client_config.json", "d6e3e5da3c220cae5f32a4e402b85038c1054a861cf86903bfca21", "SHA-256");
    }

    /// <summary>
    /// Registers a trusted file's expected hash under the specified hashing algorithm.
    /// </summary>
    public void RegisterHash(string identifier, string expectedHash, string algorithm)
    {
        if (string.IsNullOrWhiteSpace(identifier)) throw new ArgumentException("Identifier cannot be null or empty.", nameof(identifier));
        if (string.IsNullOrWhiteSpace(expectedHash)) throw new ArgumentException("Hash cannot be null or empty.", nameof(expectedHash));

        var cleanHash = expectedHash.Replace("-", "").ToLowerInvariant();
        var key = Path.GetFileName(identifier); // Match on filename only to ignore relative path differences

        switch (algorithm?.ToUpperInvariant())
        {
            case "SHA-256":
            case "SHA256":
                _sha256Registry[key] = cleanHash;
                break;
            case "SHA-384":
            case "SHA384":
                _sha384Registry[key] = cleanHash;
                break;
            case "SHA-512":
            case "SHA512":
                _sha512Registry[key] = cleanHash;
                break;
            default:
                throw new NotSupportedException($"Hashing algorithm '{algorithm}' is not supported by the integrity subsystem.");
        }
    }

    /// <summary>
    /// Gets the expected hash for a given file identifier.
    /// </summary>
    public string? GetExpectedHash(string identifier, string algorithm)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return null;

        var key = Path.GetFileName(identifier);

        switch (algorithm?.ToUpperInvariant())
        {
            case "SHA-256":
            case "SHA256":
                return _sha256Registry.TryGetValue(key, out var hash256) ? hash256 : null;
            case "SHA-384":
            case "SHA384":
                return _sha384Registry.TryGetValue(key, out var hash384) ? hash384 : null;
            case "SHA-512":
            case "SHA512":
                return _sha512Registry.TryGetValue(key, out var hash512) ? hash512 : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Verifies if a computed file hash matches the trusted expected hash from the registry.
    /// If no registry entry exists for the file, this method can fall back to allowing or rejecting depending on policy.
    /// </summary>
    public bool VerifyHash(string identifier, string computedHash, string algorithm)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(computedHash)) return false;

        var cleanComputed = computedHash.Replace("-", "").ToLowerInvariant();
        var expected = GetExpectedHash(identifier, algorithm);

        if (expected == null)
        {
            // If the hash is not in the registry, we treat it as unregistered (tampered or unknown)
            return false;
        }

        return expected.Equals(cleanComputed, StringComparison.Ordinal);
    }
}

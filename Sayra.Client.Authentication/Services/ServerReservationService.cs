using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sayra.Client.Authentication.Contracts;

namespace Sayra.Client.Authentication.Services;

public class ServerReservationService : IServerReservationService
{
    private readonly ILogger<ServerReservationService> _logger;
    private readonly string _cacheFilePath;

    public ServerReservationService(ILogger<ServerReservationService> logger)
    {
        _logger = logger;
        _cacheFilePath = Path.Combine(AppContext.BaseDirectory, "Data", "Configuration", "reservation_cache.json");
    }

    public async Task<ReservationValidationResult> ValidateReservationAsync(string username, string reservationId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting online reservation validation for {Username}, reservation {ReservationId}...", username, reservationId);

        // This requires the server project synchronization phase.
        // As per guidelines, we do not implement fake logic, but prepare extension points and mark as waiting for server synchronization.
        _logger.LogWarning("Online reservation validation is currently: Waiting for Server Synchronization Phase.");

        // Fall back to offline cached reservation if available
        var cached = await GetOfflineCachedReservationAsync(username);
        if (cached != null && (string.IsNullOrEmpty(reservationId) || cached.ReservationId == reservationId))
        {
            if (cached.IsExpired)
            {
                _logger.LogWarning("Cached reservation for {Username} has expired.", username);
                return new ReservationValidationResult
                {
                    Success = false,
                    Message = "Reservation has expired."
                };
            }

            _logger.LogInformation("Successfully validated reservation from local offline fallback cache.");
            return new ReservationValidationResult
            {
                Success = true,
                Message = "Validated via local offline cache.",
                Reservation = cached
            };
        }

        return new ReservationValidationResult
        {
            Success = false,
            Message = "Unable to connect to Server Reservation Layer. Waiting for Server Synchronization Phase."
        };
    }

    public async Task<bool> ValidateCreditsAsync(string username, double requiredCredits, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating credits for {Username}. Required: {Required}", username, requiredCredits);

        // Waiting for Server Synchronization Phase. Check offline fallback cache first.
        var cached = await GetOfflineCachedReservationAsync(username);
        if (cached != null)
        {
            return cached.RemainingCredits >= requiredCredits;
        }

        _logger.LogWarning("ValidateCredits: Waiting for Server Synchronization Phase.");
        return false;
    }

    public async Task<bool> ValidateSessionOwnershipAsync(string username, string sessionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating session ownership for {Username}, Session {SessionId}", username, sessionId);

        // Waiting for Server Synchronization Phase. Check offline fallback cache first.
        var cached = await GetOfflineCachedReservationAsync(username);
        if (cached != null)
        {
            return true;
        }

        _logger.LogWarning("ValidateSessionOwnership: Waiting for Server Synchronization Phase.");
        return false;
    }

    public async Task<ReservationInfo?> GetOfflineCachedReservationAsync(string username)
    {
        if (!File.Exists(_cacheFilePath)) return null;

        try
        {
            string json = await File.ReadAllTextAsync(_cacheFilePath);
            var cache = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ReservationInfo>>(json);
            if (cache != null && cache.TryGetValue(username.ToLowerInvariant(), out var info))
            {
                return info;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read offline reservation cache.");
        }

        return null;
    }

    public async Task CacheReservationOfflineAsync(ReservationInfo reservation)
    {
        try
        {
            string dir = Path.GetDirectoryName(_cacheFilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            System.Collections.Generic.Dictionary<string, ReservationInfo> cache;
            if (File.Exists(_cacheFilePath))
            {
                string json = await File.ReadAllTextAsync(_cacheFilePath);
                cache = JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, ReservationInfo>>(json) ?? new();
            }
            else
            {
                cache = new();
            }

            cache[reservation.Username.ToLowerInvariant()] = reservation;

            string updatedJson = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_cacheFilePath, updatedJson);
            _logger.LogInformation("Cached reservation for {Username} offline.", reservation.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cache reservation offline.");
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Sayra.Client.Authentication.Contracts;

public class ReservationInfo
{
    public string ReservationId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string StationId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public double RemainingCredits { get; set; }
    public bool IsExpired => DateTime.UtcNow > EndTime;
}

public class ReservationValidationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ReservationInfo? Reservation { get; set; }
}

public interface IServerReservationService
{
    Task<ReservationValidationResult> ValidateReservationAsync(string username, string reservationId, CancellationToken cancellationToken = default);
    Task<bool> ValidateCreditsAsync(string username, double requiredCredits, CancellationToken cancellationToken = default);
    Task<bool> ValidateSessionOwnershipAsync(string username, string sessionId, CancellationToken cancellationToken = default);
    Task<ReservationInfo?> GetOfflineCachedReservationAsync(string username);
    Task CacheReservationOfflineAsync(ReservationInfo reservation);
}

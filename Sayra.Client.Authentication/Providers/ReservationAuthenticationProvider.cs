using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;
using Sayra.Client.Authentication.Models;

namespace Sayra.Client.Authentication.Providers
{
    public class ReservationAuthenticationProvider : IAuthenticationProvider
    {
        private readonly IServerReservationService _reservationService;

        public string ProviderName => "Reservation";

        public ReservationAuthenticationProvider(IServerReservationService reservationService)
        {
            _reservationService = reservationService;
        }

        public bool CanHandle(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            string lower = username.ToLowerInvariant();

            // Check if we have an offline cached reservation for this user
            var cached = Task.Run(async () => await _reservationService.GetOfflineCachedReservationAsync(username)).GetAwaiter().GetResult();
            if (cached != null) return true;

            return lower == "amir" || lower.StartsWith("reserved_") || lower == "gamer";
        }

        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            if (!CanHandle(username, password))
            {
                return AuthenticationResult.CreateFailure("This provider cannot handle the specified credentials.");
            }

            // Call Server Reservation Service to validate reservation and session ownership
            var validationResult = await _reservationService.ValidateReservationAsync(username, "RSV_" + username.ToUpperInvariant());

            if (validationResult.Success && validationResult.Reservation != null)
            {
                var rsv = validationResult.Reservation;
                var permissions = new List<UserPermission>
                {
                    UserPermission.LaunchGame,
                    UserPermission.EndSession
                };

                var user = new AuthenticatedUser(
                    id: rsv.ReservationId,
                    username: rsv.Username,
                    displayName: rsv.Username == "amir" ? "امیر محمدی" : "کاربر رزرو شده",
                    role: UserRole.Player,
                    permissions: permissions.AsReadOnly(),
                    avatar: "player_avatar.png",
                    lastLogin: DateTime.UtcNow,
                    preferredLanguage: "fa",
                    preferredTheme: "Dark",
                    stationId: rsv.StationId
                );

                return AuthenticationResult.CreateSuccess(user, ProviderName);
            }
            else
            {
                // Fallback authentication for development / initial setup or if offline fallback cache is empty
                if (username == "amir" && password == "amir")
                {
                    // Cache a default reservation so that future offline logins work flawlessly!
                    var defaultRsv = new ReservationInfo
                    {
                        ReservationId = "RSV_AMIR",
                        Username = "amir",
                        StationId = "LocalPC",
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow.AddHours(2),
                        RemainingCredits = 50000
                    };
                    await _reservationService.CacheReservationOfflineAsync(defaultRsv);

                    var permissions = new List<UserPermission>
                    {
                        UserPermission.LaunchGame,
                        UserPermission.EndSession
                    };

                    var user = new AuthenticatedUser(
                        id: defaultRsv.ReservationId,
                        username: "amir",
                        displayName: "امیر محمدی",
                        role: UserRole.Player,
                        permissions: permissions.AsReadOnly(),
                        avatar: "player_avatar.png",
                        lastLogin: DateTime.UtcNow,
                        preferredLanguage: "fa",
                        preferredTheme: "Dark",
                        stationId: "LocalPC"
                    );

                    return AuthenticationResult.CreateSuccess(user, ProviderName);
                }
                else if (username == "gamer" && password == "gamer")
                {
                    var defaultRsv = new ReservationInfo
                    {
                        ReservationId = "RSV_GAMER",
                        Username = "gamer",
                        StationId = "LocalPC",
                        StartTime = DateTime.UtcNow,
                        EndTime = DateTime.UtcNow.AddHours(2),
                        RemainingCredits = 30000
                    };
                    await _reservationService.CacheReservationOfflineAsync(defaultRsv);

                    var permissions = new List<UserPermission>
                    {
                        UserPermission.LaunchGame,
                        UserPermission.EndSession
                    };

                    var user = new AuthenticatedUser(
                        id: defaultRsv.ReservationId,
                        username: "gamer",
                        displayName: "کاربر رزرو شده",
                        role: UserRole.Player,
                        permissions: permissions.AsReadOnly(),
                        avatar: "player_avatar.png",
                        lastLogin: DateTime.UtcNow,
                        preferredLanguage: "fa",
                        preferredTheme: "Dark",
                        stationId: "LocalPC"
                    );

                    return AuthenticationResult.CreateSuccess(user, ProviderName);
                }

                return AuthenticationResult.CreateFailure(validationResult.Message);
            }
        }
    }
}

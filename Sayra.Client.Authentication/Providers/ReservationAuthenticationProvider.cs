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
        public string ProviderName => "Reservation";

        public bool CanHandle(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            string lower = username.ToLowerInvariant();
            // In a real system, we'd query the reservation system.
            // For now, "amir" represents our primary reserved gamer.
            return lower == "amir" || lower.StartsWith("reserved_") || lower == "gamer";
        }

        public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            if (!CanHandle(username, password))
            {
                return AuthenticationResult.CreateFailure("This provider cannot handle the specified credentials.");
            }

            // Let's simulate checking a reservation database/service
            await Task.Delay(100); // Simulate network latency

            string lowerUser = username.ToLowerInvariant();
            bool isSuccess = (lowerUser == "amir" && password == "amir") ||
                             (lowerUser == "gamer" && password == "gamer") ||
                             lowerUser.StartsWith("reserved_"); // Auto-success for automated test users if needed, or specific password

            if (isSuccess)
            {
                var permissions = new List<UserPermission>
                {
                    UserPermission.LaunchGame,
                    UserPermission.EndSession
                };

                var user = new AuthenticatedUser(
                    id: $"RSV_{username.ToUpperInvariant()}",
                    username: username,
                    displayName: username == "amir" ? "امیر محمدی" : "کاربر رزرو شده",
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
            else
            {
                return AuthenticationResult.CreateFailure("رمز عبور برای کاربر رزرو شده نادرست است.");
            }
        }
    }
}

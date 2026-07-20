using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sayra.Client.Authentication.Contracts;
using Sayra.Client.Authentication.Enums;
using Sayra.Client.Authentication.Models;

namespace Sayra.Client.Authentication.Providers
{
    public class OfflineAuthenticationProvider : IAuthenticationProvider
    {
        public string ProviderName => "Offline";

        public bool CanHandle(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            string lower = username.ToLowerInvariant();
            return lower == "guest" || lower == "offline_user" || lower == "offline";
        }

        public Task<AuthenticationResult> AuthenticateAsync(string username, string password)
        {
            if (!CanHandle(username, password))
            {
                return Task.FromResult(AuthenticationResult.CreateFailure("This provider cannot handle the specified credentials."));
            }

            // Offline mode typically allows simple local guest credentials without hard passwords
            var permissions = new List<UserPermission>
            {
                UserPermission.LaunchGame,
                UserPermission.EndSession
            };

            var user = new AuthenticatedUser(
                id: $"OFF_{username.ToUpperInvariant()}",
                username: username,
                displayName: username == "guest" ? "کاربر مهمان (آفلاین)" : "کاربر آفلاین",
                role: UserRole.Guest,
                permissions: permissions.AsReadOnly(),
                avatar: "guest_avatar.png",
                lastLogin: DateTime.UtcNow,
                preferredLanguage: "fa",
                preferredTheme: "Dark",
                stationId: "LocalPC"
            );

            // We can indicate that this is successful offline, but requiresSynchronization or has restricted permissions
            return Task.FromResult(AuthenticationResult.CreateSuccess(user, ProviderName, requiresSynchronization: true));
        }
    }
}
